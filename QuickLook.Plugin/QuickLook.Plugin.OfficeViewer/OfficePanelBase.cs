using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
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
    private bool _disposed;

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

    /// <summary>
    /// v3.24.0: invoked once on the UI thread after the first WebView2
    /// navigation completes, so the plugin can clear the busy spinner exactly
    /// when the content becomes visible (instead of showing a blank area).
    /// </summary>
    public Action Ready { get; set; }

    protected void Navigate(string html)
    {
        HookReadyOnce();

        _ = _webView.EnsureCoreWebView2Async().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                // WebView2 could not start; clear the spinner so the window
                // never stays stuck on a loading state.
                Dispatcher.BeginInvoke(() =>
                {
                    var ready = Ready;
                    Ready = null;
                    ready?.Invoke();
                });
                return;
            }

            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html));
        });
    }

    /// <summary>
    /// v3.23.0: builds the HTML on a background thread (OOXML parsing can take
    /// hundreds of ms on large documents) and navigates back on the UI thread
    /// when ready, so the preview window stays responsive while the document
    /// parses.
    /// </summary>
    protected void NavigateAsync(Func<string> buildHtml)
    {
        var dispatcher = Dispatcher;

        _ = Task.Run(buildHtml).ContinueWith(t =>
        {
            dispatcher.BeginInvoke(() =>
            {
                if (_disposed)
                    return;

                if (t.IsFaulted)
                {
                    Navigate(ErrorHtml(t.Exception?.GetBaseException()));
                    return;
                }

                Navigate(t.Result);
            });
        }, TaskScheduler.Default);
    }

    private void HookReadyOnce()
    {
        if (Ready is null)
            return;

        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
        handler = (_, _) =>
        {
            _webView.NavigationCompleted -= handler;
            var ready = Ready;
            Ready = null;
            ready?.Invoke();
        };
        _webView.NavigationCompleted += handler;
    }

    public void Dispose()
    {
        _disposed = true;
        _webView.Dispose();
    }

    private static string ErrorHtml(Exception error)
    {
        return
            """
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <style>body{margin:24px;font-family:'Segoe UI',sans-serif;font-size:14px;color:#C42B1C}</style>
            </head><body><div>无法读取此文档（文件可能已损坏或格式不受支持）。</div></body></html>
            """ + (error is null ? string.Empty : $"<!-- {error.Message} -->");
    }
}
