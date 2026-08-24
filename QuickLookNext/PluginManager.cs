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

using QuickLook.Common.ExtensionMethods;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.InfoPanel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using UnblockZoneIdentifier;

namespace QuickLookNext;

public sealed class PluginManager
{
    private static PluginManager _instance;
    private readonly Task _loadTask;

    private PluginManager()
    {
        DefaultPlugin = new Plugin();

        // Loading plugin assemblies does reflection and file I/O that can add
        // hundreds of milliseconds to startup. Run it on a background thread so
        // the tray icon, keyboard hook and pipe server come up first; previews
        // wait for the load task if they arrive before it completes.
        _loadTask = Task.Run(LoadPluginsCore);
    }

    internal IViewer DefaultPlugin { get; }

    internal List<IViewer> LoadedPlugins { get; private set; } = [];

    internal void EnsureLoaded()
    {
        _loadTask.GetAwaiter().GetResult();
    }

    internal static PluginManager GetInstance()
    {
        return _instance ??= new PluginManager();
    }

    internal IViewer FindMatch(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var instance = GetInstance();
        instance.EnsureLoaded();

        var matched = instance.LoadedPlugins.FirstOrDefault(plugin =>
            {
                var can = false;
                try
                {
#if DEBUG
                    var timer = new Stopwatch();
                    timer.Start();

                    can = plugin.CanHandle(path);

                    timer.Stop();
                    Debug.WriteLine($"{plugin.GetType()}: {can}, {timer.ElapsedMilliseconds}ms");
#else
                    can = plugin.CanHandle(path);
#endif
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{plugin.GetType()}: CanHandle failed: {ex}");
                }

                return can;
            });

        return (matched ?? DefaultPlugin).GetType().CreateInstance<IViewer>();
    }

    private void LoadPluginsCore()
    {
        try
        {
            // v1.3.11: finish uninstalls that were parked because a plugin
            // file was locked while the previous instance was still running.
            CleanupPendingUninstalls(App.UserPluginPath);

            var loaded = new List<IViewer>();
            LoadPlugins(App.UserPluginPath, loaded);
            LoadPlugins(Path.Combine(App.AppPath, @"QuickLook.Plugin\"), loaded);
            App.RecordStartupPhase("plugins-assemblies-loaded");
            loaded.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));

            foreach (var plugin in loaded)
            {
                try
                {
                    plugin.Init();
                }
                catch (Exception e)
                {
                    ProcessHelper.WriteLog(e.ToString());
                }
            }

            LoadedPlugins = loaded;
            App.RecordStartupPhase("plugins-inited");
        }
        catch (Exception e)
        {
            // Never let a plugin-load failure take down startup; FindMatch will
            // simply fall back to the default InfoPanel plugin.
            ProcessHelper.WriteLog(e.ToString());
        }
    }

    private void LoadPlugins(string folder, List<IViewer> loaded)
    {
        if (!Directory.Exists(folder))
            return;

        var failedPlugins = new List<(string Plugin, Exception Error)>();

        foreach (var lib in Directory.GetFiles(folder, "QuickLook.Plugin.*.dll", SearchOption.AllDirectories))
        {
            try
            {
                foreach (var t in Assembly.LoadFrom(lib).GetExportedTypes())
                {
                    if (t.IsInterface || t.IsAbstract || !typeof(IViewer).IsAssignableFrom(t))
                        continue;

                    loaded.Add(t.CreateInstance<IViewer>());
                }
            }
            // 0x80131515: ERROR_ASSEMBLY_FILE_BLOCKED - Windows blocked the assembly due to security policy
            catch (FileLoadException ex) when (ex.HResult == unchecked((int)0x80131515) && SettingHelper.IsPortableVersion())
            {
                // The unblock-and-restart flow shows dialogs, so run it on the
                // UI thread. BeginInvoke (not Invoke) keeps a preview that is
                // waiting on the load task from deadlocking.
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!HandleSecurityBlockedException())
                        ProcessHelper.WriteLog($"Failed to load blocked plugin {Path.GetFileName(lib)}");
                }));
            }
            catch (Exception ex)
            {
                // Log the error
                ProcessHelper.WriteLog($"Failed to load plugin {Path.GetFileName(lib)}: {ex}");
                failedPlugins.Add((Path.GetFileName(lib), ex));
            }
        }

        if (failedPlugins.Any())
        {
            var message = "The following plugins failed to load:\n\n" +
                string.Join("\n", failedPlugins.Select(f => $"• {f.Plugin}"));

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // The warning needs a visible owner window: during an
                    // auto-start / tray-only startup no window has been shown
                    // yet, and setting an unshown Owner crashes the process.
                    var owner = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsLoaded && w.IsVisible);
                    if (owner is null)
                    {
                        ProcessHelper.WriteLog(
                            $"Some plugins failed to load (no window to show warning): {message}");
                        return;
                    }

                    MessageBox.Show(owner, message, "Some Plugins Failed to Load",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    ProcessHelper.WriteLog(ex.ToString());
                }
            }));
        }
    }

    /// <summary>
    /// Handles the case when Windows has blocked plugin files due to security policy.
    /// Attempts automatic unblock first, then shows manual instructions if that fails.
    /// </summary>
    /// <returns>
    /// <para>true if automatic unblock succeeded and app is restarting.</para>
    /// <para>false if manual intervention is needed and exception should be thrown.</para>
    /// </returns>
    private static bool HandleSecurityBlockedException()
    {
        var triedUnblock = SettingHelper.Get("TriedUnblock", false);
        if (!triedUnblock)
        {
            SettingHelper.Set("TriedUnblock", true);
            if (TryUnblockFilesAndRestart()) return true;
        }

        // Show manual unblock instructions if automatic unblock failed or was already attempted
        MessageBox.Show(
            """
            Windows has blocked the plugins.
            To fix this, please follow these steps:
            1. Right-click the downloaded QuickLookNext zip file and select 'Properties'
            2. At the bottom of the Properties window, check 'Unblock'
            3. Click 'Apply' and 'OK'
            4. Extract the zip file again
            QuickLookNext will now close. Please launch it from the unblocked folder.
            """,
            "Security Block Detected",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        return false;
    }

    /// <summary>
    /// Attempts to automatically unblock all files in the application directory using PowerShell's Unblock-File cmdlet.
    /// If successful, restarts the application to apply the changes.
    /// </summary>
    /// <returns>
    /// <para>true if the unblock command succeeded and application restart was initiated.</para>
    /// <para>false if the unblock command failed, in which case manual unblock instructions should be shown.</para>
    /// </returns>
    private static bool TryUnblockFilesAndRestart()
    {
        ProcessHelper.WriteLog("Attempting automatic unblock of plugins...");

        try
        {
            var rootDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!string.IsNullOrEmpty(rootDir) && Directory.Exists(rootDir))
            {
                foreach (var filePath in Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories))
                {
                    if (ZoneIdentifierManager.IsZoneBlocked(filePath))
                    {
                        _ = ZoneIdentifierManager.UnblockZone(filePath);
                    }
                }
            }

            MessageBox.Show(
                """
                QuickLookNext has detected that Windows blocked the plugins, and has attempted to unblock them.
                The application will now restart to check if the unblocking was successful.
                """,
                "Security Unblock Attempt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Restart the application using TrayIconManager
            TrayIconManager.GetInstance().Restart(forced: true);
            return true;
        }
        catch (Exception e)
        {
            ProcessHelper.WriteLog($"Failed to perform automatic unblock: {e}");
            return false;
        }
    }

    /// <summary>
    /// v1.3.11: enumerates plugins for the management panel - user-installed
    /// plugins first, then the built-in plugins that ship with the app.
    /// </summary>
    internal List<PluginEntry> EnumerateInstalledPlugins()
    {
        var list = new List<PluginEntry>();
        CollectPlugins(App.UserPluginPath, user: true, list);
        CollectPlugins(Path.Combine(App.AppPath, @"QuickLook.Plugin\"), user: false, list);
        return list;
    }

    /// <summary>
    /// v1.3.11: uninstalls a user plugin. Prefers deleting the folder right
    /// away; when a file is locked, the folder is parked with an
    /// ".uninstalled" suffix and removed on the next launch.
    /// </summary>
    internal bool UninstallUserPlugin(PluginEntry entry, out string error, out bool restartRequired)
    {
        error = null;
        restartRequired = false;

        try
        {
            Directory.Delete(entry.Folder, recursive: true);
            RemovePluginsUnder(entry.Folder);
            return true;
        }
        catch (Exception)
        {
            // The plugin assembly or a native dependency is locked. Park the
            // folder so startup can finish the removal; the panel stays
            // consistent because the loaded plugin is dropped from the list.
            try
            {
                var pending = entry.Folder.TrimEnd('\\', '/') + ".uninstalled";
                if (Directory.Exists(pending))
                    Directory.Delete(pending, recursive: true);
                Directory.Move(entry.Folder, pending);
                RemovePluginsUnder(entry.Folder);
                restartRequired = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// v1.3.11: removes loaded plugin instances whose assembly lives under
    /// the given folder so future previews no longer match them.
    /// </summary>
    internal void RemovePluginsUnder(string folder)
    {
        if (LoadedPlugins is null)
            return;

        var prefix = folder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        LoadedPlugins.RemoveAll(plugin =>
        {
            try
            {
                var location = plugin.GetType().Assembly.Location;
                return !string.IsNullOrEmpty(location) &&
                    location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });
    }

    private static void CollectPlugins(string root, bool user, List<PluginEntry> list)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var dir in Directory.GetDirectories(root))
        {
            var folderName = Path.GetFileName(dir);
            if (folderName.EndsWith(".uninstalled", StringComparison.OrdinalIgnoreCase))
                continue;

            // Prefer the DLL named after the plugin folder; a folder can also
            // contain dependency copies of other plugins (e.g. PDFViewer ships
            // an HtmlViewer copy), which must not be treated as its own DLL.
            var dll = Path.Combine(dir, folderName + ".dll");
            if (!File.Exists(dll))
            {
                dll = Directory.GetFiles(dir, "QuickLook.Plugin.*.dll", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(file => new FileInfo(file).Length)
                    .FirstOrDefault();
            }
            if (dll is null)
                continue;

            var (name, version, description) = ReadPluginMeta(dir, dll, folderName);
            list.Add(new PluginEntry(name, version, description, dir, user));
        }
    }

    private static (string Name, string Version, string Description) ReadPluginMeta(
        string dir, string dll, string folderName)
    {
        var config = Path.Combine(dir, "QuickLook.Plugin.Metadata.config");
        if (File.Exists(config))
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(config);
                var ns = doc.SelectSingleNode("/Metadata/Namespace")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(ns))
                {
                    return (
                        FriendlyName(ns),
                        doc.SelectSingleNode("/Metadata/Version")?.InnerText?.Trim() ?? string.Empty,
                        doc.SelectSingleNode("/Metadata/Description")?.InnerText?.Trim() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                ProcessHelper.WriteLog($"Failed to read plugin metadata {config}: {ex}");
            }
        }

        try
        {
            var assembly = Assembly.LoadFrom(dll);
            // Use the assembly identity, not AssemblyTitle: the Lite fork has
            // copy-paste AssemblyTitle bugs (MarkdownViewer declares ImageViewer).
            var title = assembly.GetName().Name ?? folderName;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? string.Empty;
            var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
                ?? string.Empty;
            return (FriendlyName(title), version, description);
        }
        catch (Exception ex)
        {
            ProcessHelper.WriteLog($"Failed to read plugin assembly {dll}: {ex}");
            return (FriendlyName(folderName), string.Empty, string.Empty);
        }
    }

    private static string FriendlyName(string namespaceOrTitle)
    {
        const string prefix = "QuickLook.Plugin.";
        var name = namespaceOrTitle?.Trim() ?? string.Empty;
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            name = name[prefix.Length..];
        return name.Replace('.', ' ');
    }

    private static void CleanupPendingUninstalls(string userPluginPath)
    {
        if (!Directory.Exists(userPluginPath))
            return;

        foreach (var dir in Directory.GetDirectories(userPluginPath, "*.uninstalled", SearchOption.TopDirectoryOnly))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                ProcessHelper.WriteLog($"Failed to finish uninstall of {dir}: {ex}");
            }
        }
    }
}

/// <summary>
/// v1.3.11: one row in the plugin management panel.
/// </summary>
internal sealed record PluginEntry(string Name, string Version, string Description, string Folder, bool IsUserPlugin);
