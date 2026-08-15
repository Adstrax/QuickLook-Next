// Copyright © 2017-2026 QL-Win Contributors
//
// This file is part of QuickLook program.
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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuickLook.Plugin.ImageViewer.Webview.Lottie;

internal static class LottieDetector
{
    public static bool IsVaildFile(string path)
    {
        try
        {
            // v1.2.35: never read + parse the whole file on the UI thread for
            // every .json preview. Lottie's top-level keys live in the first
            // bytes, so a bounded read is enough to classify and bounds the
            // parse cost for large non-Lottie JSON files.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            const int maxRead = 256 * 1024;
            byte[] buffer = new byte[maxRead];
            int size = fs.Read(buffer, 0, maxRead);
            var jsonString = Encoding.UTF8.GetString(buffer, 0, size);
            return IsVaildContent(jsonString);
        }
        catch
        {
            // If any exception occurs, assume it's not a valid Lottie file
        }

        return false;
    }

    public static bool IsVaildContent(string jsonString)
    {
        try
        {
            // No exception will be thrown here
            var jsonLottie = LottieParser.Parse<Dictionary<string, object>>(jsonString);

            if (jsonLottie != null
             && jsonLottie.ContainsKey("v")
             && jsonLottie.ContainsKey("fr")
             && jsonLottie.ContainsKey("ip")
             && jsonLottie.ContainsKey("op")
             && jsonLottie.ContainsKey("layers"))
            {
                return true;
            }
        }
        catch
        {
            // If any exception occurs, assume it's not a valid Lottie file
        }

        return false;
    }
}
