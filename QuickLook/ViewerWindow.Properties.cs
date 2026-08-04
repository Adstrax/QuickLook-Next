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

using QuickLook.Common.Annotations;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Violeta.Appearance;

namespace QuickLook;

public partial class ViewerWindow : INotifyPropertyChanged
{
    private readonly ResourceDictionary _darkDict = new()
    {
        Source = new Uri("pack://application:,,,/QuickLook.Common;component/Styles/MainWindowStyles.Dark.xaml")
    };

    private bool _canOldPluginResize;
    private bool _pinned;
    private bool _isFullscreen;
    private WindowState _preFullscreenWindowState;
    private WindowStyle _preFullscreenWindowStyle;
    private ResizeMode _preFullscreenResizeMode;
    private Rect _preFullscreenBounds;
    private double _preFullscreenCaptionHeight;
    private Thickness _preFullscreenResizeBorderThickness;

    public bool Pinned
    {
        get => _pinned;
        set
        {
            _pinned = value;
            buttonPin.Tag = value ? "Pin" : "Auto";
            OnPropertyChanged();
        }
    }

    public IViewer Plugin { get; private set; }

    public ContextObject ContextObject { get; private set; }

    public Themes CurrentTheme { get; private set; }

    // Hidden test hook state (/test-timing): the path that was busy, so the
    // timing entry is only written on a real busy -> ready transition (the
    // ContextObject.Reset() calls also toggle IsBusy and must be ignored).
    private string _timingBusyPath;

    // The previous preview's content, kept on screen while the next preview
    // loads so switching does not flash an empty gray window (v1.2.13).
    private object _staleViewerContent;

    // v1.2.14: the plugin whose content is currently displayed. Its Cleanup is
    // deferred until the next preview's content takes over, so the old content
    // stays fully rendered instead of being emptied mid-switch.
    private IViewer _pendingPluginCleanup;
    private IViewer _contentPlugin;

    public ICommand CloseCommand { get; private set; }

    public event PropertyChangedEventHandler PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ContextObject_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ContextObject.IsBusy):
                if (ContextObject.IsBusy)
                {
                    _timingBusyPath = _path;
                    break;
                }

                // v1.2.14: apply content that was held back until the first
                // frame decoded (image plugin) - the swap is atomic, so the old
                // content stays on screen until the new one is ready.
                if (ContextObject.PendingViewerContent != null &&
                    !ReferenceEquals(ContextObject.ViewerContent, ContextObject.PendingViewerContent))
                {
                    ContextObject.ViewerContent = ContextObject.PendingViewerContent;
                    ContextObject.PendingViewerContent = null;
                }

                // Hidden test hook: record when content actually becomes ready
                // (spinner dismissed) so benches can measure preview latency.
                if (App.IsTimingEnabled && IsVisible && _timingBusyPath != null
                    && _timingBusyPath == _path && !string.IsNullOrEmpty(_path))
                {
                    try
                    {
                        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ql-smoke");
                        System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.AppendAllText(
                            System.IO.Path.Combine(dir, "timing.txt"),
                            $"{DateTime.UtcNow:o}|{_path}{Environment.NewLine}");
                    }
                    catch
                    {
                        // The hook is for measurement only; never break previews.
                    }

                    _timingBusyPath = null;
                }
                break;

            case nameof(ContextObject.ViewerContent):
                // v1.2.14: when real content takes over (the stale restore and
                // the Reset null are ignored here), dispose the old plugin and
                // track the new one as the current content.
                if (ContextObject.ViewerContent == null ||
                    ReferenceEquals(ContextObject.ViewerContent, _staleViewerContent))
                {
                    break;
                }

                var oldPlugin = _pendingPluginCleanup;
                _pendingPluginCleanup = null;
                _staleViewerContent = null;
                _contentPlugin = Plugin;

                if (oldPlugin != null)
                {
                    try
                    {
                        oldPlugin.Cleanup();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
                break;

            case nameof(ContextObject.Theme):
                SwitchTheme(ContextObject.Theme);
                break;

            case nameof(ContextObject.Title):
                if (!string.IsNullOrWhiteSpace(ContextObject.Title))
                {
                    Dispatcher?.Invoke(() =>
                    {
                        // We can not update the Title when ShowInTaskbar is false
                        // https://github.com/QL-Win/QuickLook/issues/1628
                        Title = $"QuickLook - {ContextObject.Title}";
                    });
                }
                break;

            default:
                break;
        }
    }

    public void SwitchTheme(Themes theme)
    {
        var isDark = false;

        switch (theme)
        {
            case Themes.None:
                isDark = OSThemeHelper.AppsUseDarkTheme();
                break;

            case Themes.Dark:
            case Themes.Light:
                isDark = theme == Themes.Dark;
                break;
        }

        if (isDark)
        {
            CurrentTheme = Themes.Dark;
            AppThemeState.IsDark = true;

            // Update theme for QuickLook controls
            if (!Resources.MergedDictionaries.Contains(_darkDict))
                Resources.MergedDictionaries.Add(_darkDict);

            // Update theme for WPF-UI controls
            ThemeManager.Apply(ApplicationTheme.Dark);
        }
        else
        {
            CurrentTheme = Themes.Light;
            AppThemeState.IsDark = false;

            // Update theme for QuickLook controls
            if (Resources.MergedDictionaries.Contains(_darkDict))
                Resources.MergedDictionaries.Remove(_darkDict);

            // Update theme for WPF-UI controls
            ThemeManager.Apply(ApplicationTheme.Light);
        }

        // Theme button: sun = switch to light, moon = switch to dark.
        buttonTheme.Content = isDark ? "\uE706" : "\uE708";

        if (IsLoaded)
            ApplyWindowBackgroundEffects();
    }
}
