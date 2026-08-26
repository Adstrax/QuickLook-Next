using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.15.0: self-rendered PowerPoint preview. The .pptx is converted to a
/// styled HTML slide deck (PptxToHtml) and shown in WebView2, inheriting the
/// app's rounded corners / acrylic / theme.
/// </summary>
public sealed class PresentationPanel : UserControl, IDisposable
{
    private readonly WebView2 _webView = new();

    public PresentationPanel()
    {
        _webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\"),
        };
        _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        Content = _webView;
    }

    public void LoadPresentation(string path)
    {
        var html = PptxToHtml.Convert(path, OSThemeHelper.AppsUseDarkTheme());
        _ = _webView.EnsureCoreWebView2Async().ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html)));
    }

    public void Dispose()
    {
        _webView.Dispose();
    }
}
