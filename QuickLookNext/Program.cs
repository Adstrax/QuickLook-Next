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
using System.IO;
using System.Linq;
using System.Reflection;

namespace QuickLookNext;

public static class Program
{
    // v3.3.0: once-per-process index of every DLL under the app directory
    // (simple assembly name -> full path). The release package keeps
    // third-party libraries in lib\, so most AssemblyResolve hits are served
    // by an O(1) lookup instead of a recursive directory scan per miss.
    private static readonly Lazy<Dictionary<string, string>> AssemblyIndex = new(BuildAssemblyIndex);

    private static Dictionary<string, string> BuildAssemblyIndex()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", SearchOption.AllDirectories))
            {
                map.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
            }
        }
        catch
        {
            // Indexing is best-effort; the recursive fallback below still works.
        }

        return map;
    }

    /// <summary>
    /// Application entry point. A second instance exists only to forward the
    /// requested path to the running instance; doing that before WPF starts
    /// skips the ~400 ms of PresentationFramework/XAML initialization every
    /// preview invocation used to pay. When the forwarder cannot reach a
    /// running instance, fall back to the normal WPF startup.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        // v3.2.0: register the assembly-resolution fallback before anything
        // touches App (or any third-party assembly). The release package keeps
        // the root clean by moving every non-entry DLL into lib\; resolution
        // falls back to searching the whole app directory recursively.
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            // Ignore the resource fails
            // e.g. "QuickLookNext.resources, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
            if (e.Name.Contains(".resources,"))
            {
                return null;
            }

            try
            {
                // Manually resolve the assembly fails
                // https://github.com/QL-Win/QuickLook/issues/1618
                // e.g. "System.Memory, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
                if (e.Name.Split(',').FirstOrDefault() is string assemblyName)
                {
                    if (AssemblyIndex.Value.TryGetValue(assemblyName, out var indexedPath))
                    {
                        return Assembly.LoadFrom(indexedPath);
                    }

                    // Fallback for files added after startup (rare): scan once.
                    foreach (var libPath in FetchFiles(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll"))
                    {
                        return Assembly.LoadFrom(libPath);
                    }
                }
            }
            catch
            {
                // There is no way to resolve it
            }

            return null;

            static IEnumerable<string> FetchFiles(string rootPath, string targetFileName)
            {
                foreach (var file in Directory.GetFiles(rootPath, "*" + Path.GetExtension(targetFileName), SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileName(file), targetFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return file;
                    }
                }
            }
        };

        if (StartupForwarder.TryForwardToRunningInstance())
            return;

        App.Main();
    }
}
