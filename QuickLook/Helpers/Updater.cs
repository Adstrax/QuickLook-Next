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

using Newtonsoft.Json;
using QuickLook.Common.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace QuickLook.Helpers;

internal class Updater
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.Add("User-Agent", "curl/8.20.0");
        return client;
    }

    // "silent" indicates whether this check was automatic/background.
    // When "silent" is true, do not open or invoke UI that shows
    // the full markdown release notes. Only show detailed release
    // notes when the check is user-initiated (silent == false).
    public static void CheckForUpdates(bool silent = false)
    {
        if (App.IsUWP)
        {
            if (!silent)
            {
                // v1.3.9: shell URIs need UseShellExecute on .NET Core.
                try
                {
                    Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?productid=9NV4BS3L1H4S")
                    {
                        UseShellExecute = true,
                    });
                }
                catch (Win32Exception)
                {
                    // Store not available; ignore.
                }
            }

            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var json = DownloadJson("https://api.github.com/repos/QL-Win/QuickLook/releases/latest");

                var nVersion = (string)json["tag_name"];

                if (new Version(nVersion) <= Assembly.GetExecutingAssembly().GetName().Version)
                {
                    if (!silent)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            TrayIconManager.ShowNotification(string.Empty,
                                TranslationHelper.Get("Update_NoUpdate")));
                    }
                    return;
                }

                // Only collect and show the detailed markdown release notes
                // when the update check is explicitly initiated by the user
                // (i.e. not a silent/automatic background check).
                if (!silent)
                    CollectAndShowReleaseNotes();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TrayIconManager.ShowNotification(string.Empty,
                        string.Format(TranslationHelper.Get("Update_Found"), nVersion),
                        timeout: 20000,
                        clickEvent: () =>
                        {
                            // v1.3.9: shell URIs need UseShellExecute on .NET Core.
                            try
                            {
                                Process.Start(new ProcessStartInfo("https://github.com/QL-Win/QuickLook/releases/latest")
                                {
                                    UseShellExecute = true,
                                });
                            }
                            catch (Win32Exception)
                            {
                                // No default browser; ignore.
                            }
                        });
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Application.Current.Dispatcher.Invoke(
                    () => TrayIconManager.ShowNotification(string.Empty,
                        string.Format(TranslationHelper.Get("Update_Error"), e.Message)));
            }
        });
    }

    private static void CollectAndShowReleaseNotes()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var json = DownloadJson("https://api.github.com/repos/QL-Win/QuickLook/releases");

                var notes = "# A new version of QuickLook is available!\r\n";

                var count = 0;
                foreach (var item in json)
                {
                    notes += $"## {item["name"]}\r\n\r\n";
                    notes += item["body"] + "\r\n\r\n";

                    if (count++ > 10)
                        break;
                }

                var changeLogPath = Path.GetTempFileName() + ".md";
                File.WriteAllText(changeLogPath, notes);

                PipeServerManager.PostMessage(PipeMessages.Invoke, changeLogPath);
                PipeServerManager.PostMessage(PipeMessages.Forget);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Application.Current.Dispatcher.Invoke(
                    () => TrayIconManager.ShowNotification(string.Empty,
                        string.Format(TranslationHelper.Get("Update_Error"), e.Message)));
            }
        });
    }

    private static dynamic DownloadJson(string url)
    {
        var json = Http.GetStringAsync(url).GetAwaiter().GetResult();
        return JsonConvert.DeserializeObject<dynamic>(json);
    }
}
