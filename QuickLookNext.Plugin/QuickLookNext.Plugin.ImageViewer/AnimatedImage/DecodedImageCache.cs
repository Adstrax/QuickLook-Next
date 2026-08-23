// Copyright © 2017-2026 QL-Win Contributors
//
// This file is part of QuickLookNext program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace QuickLookNext.Plugin.ImageViewer.AnimatedImage;

/// <summary>
/// Small LRU cache for decoded image frames (v1.2.13). Previewing the same
/// image again - or switching back and forth between a few images - used to
/// re-decode the whole file every time, which is exactly where the spinner is
/// most noticeable. Caching the downscaled thumbnail and the first rendered
/// frame makes repeated previews nearly instant. Memory is bounded by both the
/// entry count and the total number of pixels.
/// </summary>
internal static class DecodedImageCache
{
    private const int MaxEntries = 24;
    private const long MaxTotalPixels = 48L * 1024 * 1024; // ~48 MP (~192 MB BGRA worst case)
    private const long MaxSinglePixels = 24L * 1024 * 1024;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, LinkedListNode<Entry>> Map = [];
    private static readonly LinkedList<Entry> Lru = [];

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required BitmapSource Source { get; init; }
        public required long Pixels { get; init; }
    }

    public static bool TryGet(string key, out BitmapSource source)
    {
        lock (Sync)
        {
            if (Map.TryGetValue(key, out var node))
            {
                Lru.Remove(node);
                Lru.AddFirst(node);
                source = node.Value.Source;
                return true;
            }
        }

        source = null;
        return false;
    }

    public static void Add(string key, BitmapSource source)
    {
        if (source == null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            return;

        var pixels = (long)source.PixelWidth * source.PixelHeight;
        if (pixels > MaxSinglePixels)
            return;

        // The source must be shareable across threads (it is created on a
        // background decode thread and consumed on the UI thread).
        if (!source.IsFrozen)
        {
            if (!source.CanFreeze)
                return;
            source.Freeze();
        }

        lock (Sync)
        {
            if (Map.TryGetValue(key, out var existing))
            {
                Lru.Remove(existing);
                Map.Remove(key);
            }

            var node = new LinkedListNode<Entry>(new Entry
            {
                Key = key,
                Source = source,
                Pixels = pixels,
            });
            Map[key] = node;
            Lru.AddFirst(node);

            EvictLocked();
        }
    }

    private static void EvictLocked()
    {
        var total = 0L;
        foreach (var entry in Lru)
            total += entry.Pixels;

        while (Lru.Count > MaxEntries || total > MaxTotalPixels)
        {
            var last = Lru.Last;
            if (last == null)
                break;

            total -= last.Value.Pixels;
            Map.Remove(last.Value.Key);
            Lru.RemoveLast();
        }
    }
}
