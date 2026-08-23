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

using QuickLook.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickLook;

/// <summary>
/// v1.3.11: plugin management panel. Lists both built-in and user-installed
/// plugins, and lets the user uninstall plugins that live in the user plugin
/// folder (built-in plugins ship with the app and are marked as such).
/// </summary>
public partial class PluginManagerWindow : Window
{
    private readonly bool _isDark;
    private readonly List<PluginEntry> _entries = [];

    public PluginManagerWindow()
    {
        InitializeComponent();

        _isDark = TrayIconManager.IsDarkTheme();
        ApplyTheme();

        Title = Tr("PM_Title", "Manage Plugins");
        btnOpenFolder.Content = Tr("PM_OpenFolder", "Open Plugin Folder");
        btnRefresh.Content = Tr("PM_Refresh", "Refresh");
        btnClose.Content = Tr("PM_Close", "Close");

        RefreshList();
    }

    /// <summary>
    /// Opens the manager, reusing the existing window when one is already open.
    /// </summary>
    internal static void ShowWindow()
    {
        var existing = Application.Current.Windows
            .OfType<PluginManagerWindow>()
            .FirstOrDefault(w => w.IsVisible);
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        new PluginManagerWindow().Show();
    }

    /// <summary>
    /// Test hook: dump the currently listed plugins (name + user marker) so
    /// the smoke test can assert the panel enumerates installed plugins.
    /// </summary>
    internal string DiagnosePlugins()
    {
        return string.Join("|", _entries.Select(e => e.Name + (e.IsUserPlugin ? "*" : string.Empty)));
    }

    private void RefreshList()
    {
        _entries.Clear();
        _entries.AddRange(PluginManager.GetInstance().EnumerateInstalledPlugins());

        pluginList.Items.Clear();
        foreach (var entry in _entries)
            pluginList.Items.Add(BuildRow(entry));

        var userCount = _entries.Count(e => e.IsUserPlugin);
        var builtInCount = _entries.Count - userCount;
        headerText.Text = string.Format(
            Tr("PM_Header", "Installed Plugins ({0} user, {1} built-in)"),
            userCount, builtInCount);

        statusText.Text = userCount == 0
            ? Tr("PM_None", "No user-installed plugins yet. Preview a .qlplugin file to install one.")
            : string.Empty;
    }

    private Border BuildRow(PluginEntry entry)
    {
        var name = new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Resources["TextBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };

        var version = new TextBlock
        {
            Text = entry.Version,
            Foreground = (Brush)Resources["SecondaryTextBrush"],
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
        namePanel.Children.Add(name);
        namePanel.Children.Add(version);

        var description = new TextBlock
        {
            Text = entry.Description,
            Foreground = (Brush)Resources["SecondaryTextBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 16, 0),
            MaxWidth = 280,
        };

        var badge = new Border
        {
            Background = (Brush)Resources["BadgeBrush"],
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = entry.IsUserPlugin
                    ? Tr("PM_UserPlugin", "User")
                    : Tr("PM_BuiltIn", "Built-in"),
                Foreground = (Brush)Resources["BadgeTextBrush"],
                FontSize = 11,
            },
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(namePanel, 0);
        grid.Children.Add(namePanel);
        Grid.SetColumn(description, 1);
        grid.Children.Add(description);
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        if (entry.IsUserPlugin)
        {
            var uninstall = new Button
            {
                Content = Tr("PM_Uninstall", "Uninstall"),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = (Brush)Resources["DangerBrush"],
                Style = (Style)Resources["PanelButtonStyle"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            uninstall.Click += (_, _) => Uninstall(entry, uninstall);

            Grid.SetColumn(uninstall, 3);
            grid.Children.Add(uninstall);
        }

        var row = new Border
        {
            Child = grid,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            ToolTip = entry.Folder,
        };
        row.MouseEnter += (_, _) => row.Background = (Brush)Resources["RowHoverBrush"];
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

        return row;
    }

    private void Uninstall(PluginEntry entry, Button button)
    {
        var message = string.Format(
            Tr("PM_ConfirmUninstall", "Uninstall plugin \"{0}\"? Files will be removed from your user plugins folder."),
            entry.Name);
        if (MessageBox.Show(this, message, Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        button.IsEnabled = false;
        if (PluginManager.GetInstance().UninstallUserPlugin(entry, out var error, out var restartRequired))
        {
            statusText.Text = restartRequired
                ? Tr("PM_RestartRequired", "Files are locked; the plugin will be fully removed after a restart.")
                : Tr("PM_Uninstalled", "Uninstalled. Restart to fully release the loaded files.");
            RefreshList();
        }
        else
        {
            button.IsEnabled = true;
            statusText.Text = error;
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(App.UserPluginPath);
            Process.Start("explorer.exe", App.UserPluginPath);
        }
        catch (Exception ex)
        {
            statusText.Text = ex.Message;
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshList();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyTheme()
    {
        if (!_isDark)
            return;

        SetBrush("WindowBgBrush", "#FF20242A");
        SetBrush("TextBrush", "#FFF5F5F5");
        SetBrush("SecondaryTextBrush", "#FF9E9E9E");
        SetBrush("RowHoverBrush", "#14FFFFFF");
        SetBrush("SeparatorBrush", "#14FFFFFF");
        SetBrush("BadgeBrush", "#1860CDFF");
        SetBrush("BadgeTextBrush", "#FF60CDFF");
        SetBrush("ButtonBgBrush", "#14FFFFFF");
        SetBrush("ButtonHoverBrush", "#24FFFFFF");
        SetBrush("DangerBrush", "#FFFF7B72");
    }

    private void SetBrush(string key, string hex)
    {
        // Brushes declared in XAML resources are frozen and cannot be
        // mutated, so swap in a new unfrozen brush for the dark theme.
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private static string Tr(string key, string failsafe)
    {
        return TranslationHelper.Get(key, failsafe: failsafe);
    }
}
