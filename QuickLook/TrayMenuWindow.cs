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
using QuickLook.Common.NativeMethods;
using QuickLook.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace QuickLook;

/// <summary>
/// A Mica-backed, self-drawn context menu used by the system tray icon.
/// The built-in TrayIconHost menu is a native Win32 popup which cannot render
/// Mica, so it is replaced by this borderless WPF window that reuses the same
/// backdrop pipeline as the preview window.
/// The window is intentionally non-activating (WS_EX_NOACTIVATE), so opening
/// or using the tray menu never steals focus from a live preview. A low-level
/// mouse hook dismisses the menu on outside clicks and a keyboard hook closes
/// it on Escape.
/// </summary>
internal sealed class TrayMenuWindow : Window
{
    private static TrayMenuWindow _current;

    private readonly bool _isDark;
    private readonly StackPanel _root;

    private readonly Brush _textBrush;
    private readonly Brush _disabledTextBrush;
    private readonly Brush _hoverBrush;
    private readonly Brush _separatorBrush;
    private readonly Brush _borderBrush;
    private readonly Brush _checkBrush;
    private readonly Brush _tintBrush;

    private LowLevelMouseProc _mouseProc;
    private LowLevelKeyboardProc _keyboardProc;
    private nint _mouseHook;
    private nint _keyboardHook;
    private readonly DispatcherTimer _autoCloseTimer;
    private long _generation;
    private bool _closing;
    private bool _accentApplied;

    private TrayMenuWindow(IReadOnlyList<TrayMenuEntry> entries, bool isDark, int autoCloseMs)
    {
        _isDark = isDark;

        Title = "QuickLook Tray Menu";
        WindowStyle = WindowStyle.None;
        // v1.2.37: layered window - the acrylic (SetWindowCompositionAttribute)
        // and the WPF-drawn corners/shadow rely on per-pixel transparency.
        AllowsTransparency = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily(TranslationHelper.Get("UI_FontFamily", failsafe: "Segoe UI"));
        FontSize = 13;

        _textBrush = CreateBrush(isDark ? "#F5F5F5" : "#1A1A1A");
        _disabledTextBrush = CreateBrush(isDark ? "#9E9E9E" : "#7A7A7A");
        _hoverBrush = CreateBrush(isDark ? "#14FFFFFF" : "#10000000");
        _separatorBrush = CreateBrush(isDark ? "#14FFFFFF" : "#10000000");
        _borderBrush = CreateBrush(isDark ? "#26FFFFFF" : "#26000000");
        _checkBrush = CreateBrush(isDark ? "#60CDFF" : "#005FB8");
        // v1.2.37: translucent overlay over the frosted acrylic - keep the
        // alpha moderate so the blur shows through while text stays readable.
        _tintBrush = CreateBrush(isDark ? "#8C20242A" : "#B8F8F6F4");

        _root = BuildMenu(entries);

        Content = new Border
        {
            // v1.2.37: the rounded panel fills the window exactly - the WCA
            // acrylic blurs the whole window rect, so a transparent margin
            // would show a square frosted frame around the rounded panel.
            Background = _tintBrush,
            BorderBrush = _borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.30,
            },
            Child = _root,
        };

        if (autoCloseMs > 0)
        {
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(autoCloseMs),
            };
            _autoCloseTimer.Tick += (_, _) => CloseMenu();
            _autoCloseTimer.Start();
        }

        Closed += (_, _) =>
        {
            _autoCloseTimer?.Stop();
            UninstallHooks();
        };
    }

    /// <summary>
    /// Reads back the DWM backdrop attributes of the currently open menu
    /// window so tests can prove Mica is actually applied.
    /// </summary>
    public static string DiagnoseBackdrop()
    {
        var menu = _current;
        if (menu is null || !menu.IsVisible)
            return "no menu window open";

        var hwnd = new WindowInteropHelper(menu).Handle;
        return $"hwnd=0x{hwnd.ToInt64():X} accent-applied={menu._accentApplied}";
    }

    /// <summary>
    /// Show the tray menu at the current cursor position. Any previously open
    /// menu is closed first.
    /// </summary>
    public static bool IsOpen => _current?.IsVisible == true;

    public static void CloseCurrentMenu()
    {
        _current?.CloseMenu();
    }

    /// <summary>
    /// Show a menu at the current cursor position (used by the tray icon).
    /// </summary>
    public static void ShowMenu(IReadOnlyList<TrayMenuEntry> entries, bool isDark, int autoCloseMs = 0)
    {
        ShowMenuCore(entries, isDark, anchor: null, autoCloseMs);
    }

    /// <summary>
    /// Show a menu anchored below a control (used by the preview window's
    /// "More" button), right-aligned like a Win11 flyout.
    /// </summary>
    public static void ShowMenu(IReadOnlyList<TrayMenuEntry> entries, bool isDark, FrameworkElement anchor,
        int autoCloseMs = 0)
    {
        ShowMenuCore(entries, isDark, anchor, autoCloseMs);
    }

    private static void ShowMenuCore(IReadOnlyList<TrayMenuEntry> entries, bool isDark, FrameworkElement anchor,
        int autoCloseMs)
    {
        _current?.CloseMenu();

        var menu = new TrayMenuWindow(entries, isDark, autoCloseMs);
        _current = menu;

        if (anchor is not null && TryGetAnchorRect(anchor, out var anchorRect))
            menu.ShowAt(anchorRect);
        else
            menu.ShowAtCursor();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyBackdrop();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Re-apply after first paint, like the preview window does; some WPF
        // windows need the DWM backdrop attribute set once the window is
        // actually visible for it to take effect reliably.
        ApplyBackdrop();
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private StackPanel BuildMenu(IReadOnlyList<TrayMenuEntry> entries)
    {
        var panel = new StackPanel
        {
            MinWidth = 232,
            Margin = new Thickness(0, 4, 0, 4),
        };

        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = _separatorBrush,
                    Margin = new Thickness(12, 4, 12, 4),
                });
                continue;
            }

            panel.Children.Add(BuildItem(entry));
        }

        return panel;
    }

    private Border BuildItem(TrayMenuEntry entry)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = BuildIcon(entry.Icon, entry.IsEnabled);
        if (icon is not null)
        {
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
        }

        var text = new TextBlock
        {
            Text = entry.Header,
            Foreground = entry.IsEnabled ? _textBrush : _disabledTextBrush,
            FontWeight = entry.IsBold ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(icon is null ? 12 : 8, 0, 12, 0),
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var check = new TextBlock
        {
            Text = "\uE73B", // Segoe MDL2 Assets: CheckMark
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = _checkBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 12, 0),
            Visibility = entry.IsChecked ? Visibility.Visible : Visibility.Collapsed,
        };
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        var item = new Border
        {
            Child = grid,
            Height = 32,
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            IsEnabled = entry.IsEnabled,
            Opacity = entry.IsEnabled ? 1d : 0.5d,
            ToolTip = string.IsNullOrEmpty(entry.ToolTip) ? null : entry.ToolTip,
        };

        if (entry.IsEnabled)
        {
            item.Cursor = Cursors.Hand;
            item.MouseEnter += (_, _) => item.Background = _hoverBrush;
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonUp += (_, _) =>
            {
                var command = entry.Command;
                CloseMenu();
                command?.Invoke();
            };
        }

        return item;
    }

    private FrameworkElement BuildIcon(object icon, bool isEnabled)
    {
        if (icon is string glyph && !string.IsNullOrWhiteSpace(glyph))
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = (FontFamily)(Application.Current?.Resources["SymbolThemeFontFamily"]
                    ?? new FontFamily("Segoe Fluent Icons")),
                FontSize = 14,
                Foreground = isEnabled ? _textBrush : _disabledTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
        }

        if (icon is ImageSource image)
        {
            return new Image
            {
                Source = image,
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
        }

        if (icon is FrameworkElement element)
        {
            element.Margin = new Thickness(12, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
            return element;
        }

        return null;
    }

    private void ShowAtCursor()
    {
        if (!GetCursorPos(out var pt))
            return;

        // width == 0 marks cursor placement (as opposed to an anchored menu).
        ShowAt(new Rect(pt.X, pt.Y, 0, 0));
    }

    private void ShowAt(Rect anchorPx)
    {
        _generation++;

        var hwnd = new WindowInteropHelper(this).Handle; // forces HWND creation
        this.SetNoactivate();

        var hMonitor = MonitorFromPoint(new POINT
        {
            X = (int)anchorPx.X,
            Y = (int)anchorPx.Y,
        }, MONITOR_DEFAULTTONEAREST);
        GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);

        var monitor = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref monitor);

        var scaleX = dpiX / 96d;
        var scaleY = dpiY / 96d;

        var content = (FrameworkElement)Content;
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        content.UpdateLayout();
        var menuWidth = content.DesiredSize.Width;
        var menuHeight = content.DesiredSize.Height;

        // Working area in DIPs, clamped so the whole menu stays on screen.
        var workLeft = monitor.rcWork.Left / scaleX;
        var workTop = monitor.rcWork.Top / scaleY;
        var workRight = monitor.rcWork.Right / scaleX;
        var workBottom = monitor.rcWork.Bottom / scaleY;

        double left;
        double top;

        if (anchorPx.Width > 0)
        {
            // Anchored below a control, right-aligned (Win11 flyout style).
            left = (anchorPx.Right / scaleX) - menuWidth;
            top = (anchorPx.Bottom / scaleY) + (4 / scaleY);
        }
        else
        {
            // Cursor placement (tray icon).
            left = anchorPx.X / scaleX;
            top = anchorPx.Y / scaleY;
        }

        left = Math.Clamp(left, workLeft, Math.Max(workLeft, workRight - menuWidth));
        top = Math.Clamp(top, workTop, Math.Max(workTop, workBottom - menuHeight));

        Left = left;
        Top = top;

        InstallHooks();
        Show();

        // Correct to exact physical pixels (handles mixed-DPI monitors where
        // the pre-show DIP placement may have been interpreted on another
        // monitor's scale). Same values in the common case, so no flicker.
        MoveWindow(hwnd,
            (int)Math.Round(left * scaleX),
            (int)Math.Round(top * scaleY),
            (int)Math.Round(menuWidth * scaleX),
            (int)Math.Round(menuHeight * scaleY),
            true);
    }

    private static bool TryGetAnchorRect(FrameworkElement anchor, out Rect rect)
    {
        rect = default;
        try
        {
            if (anchor.IsLoaded && PresentationSource.FromVisual(anchor) is not null)
            {
                var topLeft = anchor.PointToScreen(new Point(0, 0));
                var bottomRight = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight));
                rect = new Rect(topLeft, bottomRight);
                return true;
            }
        }
        catch
        {
            // Fall back to cursor placement below.
        }

        return false;
    }

    private void ApplyBackdrop()
    {
        // v1.2.37: the DWM SystembackdropType variant silently renders a dead
        // color on borderless popup windows, so always use the
        // SetWindowCompositionAttribute acrylic (TranslucentTB-style), which
        // is reliable on layered windows. Corners/shadow are drawn by WPF.
        _accentApplied = WindowHelper.EnableAcrylicBlur(this, GetTintColor(), _isDark, 0.3d);
    }

    private Color GetTintColor()
    {
        return _isDark ? Color.FromRgb(0x2A, 0x24, 0x20) : Color.FromRgb(0xF8, 0xF6, 0xF4);
    }

    private void InstallHooks()
    {
        if (_mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero)
            return;

        _mouseProc = MouseHookProc;
        _keyboardProc = KeyboardHookProc;

        var hMod = Kernel32.LoadLibrary("user32.dll");
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
    }

    private void UninstallHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _mouseProc = null;
        _keyboardProc = null;
    }

    private nint MouseHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_closing)
        {
            var message = (uint)wParam.ToInt64();
            if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                if (!IsPointInsideWindow(data.pt.X, data.pt.Y))
                    ScheduleClose();
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_closing && ((uint)wParam.ToInt64() is WM_KEYDOWN or WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (data.vkCode == VK_ESCAPE)
            {
                ScheduleClose();
                return (nint)1; // swallow Escape while the menu is open
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private bool IsPointInsideWindow(int x, int y)
    {
        if (!IsVisible)
            return false;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
            return false;

        return x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;
    }

    private void ScheduleClose()
    {
        var generation = _generation;
        Dispatcher.BeginInvoke(() =>
        {
            // Ignore stale close requests queued before the menu was reopened
            // (e.g. the right-click that opens a new menu also goes through
            // the mouse hook first).
            if (generation == _generation && IsVisible)
                CloseMenu();
        }, DispatcherPriority.Background);
    }

    private void CloseMenu()
    {
        if (_closing)
            return;

        _closing = true;
        _generation++;
        UninstallHooks();

        if (IsVisible)
            Close();
    }

    // ---- P/Invoke ---------------------------------------------------------

    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_XBUTTONDOWN = 0x020B;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint VK_ESCAPE = 0x1B;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int MDT_EFFECTIVE_DPI = 0;

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);
    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);
}

/// <summary>
/// Describes one row of the tray menu.
/// </summary>
internal sealed record TrayMenuEntry
{
    public string Header { get; init; }
    public Action Command { get; init; }
    public bool IsSeparator { get; init; }
    public bool IsChecked { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsBold { get; init; }
    public object Icon { get; init; }
    public string ToolTip { get; init; }

    public static TrayMenuEntry Separator => new() { IsSeparator = true };
}
