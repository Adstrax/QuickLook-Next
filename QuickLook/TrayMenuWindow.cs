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

    private LowLevelMouseProc _mouseProc;
    private LowLevelKeyboardProc _keyboardProc;
    private nint _mouseHook;
    private nint _keyboardHook;
    private readonly DispatcherTimer _autoCloseTimer;
    private long _generation;
    private bool _closing;

    private TrayMenuWindow(IReadOnlyList<TrayMenuEntry> entries, bool isDark, int autoCloseMs)
    {
        _isDark = isDark;

        Title = "QuickLook Tray Menu";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
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

        _root = BuildMenu(entries);

        Content = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = _borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
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
    /// Show the tray menu at the current cursor position. Any previously open
    /// menu is closed first.
    /// </summary>
    public static void ShowMenu(IReadOnlyList<TrayMenuEntry> entries, bool isDark, int autoCloseMs = 0)
    {
        _current?.CloseMenu();

        var menu = new TrayMenuWindow(entries, isDark, autoCloseMs);
        _current = menu;
        menu.ShowAtCursor();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
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
        var text = new TextBlock
        {
            Text = entry.Header,
            Foreground = entry.IsEnabled ? _textBrush : _disabledTextBrush,
            FontWeight = entry.IsBold ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
        };

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

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        Grid.SetColumn(check, 1);
        grid.Children.Add(text);
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

    private void ShowAtCursor()
    {
        _generation++;

        if (!GetCursorPos(out var pt))
            return;

        var hwnd = new WindowInteropHelper(this).Handle; // forces HWND creation
        this.SetNoactivate();

        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
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

        var left = Math.Clamp(pt.X / scaleX, workLeft, Math.Max(workLeft, workRight - menuWidth));
        var top = Math.Clamp(pt.Y / scaleY, workTop, Math.Max(workTop, workBottom - menuHeight));

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

    private void ApplyBackdrop()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(hwnd) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        if (App.IsWin11)
        {
            if (Environment.OSVersion.Version >= new Version(10, 0, 22523))
                WindowHelper.EnableBackdropMicaBlur(this, _isDark);
            else
                WindowHelper.EnableMicaBlur(this, _isDark);

            WindowHelper.SetWindowCorner(this, Dwmapi.WindowCornerStyle.Round);
        }
        else
        {
            WindowHelper.EnableBlur(this);
            Background = new SolidColorBrush(_isDark
                ? Color.FromRgb(0x20, 0x20, 0x20)
                : Color.FromRgb(0xF3, 0xF3, 0xF3));
        }
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

    public static TrayMenuEntry Separator => new() { IsSeparator = true };
}
