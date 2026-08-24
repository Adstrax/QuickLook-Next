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

using Newtonsoft.Json;
using QuickLook.Common.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace QuickLookNext.Helpers;

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
                var json = DownloadJson("https://api.github.com/repos/Adstrax/QuickLook-Next/releases/latest");

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

                if (!silent)
                {
                    // v3.0.4: user-initiated check -> download and install the
                    // release package automatically instead of opening GitHub.
                    Application.Current.Dispatcher.Invoke(() =>
                        TrayIconManager.ShowNotification(string.Empty,
                            string.Format(
                                TranslationHelper.Get("Update_AutoDownloading",
                                    failsafe: "发现新版本 {0}，正在自动下载并更新..."),
                                nVersion),
                            timeout: 20000));

                    if (TryAutoUpdate(json))
                    {
                        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                        return;
                    }

                    // Auto-update unavailable (read-only folder / no package):
                    // fall back to opening the download page.
                    Application.Current.Dispatcher.Invoke(() =>
                        TrayIconManager.ShowNotification(string.Empty,
                            TranslationHelper.Get("Update_AutoUpdateFailed",
                                failsafe: "自动更新失败，点击打开下载页面"),
                            timeout: 20000,
                            clickEvent: OpenReleasesPage));
                    return;
                }

                // Background check: only notify; clicking the notification
                // starts the automatic update.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TrayIconManager.ShowNotification(string.Empty,
                        string.Format(TranslationHelper.Get("Update_Found"), nVersion),
                        timeout: 20000,
                        clickEvent: () => _ = Task.Run(() =>
                        {
                            if (TryAutoUpdate(json))
                            {
                                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                            }
                            else
                            {
                                OpenReleasesPage();
                            }
                        }));
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

    /// <summary>
    /// v3.0.4: downloads the release package, stages it next to the app and
    /// hands the actual file replacement to a hidden updater script, so the
    /// running process can exit first and the updater can relaunch the app.
    /// Returns false (without shutting down) when auto-update is impossible.
    /// </summary>
    private static bool TryAutoUpdate(dynamic release)
    {
        try
        {
            var tag = (string)release["tag_name"];

            string downloadUrl = null;
            foreach (var asset in release["assets"])
            {
                var name = (string)asset["name"];
                if (string.IsNullOrEmpty(name) ||
                    !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    !name.StartsWith("QuickLook-Next-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                downloadUrl = (string)asset["browser_download_url"];
                break;
            }

            if (string.IsNullOrEmpty(downloadUrl))
                return false;

            var appDir = App.AppPath;
            if (string.IsNullOrEmpty(appDir) || !IsWritable(appDir))
                return false;

            var workDir = Path.Combine(Path.GetTempPath(), "QuickLookNext.Update");
            Directory.CreateDirectory(workDir);

            var zipPath = Path.Combine(workDir, $"QuickLook-Next-{tag}.zip");
            var extractDir = Path.Combine(workDir, "new");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, recursive: true);

            using (var download = new HttpClient(new HttpClientHandler { UseDefaultCredentials = true }))
            {
                download.Timeout = TimeSpan.FromMinutes(5);
                download.DefaultRequestHeaders.Add("User-Agent", "curl/8.20.0");
                using var file = File.Create(zipPath);
                using var stream = download.GetStreamAsync(downloadUrl).GetAwaiter().GetResult();
                stream.CopyTo(file);
            }

            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Sanity check: the package must contain the app entry point.
            if (!File.Exists(Path.Combine(extractDir, "QuickLook-Next.exe")))
                return false;

            var batPath = Path.Combine(workDir, "update.cmd");
            File.WriteAllText(batPath, BuildUpdateScript(appDir, extractDir, workDir));

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"")
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            return true;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Auto update failed: {e}");
            return false;
        }
    }

    private static string BuildUpdateScript(string appDir, string extractDir, string workDir)
    {
        return $"""
            @echo off
            :wait
            tasklist /FI "IMAGENAME eq QuickLook-Next.exe" | find /I "QuickLook-Next.exe" >nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak >nul
                goto wait
            )
            for %%f in ("{appDir}\*") do del /Q "%%f"
            for /d %%d in ("{appDir}\*") do if /I not "%%~nxd"=="UserData" rd /S /Q "%%d"
            xcopy /E /Y /Q "{extractDir}\*" "{appDir}\"
            start "" "{appDir}\QuickLook-Next.exe"
            del "%~f0"
            rd /S /Q "{workDir}" 2>nul
            """;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".ql-update-probe");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void OpenReleasesPage()
    {
        try
        {
            // v1.3.9: shell URIs need UseShellExecute on .NET Core.
            Process.Start(new ProcessStartInfo("https://github.com/Adstrax/QuickLook-Next/releases/latest")
            {
                UseShellExecute = true,
            });
        }
        catch (Win32Exception)
        {
            // No default browser; ignore.
        }
    }

    /// <summary>
    /// Test hook for the auto-update pipeline: feeds a (possibly fake) release
    /// object into the same download/install path used by CheckForUpdates.
    /// </summary>
    internal static bool RunAutoUpdate(dynamic release) => TryAutoUpdate(release);

    private static void CollectAndShowReleaseNotes()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var json = DownloadJson("https://api.github.com/repos/Adstrax/QuickLook-Next/releases");

                var notes = "# A new version of QuickLookNext is available!\r\n";

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
