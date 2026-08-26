using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.14.0: self-rendered Word document preview. The .docx is converted to
/// styled HTML (DocxToHtml) and shown in WebView2, so the preview inherits the
/// app's rounded corners / acrylic / theme instead of the system component.
/// </summary>
public sealed class DocumentPanel : UserControl, IDisposable
{
    private readonly WebView2 _webView = new();

    public DocumentPanel()
    {
        _webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\"),
        };
        _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        Content = _webView;
    }

    public void LoadDocument(string path)
    {
        var html = DocxToHtml.Convert(path, OSThemeHelper.AppsUseDarkTheme());
        _ = _webView.EnsureCoreWebView2Async().ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html)));
    }

    public void Dispose()
    {
        _webView.Dispose();
    }
}
