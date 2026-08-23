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

using QuickLook.Common.ExtensionMethods;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using QuickLook.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Wpf.Ui.Violeta.Controls;
using static QuickLook.Common.NativeMethods.Dwmapi;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace QuickLook;

public partial class ViewerWindow : Window
{
    private Size _customWindowSize = Size.Empty;
    private bool _ignoreNextWindowSizeChange;
    private string _path = string.Empty;
    private FileSystemWatcher _autoReloadWatcher;
    private readonly bool _autoReload;
    private int _lastCursorX;
    private int _lastCursorY;
    private bool _hasLastCursor;
    private bool _warmShown;
    // v1.3.1: the window is created as a layered window when the user's
    // backdrop is an acrylic material on Win11, so the WCA acrylic renders
    // from the very first frame (see ShouldUseLayeredAcrylic).
    private readonly bool _layeredAcrylic;

    internal ViewerWindow()
    {
        // v1.3.1: Win11's DWM SystembackdropType.Acrylic renders a solid tint
        // on inactive windows and the preview window never activates
        // (ShowActivated=false), so the chosen acrylic only appeared after a
        // click. A layered window + WCA acrylic (the tray-menu recipe) blurs
        // regardless of the activation state.
        _layeredAcrylic = ShouldUseLayeredAcrylic();
        if (_layeredAcrylic)
        {
            // WPF rule: layered windows must not use a native window style.
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
        }

        // this object should be initialized before loading UI components, because many of which are binding to it.
        ContextObject = new ContextObject() { Source = this };

        ContextObject.PropertyChanged += ContextObject_PropertyChanged;

        InitializeComponent();

        // v1.2.1: start with the user's saved light/dark choice (None = follow system).
        ContextObject.Theme = (Themes)SettingHelper.Get(
            "LastTheme", (int)Themes.None, "QuickLook");

        _autoReload = SettingHelper.Get("AutoReload", false);

        Icon = (App.IsWin10 ? Properties.Resources.app_white_png : Properties.Resources.app_png).ToBitmapSource();

        FontFamily = new FontFamily(TranslationHelper.Get("UI_FontFamily", failsafe: "Segoe UI"));

        SizeChanged += SaveWindowSizeOnSizeChanged;

        StateChanged += (_, _) => _ignoreNextWindowSizeChange = true;

        windowFrameContainer.PreviewMouseMove += ShowWindowCaptionContainer;

        Topmost = SettingHelper.Get("Topmost", false);
        buttonTop.Tag = Topmost ? "Top" : "Auto";
        buttonTheme.Click += ToggleTheme;

        ShowInTaskbar = SettingHelper.Get("ShowInTaskbar", false);

        Deactivated += (_, _) =>
        {
            if (!SettingHelper.Get("CloseOnLostFocus", false))
                return;
            if (Pinned)
                return;
            // Defer close to ContextIdle so pending Render/Input operations
            // (e.g. MoveWindow, BringToFront) complete before the window is closed.
            Dispatcher.BeginInvoke(() =>
            {
                if (IsVisible && !Pinned)
                    Close();
            }, DispatcherPriority.ContextIdle);
        };

        buttonTop.Click += (_, _) =>
        {
            Topmost = !Topmost;
            SettingHelper.Set("Topmost", Topmost);
            buttonTop.Tag = Topmost ? "Top" : "Auto";
        };

        buttonPin.Click += (_, _) =>
        {
            if (SettingHelper.Get("CloseOnLostFocus", false))
            {
                Pinned = !Pinned;
                ViewWindowManager.GetInstance().ForgetCurrentWindow();
                return;
            }

            if (Pinned)
            {
                Toast.Information(TranslationHelper.Get("InfoPanel_CantPreventClosing"));
                return;
            }

            ViewWindowManager.GetInstance().ForgetCurrentWindow();
        };

        buttonCloseWindow.Click += (_, _) =>
        {
            Close();
        };

        buttonOpen.Click += (_, _) =>
        {
            if (Pinned)
                RunAndClose();
            else
                ViewWindowManager.GetInstance().RunAndClosePreview();
        };

        buttonReload.Click += (_, _) =>
        {
            ViewWindowManager.GetInstance().ReloadPreview();
        };

        buttonWindowStatus.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        buttonShare.Click += (_, _) => ShareHelper.Share(_path, this);
        buttonOpenWith.Click += (_, _) => ShareHelper.Share(_path, this, true);

        buttonReload.Visibility = SettingHelper.Get("ShowReload", false) ? Visibility.Visible : Visibility.Collapsed;

        buttonMore.Click += (_, _) => ToggleMoreMenu();

        // Set UI translations
        buttonTop.ToolTip = TranslationHelper.Get("MW_StayTop");
        buttonPin.ToolTip = TranslationHelper.Get("MW_PreventClosing");
        buttonOpenWith.ToolTip = TranslationHelper.Get("MW_OpenWithMenu");
        buttonShare.ToolTip = TranslationHelper.Get("MW_Share");
        buttonReload.ToolTip = TranslationHelper.Get("MW_Reload", failsafe: "Reload");
        buttonMore.ToolTip = TranslationHelper.Get("MW_More", failsafe: "More");
    }

    /// <summary>
    /// v1.2.36: pay the one-time first-Show cost (HWND creation, layout, DWM
    /// backdrop) during startup idle, off-screen. The window stays "visible"
    /// off-screen; the first real preview re-positions it as a new window so
    /// it is centered correctly and never inherits the warm-up size.
    /// </summary>
    internal void WarmUp()
    {
        if (IsVisible)
            return;

        _warmShown = true;
        // The warm-up Show fires SizeChanged; do not persist that default
        // size as the user's custom window size.
        _ignoreNextWindowSizeChange = true;

        var restoreActivated = ShowActivated;
        ShowActivated = false;
        Left = -32000;
        Top = -32000;
        Show();
        ShowActivated = restoreActivated;
    }

    public new void Close()
    {
        // Workaround to prevent DPI jump animation when closing window in .NET Framework 4.6.2
        // Safe to remove this line if QuickLook no longer targets .NET Framework 4.6.2
        Hide();

        base.Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowHelper.RemoveWindowControls(this);

        ApplyWindowBackgroundEffects();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // v1.1.4: baseline the cursor position. WPF synthesizes MouseMove events
        // when the window opens or its content is replaced (preview switching)
        // while the cursor is stationary. We only treat a MouseMove as real when
        // the cursor screen position actually changes.
        if (GetCursorPos(out var pt))
        {
            _lastCursorX = pt.X;
            _lastCursorY = pt.Y;
            _hasLastCursor = true;
        }

        ApplyWindowBackgroundEffects();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (_isFullscreen)
        {
            Dispatcher.BeginInvoke(new Action(() => this.BringToFront(Topmost)), DispatcherPriority.Render);
        }
    }

    private void ApplyWindowBackgroundEffects()
    {
        var useTransparency = SettingHelper.Get("UseTransparency", true)
            && SystemParameters.IsGlassEnabled
            && !App.IsGPUInBlacklist;
        var backdrop = GetBackdropOption();

        if (useTransparency)
        {
            ApplyBackdrop(backdrop);
        }
        else
        {
            SetGlassFrameThickness(1d);
            WindowHelper.DisableDwmBlur(this); // Fix white flash in dark mode
            Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
        }

        var customColor = SettingHelper.Get("WindowBackgroundColor", string.Empty, "QuickLook");
        if (!string.IsNullOrEmpty(customColor))
        {
            try
            {
                Background = (Brush)new BrushConverter().ConvertFromString(customColor);
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
            {
                // Ignore invalid color
            }
        }
    }

    private void SetGlassFrameThickness(double thickness)
    {
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome != null)
            chrome.GlassFrameThickness = new Thickness(thickness);
    }

    private void ApplyBackdrop(SystembackdropType backdrop)
    {
        switch (backdrop)
        {
            case SystembackdropType.None:
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.DisableDwmBlur(this); // Fix white flash in dark mode
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }
                break;

            case SystembackdropType.Auto:
            case SystembackdropType.Mica:
            default:
                if (App.IsWin11)
                {
                    if (Environment.OSVersion.Version >= new Version(10, 0, 22523))
                    {
                        SetGlassFrameThickness(1d);
                        WindowHelper.EnableBackdropMicaBlur(this, CurrentTheme == Themes.Dark);
                        Background = Brushes.Transparent;
                    }
                    else
                    {
                        SetGlassFrameThickness(1d);
                        WindowHelper.EnableMicaBlur(this, CurrentTheme == Themes.Dark);
                        Background = Brushes.Transparent;
                    }
                }
                else if (App.IsWin10)
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableBlur(this);
                    Background = (Brush)FindResource("MainWindowBackground");
                }
                else
                {
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }

                break;

            case SystembackdropType.Acrylic:
                if (_layeredAcrylic)
                {
                    // v1.3.1: layered window - WCA acrylic works while the
                    // window is inactive, so the frosted glass shows on open.
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylicTintColor(), CurrentTheme == Themes.Dark, 0.4d);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin11 && Environment.OSVersion.Version >= new Version(10, 0, 22523))
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableBackdropAcrylicBlur(this, CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin10)
                {
                    SetGlassFrameThickness(0d);
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylicTintColor(), CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else
                {
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }

                break;

            case SystembackdropType.Acrylic10:
                if (App.IsWin10 || App.IsWin11)
                {
                    SetGlassFrameThickness(0d);
                    WindowHelper.DisableDwmBlur(this); // Restore rounded corners on Windows 11
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylic10TintColor(), CurrentTheme == Themes.Dark, GetAcrylic10TintOpacity());
                    Background = GetAcrylic10TintLuminosityOpacityBackground(CurrentTheme == Themes.Dark);
                }
                else
                {
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }

                break;

            case SystembackdropType.Acrylic11:
                if (_layeredAcrylic)
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylicTintColor(), CurrentTheme == Themes.Dark, 0.4d);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin11 && Environment.OSVersion.Version >= new Version(10, 0, 22523))
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableBackdropAcrylicBlur(this, CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin11)
                {
                    SetGlassFrameThickness(0d);
                    WindowHelper.DisableDwmBlur(this); // Restore rounded corners on Windows 11
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylicTintColor(), CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin10)
                {
                    SetGlassFrameThickness(0d);
                    WindowHelper.EnableAcrylicBlur(this, GetAcrylicTintColor(), CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else
                {
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }

                break;

            case SystembackdropType.Tabbed:
                if (App.IsWin11 && Environment.OSVersion.Version >= new Version(10, 0, 22523))
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableBackdropTabbedBlur(this, CurrentTheme == Themes.Dark);
                    Background = Brushes.Transparent;
                }
                else if (App.IsWin10)
                {
                    SetGlassFrameThickness(1d);
                    WindowHelper.EnableBlur(this);
                    Background = (Brush)FindResource("MainWindowBackground");
                }
                else
                {
                    Background = (Brush)FindResource("MainWindowBackgroundNoTransparent");
                }

                break;
        }
    }

    private Color GetAcrylicTintColor()
    {
        var customColor = SettingHelper.Get("WindowBackgroundColor", string.Empty, "QuickLook");

        if (!string.IsNullOrEmpty(customColor))
        {
            try
            {
                return ((SolidColorBrush)new BrushConverter().ConvertFromString(customColor)).Color;
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
            {
                // Ignore invalid color
            }
        }

        return ((SolidColorBrush)FindResource("MainWindowBackground")).Color;
    }

    private Color GetAcrylic10TintColor()
    {
        var customColor = SettingHelper.Get("WindowBackgroundColor", string.Empty, "QuickLook");

        if (!string.IsNullOrEmpty(customColor))
        {
            try
            {
                return ((SolidColorBrush)new BrushConverter().ConvertFromString(customColor)).Color;
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
            {
                // Ignore invalid color
            }
        }

        return CurrentTheme == Themes.Dark
            ? Color.FromRgb(0x17, 0x17, 0x17)
            : Color.FromRgb(0xF2, 0xF2, 0xF2);
    }

    private static double GetAcrylic10TintOpacity()
    {
        var acrylicTintOpacity = 0.7d;
        return acrylicTintOpacity;
    }

    private static Brush GetAcrylic10TintLuminosityOpacityBackground(bool isDarkTheme)
    {
        var acrylicTintLuminosityOpacity = 0.44d;
        var t = acrylicTintLuminosityOpacity * (isDarkTheme ? 0.6d : 1.25d);
        var v = isDarkTheme ? (byte)0x22 : (byte)0xE1;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(t * 255d * 0.6d), v, v, v));
        brush.Freeze();
        return brush;
    }

    private static SystembackdropType GetBackdropOption()
    {
        // v1.2.10: default to Acrylic - the same frosted effect as the startup
        // notification popup. Mica/Tabbed remain available via the setting.
        var option = SettingHelper.Get("WindowBackdrop", nameof(SystembackdropType.Acrylic), "QuickLook")?.Trim();

        if (string.IsNullOrEmpty(option))
            return SystembackdropType.Auto;

        if (string.Equals(option, nameof(SystembackdropType.Acrylic), StringComparison.OrdinalIgnoreCase))
            return SystembackdropType.Acrylic;

        if (string.Equals(option, nameof(SystembackdropType.Tabbed), StringComparison.OrdinalIgnoreCase))
            return SystembackdropType.Tabbed;

        return Enum.TryParse(option, true, out SystembackdropType parsed)
            ? parsed
            : SystembackdropType.Auto;
    }

    /// <summary>
    /// v1.3.1: acrylic materials cannot render while the preview window is
    /// inactive through the DWM backdrop API (Win11 limitation). Creating the
    /// window as a layered window lets the WCA acrylic show immediately; Mica,
    /// Tabbed and the other backdrops render fine on a normal window and stay
    /// non-layered (hardware-accelerated).
    /// </summary>
    private static bool ShouldUseLayeredAcrylic()
    {
        if (!App.IsWin11)
            return false;

        return GetBackdropOption() switch
        {
            SystembackdropType.Acrylic or SystembackdropType.Acrylic10 or SystembackdropType.Acrylic11 => true,
            _ => false,
        };
    }

    private void SaveWindowSizeOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // first shown?
        if (e.PreviousSize == new Size(0, 0))
            return;
        // resize when switching preview?
        if (_ignoreNextWindowSizeChange)
        {
            _ignoreNextWindowSizeChange = false;
            return;
        }

        // by user?
        _customWindowSize = new Size(Width, Height);
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        var newTheme = CurrentTheme == Themes.Dark ? Themes.Light : Themes.Dark;

        // ContextObject.Theme triggers SwitchTheme (window + backdrop + theme state).
        ContextObject.Theme = newTheme;

        // Persist the choice so future previews start with it.
        SettingHelper.Set("LastTheme", (int)newTheme, "QuickLook");
        SettingHelper.Set("LastTheme", (int)newTheme, "QuickLook.Plugin.ImageViewer");

        // Re-render the current preview so web content (WebView2) picks up the
        // new PreferredColorScheme / theme.
        ViewWindowManager.GetInstance().ReloadPreview();
    }

    private void ShowWindowCaptionContainer(object sender, MouseEventArgs e)
    {
        if (!GetCursorPos(out var pt))
            return;

        if (!_hasLastCursor)
        {
            _hasLastCursor = true;
            _lastCursorX = pt.X;
            _lastCursorY = pt.Y;
            return;
        }

        // Synthetic MouseMove (window opened/moved under a stationary cursor)
        // carries the same cursor position; ignore it.
        if (pt.X == _lastCursorX && pt.Y == _lastCursorY)
            return;

        _lastCursorX = pt.X;
        _lastCursorY = pt.Y;

        var show = (Storyboard)windowCaptionContainer.FindResource("ShowCaptionContainerStoryboard");

        if (windowCaptionContainer.Opacity == 0 || windowCaptionContainer.Opacity == 1)
            show.Begin();
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void AutoHideCaptionContainer(object sender, EventArgs e)
    {
        if (!ContextObject.TitlebarAutoHide)
            return;

        var hide = (Storyboard)windowCaptionContainer.FindResource("HideCaptionContainerStoryboard");

        hide.Begin();
    }
}
