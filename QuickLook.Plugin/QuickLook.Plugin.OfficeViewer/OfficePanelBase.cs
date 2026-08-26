using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QuickLook.Common.ExtensionMethods;
using QuickLook.Common.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.16.0: shared layout for the self-rendered Office previews, matching the
/// PDF viewer's structure - a left frame (transparent so the window's acrylic
/// backdrop shows) and a solid, non-blurred document surface on the right.
/// </summary>
public abstract class OfficePanelBase : UserControl, IDisposable
{
    private readonly WebView2 _webView = new();
    private readonly TextBlock _fileNameText;
    private readonly TextBlock _metaText;

    protected OfficePanelBase()
    {
        var isDark = OSThemeHelper.AppsUseDarkTheme();

        _webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\"),
        };
        _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

        // Left frame: transparent background so the window backdrop shows.
        var framePanel = new StackPanel { Margin = new Thickness(14, 14, 10, 14) };
        _fileNameText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = isDark ? TextBrush("#F5F5F5") : TextBrush("#1A1A1A"),
        };
        _metaText = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = 12,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap,
            Foreground = isDark ? TextBrush("#9E9E9E") : TextBrush("#7A7A7A"),
        };
        framePanel.Children.Add(_fileNameText);
        framePanel.Children.Add(_metaText);
        var frame = new Border
        {
            Width = 170,
            Background = Brushes.Transparent,
            Child = framePanel,
        };

        // Right side: solid document surface (no blur), theme-aware.
        var content = new Border
        {
            Background = isDark ? TextBrush("#1E1E1E") : Brushes.White,
            Child = _webView,
        };
        var separator = new Border
        {
            Width = 1,
            Background = isDark ? TextBrush("#26FFFFFF") : TextBrush("#26000000"),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(frame, 0);
        grid.Children.Add(frame);
        Grid.SetColumn(separator, 1);
        grid.Children.Add(separator);
        Grid.SetColumn(content, 2);
        grid.Children.Add(content);
        Content = grid;
    }

    protected void SetFrameInfo(string path, string typeLabel)
    {
        _fileNameText.Text = Path.GetFileName(path);
        try
        {
            var info = new FileInfo(path);
            _metaText.Text = $"{typeLabel}\n{info.Length.ToPrettySize(1)}";
        }
        catch
        {
            _metaText.Text = typeLabel;
        }
    }

    protected void Navigate(string html)
    {
        _ = _webView.EnsureCoreWebView2Async().ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html)));
    }

    private static Brush TextBrush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    public void Dispose()
    {
        _webView.Dispose();
    }
}
