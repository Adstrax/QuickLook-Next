using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.16.0/v3.17.0: shared surface for the self-rendered Office previews.
/// The content always sits on a plain white paper-like surface (never
/// theme-adapted) so real documents stay readable in dark mode too.
/// </summary>
public abstract class OfficePanelBase : UserControl, IDisposable
{
    private readonly WebView2 _webView = new();

    protected OfficePanelBase()
    {
        _webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\"),
        };
        // v3.17.0: solid white paper surface, independent of the app theme.
        _webView.DefaultBackgroundColor = System.Drawing.Color.White;
        Content = new Border
        {
            Background = Brushes.White,
            Child = _webView,
        };
    }

    protected void Navigate(string html)
    {
        _ = _webView.EnsureCoreWebView2Async().ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html)));
    }

    public void Dispose()
    {
        _webView.Dispose();
    }
}
