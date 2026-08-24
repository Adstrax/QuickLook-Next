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

using PdfiumViewer;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace QuickLook.Plugin.PDFViewer;

public sealed class Plugin : IViewer
{
    private ContextObject _context;
    private string _path;
    private PdfViewerControl _pdfControl;
    private PasswordControl _passwordControl;

    public int Priority => -1;

    public void Init()
    {
    }

public bool CanHandle(string path)
{
    if (Directory.Exists(path))
        return false;

    var extension = Path.GetExtension(path);
    if (extension.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        return true;

    // v1.2.35: files with a known extension are matched by extension only, so
    // previewing e.g. txt/zip files no longer opens the file on the UI thread
    // for nothing. Magic-number detection is reserved for extensionless files.
    if (extension.Length > 0)
        return false;

    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    byte[] buffer = new byte[4];
    if (fs.Read(buffer, 0, 4) < 4) return false;
    return buffer[0] == (byte)'%' &&
    buffer[1] == (byte)'P' &&
    buffer[2] == (byte)'D' &&
    buffer[3] == (byte)'F';
}

    public void Prepare(string path, ContextObject context)
    {
        _context = context;
        _path = path;

        // Don't load the document just to size the window. View loads it once
        // and resizes afterwards, so the PDF is not parsed twice.
        context.PreferredSize = new Size { Width = 800, Height = 600 };
    }

    public void View(string path, ContextObject context)
    {
        _pdfControl = new PdfViewerControl();
        context.ViewerContent = _pdfControl;

        Exception exception = null;

        _ = _pdfControl.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _pdfControl.LoadPdf(path);

                ResizeWindowToContent(context);

                context.Title = $"1 / {_pdfControl.TotalPages}: {Path.GetFileName(path)}";

                _pdfControl.CurrentPageChanged += UpdateWindowCaption;
                context.IsBusy = false;
            }
            catch (PdfException ex) when (ex.Message == "Password required or incorrect password")
            {
                // Fallback to request a password
                _passwordControl = new PasswordControl();
                _passwordControl.PasswordRequested += (string password) =>
                {
                    try
                    {
                        context.ViewerContent = _pdfControl;
                        context.IsBusy = true;
                        _pdfControl.LoadPdf(path, password);

                        ResizeWindowToContent(context);

                        context.Title = $"1 / {_pdfControl.TotalPages}: {Path.GetFileName(path)}";

                        _pdfControl.CurrentPageChanged += UpdateWindowCaption;
                        context.IsBusy = false;
                    }
                    catch (PdfException ex) when (ex.Message == "Password required or incorrect password")
                    {
                        // This password is not accepted
                        return false;
                    }

                    // This password is accepted
                    return true;
                };

                context.ViewerContent = _passwordControl;
                context.Title = $"[PASSWORD PROTECTED] {Path.GetFileName(path)}";
                context.IsBusy = false;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }, DispatcherPriority.Loaded).Wait();

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    public void Cleanup()
    {
        GC.SuppressFinalize(this);

        _pdfControl?.Dispose();
        _pdfControl = null;

        _context = null;
    }

    private void UpdateWindowCaption(object sender, EventArgs e2)
    {
        _context.Title = $"{_pdfControl.CurrentPage + 1} / {_pdfControl.TotalPages}: {Path.GetFileName(_path)}";
    }

    private void ResizeWindowToContent(ContextObject context)
    {
        context.SetPreferredSizeFit(_pdfControl.GetDesiredControlSize(), 0.9);

        if (Window.GetWindow(_pdfControl) is not Window window)
            return;

        // Call the viewer window private method using reflection
        // QuickLookNext.ViewerWindow.ResizeAndCentreExistingWindow
        var resizeMethod = window.GetType().GetMethod("ResizeAndCentreExistingWindow",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (resizeMethod == null)
            return;

        var newRect = (Rect)resizeMethod.Invoke(window, [context.PreferredSize]);
        window.MoveWindow(newRect.Left, newRect.Top, newRect.Width, newRect.Height);
    }
}
