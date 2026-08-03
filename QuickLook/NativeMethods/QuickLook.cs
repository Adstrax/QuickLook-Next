// Copyright © 2017-2026 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// QuickLook Lite: managed implementation of the shell integration that the full
// QuickLook performed through the native QuickLook.Native32/64/arm64 DLLs.
// Covers Explorer and Desktop (the space-key path). No C++/ATL toolchain required.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;

namespace QuickLook.NativeMethods;

internal static class QuickLook
{
    private const int S_OK = 0;
    private const int MaxPath = 32767;
    private const int SWC_DESKTOP = 8;
    private const int SWFO_NEEDDISPATCH = 1;
    private const int SVGIO_SELECTION = 0x1;
    private const short CF_HDROP = 15;
    private const int TYMED_HGLOBAL = 1;
    private const int DVASPECT_CONTENT = 1;

    private static readonly Guid IID_IDataObject = new("0000010E-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid IID_IServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    internal enum FocusedWindowType
    {
        Invalid,
        Desktop,
        Explorer,
        Dialog,
        Everything,
        DOpus,
        MultiCommander,
        IDM,
        FilePilot,
        DeskBox,
    }

    internal static void Init()
    {
        // Lite build: shell integration is fully managed; nothing to initialize.
    }

    internal static FocusedWindowType GetFocusedWindowType()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return FocusedWindowType.Invalid;

        if (IsCursorActivated(hwnd))
            return FocusedWindowType.Invalid;

        var cls = GetClassNameString(hwnd);

        // Desktop
        if (cls is "WorkerW" or "Progman")
        {
            if (FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                return FocusedWindowType.Desktop;
        }

        // File Explorer
        if (cls is "ExploreWClass" or "CabinetWClass")
        {
            if (!IsExplorerSearchBoxFocused())
                return FocusedWindowType.Explorer;
        }

        // Open/save dialogs
        if (cls == "#32770")
        {
            if (FindWindowEx(hwnd, IntPtr.Zero, "DUIViewWndClassName", null) != IntPtr.Zero
                && !IsExplorerSearchBoxFocused())
                return FocusedWindowType.Dialog;
        }

        return FocusedWindowType.Invalid;
    }

    internal static string GetCurrentSelection()
    {
        var result = string.Empty;

        // Communicate with Shell COM from a dedicated STA thread.
        var thread = new Thread(() =>
        {
            try
            {
                result = GetFocusedWindowType() switch
                {
                    FocusedWindowType.Desktop => GetSelectionFromDesktop(),
                    FocusedWindowType.Explorer => GetSelectionFromExplorer(),
                    _ => string.Empty,
                };
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (result.Length > 2 && result[0] == '"' && result[^1] == '"')
        {
            result = result.Substring(1, result.Length - 2);
        }

        return ResolveShortcut(result);
    }

    private static string GetSelectionFromExplorer()
    {
        var shellWindowsType = Type.GetTypeFromCLSID(CLSID_ShellWindows);
        if (shellWindowsType == null)
            return string.Empty;

        dynamic shellWindows = Activator.CreateInstance(shellWindowsType);
        try
        {
            var count = (int)shellWindows.Count;

            var foreground = GetForegroundWindow();
            var tabWindow = FindWindowEx(foreground, IntPtr.Zero, "ShellTabWindowClass", null);

            for (var i = 0; i < count; i++)
            {
                try
                {
                    object dispObj = shellWindows.Item(i);
                    if (dispObj == null)
                        continue;

                    var provider = (IServiceProvider)dispObj;
                    var browserIid = IID_IShellBrowser;
                    if (provider.QueryService(ref browserIid, ref browserIid, out var sbPtr) != S_OK)
                        continue;

                    var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(sbPtr);
                    try
                    {
                        if (browser.GetWindow(out var phwnd) != S_OK)
                            continue;

                        if (phwnd == foreground || (tabWindow != IntPtr.Zero && tabWindow == phwnd))
                        {
                            var selection = GetSelectedInternal(browser);
                            if (!string.IsNullOrEmpty(selection))
                                return selection;
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(browser);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellWindows);
        }

        return string.Empty;
    }

    private static string GetSelectionFromDesktop()
    {
        var shellWindowsType = Type.GetTypeFromCLSID(CLSID_ShellWindows);
        if (shellWindowsType == null)
            return string.Empty;

        dynamic shellWindows = Activator.CreateInstance(shellWindowsType);
        try
        {
            object loc = null;
            object locRoot = null;

            object dispOut;
            shellWindows.FindWindowSW(ref loc, ref locRoot, SWC_DESKTOP, out int hwnd,
                SWFO_NEEDDISPATCH, out dispOut);
            if (dispOut == null)
                return string.Empty;

            var disp = dispOut;
            try
            {
                var provider = (IServiceProvider)disp;
                var browserIid = IID_IShellBrowser;
                if (provider.QueryService(ref browserIid, ref browserIid, out var sbPtr) != S_OK)
                    return string.Empty;

                var browser = (IShellBrowser)Marshal.GetObjectForIUnknown(sbPtr);
                try
                {
                    return GetSelectedInternal(browser);
                }
                finally
                {
                    Marshal.ReleaseComObject(browser);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(disp);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellWindows);
        }
    }

    private static string GetSelectedInternal(IShellBrowser browser)
    {
        var hrShellView = browser.QueryActiveShellView(out var psvPtr);
        if (hrShellView != S_OK || psvPtr == IntPtr.Zero)
            return string.Empty;

        var view = (IShellView)Marshal.GetObjectForIUnknown(psvPtr);
        try
        {
            var dataObjectIid = IID_IDataObject;
            var hrItemObject = view.GetItemObject(SVGIO_SELECTION, ref dataObjectIid, out var daoPtr);
            if (hrItemObject != S_OK || daoPtr == IntPtr.Zero)
                return string.Empty;

            var dataObject = (IDataObject)Marshal.GetObjectForIUnknown(daoPtr);
            try
            {
                return ReadFirstFileFromDataObject(dataObject);
            }
            finally
            {
                Marshal.ReleaseComObject(dataObject);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(view);
        }
    }

    private static string ReadFirstFileFromDataObject(IDataObject dataObject)
    {
        var format = new FORMATETC
        {
            cfFormat = CF_HDROP,
            ptd = IntPtr.Zero,
            dwAspect = (DVASPECT)DVASPECT_CONTENT,
            lindex = -1,
            tymed = (TYMED)TYMED_HGLOBAL,
        };

        STGMEDIUM medium;
        try
        {
            dataObject.GetData(ref format, out medium);
        }
        catch (Exception)
        {
            return string.Empty;
        }

        try
        {
            if ((int)medium.tymed != TYMED_HGLOBAL || medium.unionmember == IntPtr.Zero)
                return string.Empty;

            var fileCount = DragQueryFile(medium.unionmember, 0xFFFFFFFF, null, 0);
            if (fileCount < 1)
                return string.Empty;

            var path = new StringBuilder(MaxPath);
            if (DragQueryFile(medium.unionmember, 0, path, path.Capacity) <= 0)
                return string.Empty;

            var longPath = new StringBuilder(MaxPath);
            GetLongPathName(path.ToString(), longPath, longPath.Capacity);
            return longPath.ToString();
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static string ResolveShortcut(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (!string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
            return path;

        var link = new ShellLink();
        ((IPersistFile)link).Load(path, 0);
        var sb = new StringBuilder(MaxPath);
        ((IShellLinkW)link).GetPath(sb, sb.Capacity, out _, 0);

        return sb.Length == 0 ? path : sb.ToString();
    }

    // ---- helpers ----------------------------------------------------------

    private static bool IsCursorActivated(IntPtr hwnd)
    {
        var threadId = GetWindowThreadProcessId(hwnd, out _);

        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref info))
            return false;

        return info.flags != 0 || info.hwndCaret != IntPtr.Zero;
    }

    private static bool IsExplorerSearchBoxFocused()
    {
        var hwnd = GetFocusedControl();
        if (hwnd == IntPtr.Zero)
            return false;

        return GetClassNameString(hwnd) == "Windows.UI.Core.CoreWindow";
    }

    private static IntPtr GetFocusedControl()
    {
        var threadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        if (threadId == 0 || !AttachThreadInput(GetCurrentThreadId(), threadId, true))
            return IntPtr.Zero;

        try
        {
            return GetFocus();
        }
        finally
        {
            AttachThreadInput(GetCurrentThreadId(), threadId, false);
        }
    }

    private static string GetClassNameString(IntPtr hwnd)
    {
        var buffer = new char[256];
        if (GetClassName(hwnd, buffer, buffer.Length) <= 0)
            return string.Empty;
        return new string(buffer).TrimEnd('\0');
    }

    // ---- P/Invoke ---------------------------------------------------------

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetLongPathName(string lpszShortPath, StringBuilder lpszLongPath, int cchBuffer);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // ---- Shell COM interfaces ---------------------------------------------

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    }

    [ComImport, Guid("000214E2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        // IOleWindow
        [PreserveSig] int GetWindow(out IntPtr phwnd);

        [PreserveSig] int ContextSensitiveHelp(int fEnterMode);

        // IShellBrowser
        [PreserveSig] int InsertMenusSB(IntPtr hmenuShared, IntPtr lpMenuWidths);

        [PreserveSig] int SetMenuSB(IntPtr hmenuShared, IntPtr holemenu, IntPtr hwndActiveObject);

        [PreserveSig] int RemoveMenusSB(IntPtr hmenuShared);

        [PreserveSig] int SetStatusTextSB(IntPtr pszStatusText);

        [PreserveSig] int EnableModelessSB(int fEnable);

        [PreserveSig] int TranslateAcceleratorSB(IntPtr pmsg, ushort wID);

        [PreserveSig] int BrowseObject(IntPtr pidl, uint wFlags);

        [PreserveSig] int GetViewStateStream(uint grfMode, out IntPtr ppstm);

        [PreserveSig] int GetControlWindow(uint id, out IntPtr phwnd);

        [PreserveSig]
        int SendControlMsg(uint id, uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr pret);

        [PreserveSig] int QueryActiveShellView(out IntPtr ppshv);

        [PreserveSig] int OnViewWindowActive(IntPtr pshv);

        [PreserveSig] int SetToolbarItems(IntPtr lpButtons, uint nButtons, uint uFlags);
    }

    [ComImport, Guid("000214E3-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView
    {
        // IOleWindow
        [PreserveSig] int GetWindow(out IntPtr phwnd);

        [PreserveSig] int ContextSensitiveHelp(int fEnterMode);

        // IShellView
        [PreserveSig] int TranslateAccelerator(IntPtr pmsg);

        [PreserveSig] int EnableModeless(int fEnable);

        [PreserveSig] int UIActivate(uint uState);

        [PreserveSig] int Refresh();

        [PreserveSig]
        int CreateViewWindow(IntPtr psb, IntPtr psvPrev, uint dwFlags, IntPtr prcView, out IntPtr phwnd);

        [PreserveSig] int DestroyViewWindow();

        [PreserveSig] int GetCurrentInfo(out IntPtr pvi);

        [PreserveSig] int AddPropertySheetPages(uint dwReserved, IntPtr pfn, IntPtr lparam);

        [PreserveSig] int SaveViewState();

        [PreserveSig] int SelectItem(IntPtr pidlItem, uint uFlags);

        [PreserveSig] int GetItemObject(uint uItem, ref Guid riid, out IntPtr ppv);
    }
}
