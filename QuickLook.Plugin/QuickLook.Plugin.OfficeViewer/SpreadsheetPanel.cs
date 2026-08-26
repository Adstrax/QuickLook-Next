using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MiniExcelLibs;
using QuickLook.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.12.0: self-rendered spreadsheet preview. The workbook is read with
/// MiniExcel and rendered as a styled HTML table in WebView2, so the preview
/// inherits the app's rounded corners / acrylic / theme instead of hosting
/// the Windows system preview component.
/// </summary>
public sealed class SpreadsheetPanel : UserControl, IDisposable
{
    private const int MaxRows = 300;
    private const int MaxColumns = 50;

    private readonly WebView2 _webView = new();

    public SpreadsheetPanel()
    {
        _webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(SettingHelper.LocalDataPath, @"WebView2_Data\"),
        };
        // Transparent so the window's acrylic backdrop shows through.
        _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
        Content = _webView;
    }

    public void LoadSpreadsheet(string path)
    {
        var html = BuildHtml(path);
        _ = _webView.EnsureCoreWebView2Async().ContinueWith(_ =>
            Dispatcher.BeginInvoke(() => _webView.NavigateToString(html)));
    }

    private static string BuildHtml(string path)
    {
        var isDark = OSThemeHelper.AppsUseDarkTheme();
        var rows = ReadRows(path);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
        if (isDark)
        {
            sb.Append(":root{--fg:#F5F5F5;--border:rgba(255,255,255,.14);" +
                "--head-bg:#22262C;--alt-bg:rgba(255,255,255,.03);--error:#FF7B72;" +
                "--scroll-thumb:rgba(255,255,255,.28);--scroll-thumb-hover:rgba(255,255,255,.45)}");
        }
        else
        {
            sb.Append(":root{--fg:#1A1A1A;--border:rgba(0,0,0,.12);" +
                "--head-bg:#F3F3F3;--alt-bg:rgba(0,0,0,.02);--error:#C42B1C;" +
                "--scroll-thumb:rgba(0,0,0,.28);--scroll-thumb-hover:rgba(0,0,0,.45)}");
        }
        sb.Append("html,body{height:100%}body{margin:0;color:var(--fg);background:transparent;" +
            "font-family:'Segoe UI',Helvetica,Arial,sans-serif}");
        // v3.13.0: slim, theme-aware scrollbar that blends into the acrylic
        // backdrop instead of the thick default white-track WebView2 bar.
        sb.Append("::-webkit-scrollbar{width:8px;height:8px}");
        sb.Append("::-webkit-scrollbar-track{background:transparent}");
        sb.Append("::-webkit-scrollbar-thumb{background:var(--scroll-thumb);border-radius:4px}");
        sb.Append("::-webkit-scrollbar-thumb:hover{background:var(--scroll-thumb-hover)}");
        sb.Append("::-webkit-scrollbar-corner{background:transparent}");
        sb.Append(".wrap{padding:16px;overflow:auto;height:100%;box-sizing:border-box}");
        sb.Append("table{border-collapse:collapse;font-size:13px;min-width:100%}");
        sb.Append("th,td{border:1px solid var(--border);padding:5px 10px;white-space:nowrap}");
        sb.Append("thead th{position:sticky;top:0;background:var(--head-bg);font-weight:600;z-index:1}");
        sb.Append("tbody tr:nth-child(even) td{background:var(--alt-bg)}");
        sb.Append(".error{color:var(--error);padding:24px;font-size:14px}");
        sb.Append("</style></head><body>");

        if (rows is null)
        {
            sb.Append("<div class=\"error\">无法读取此工作簿（文件可能已损坏或格式不受支持）。</div>");
        }
        else
        {
            sb.Append("<div class=\"wrap\"><table><thead><tr>");
            var colCount = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
            for (var c = 0; c < colCount; c++)
                sb.Append("<th>").Append(ColumnName(c)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var cell in row)
                    sb.Append("<td>").Append(cell).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static List<List<string>> ReadRows(string path)
    {
        try
        {
            var result = new List<List<string>>();
            foreach (var row in MiniExcel.Query(path, useHeaderRow: false))
            {
                var cells = new List<string>();
                foreach (var kv in row)
                {
                    if (cells.Count >= MaxColumns)
                        break;
                    cells.Add(kv.Value is null ? string.Empty : WebUtility.HtmlEncode(kv.Value.ToString()));
                }

                // Trim trailing empty cells so short rows do not stretch the table.
                while (cells.Count > 0 && cells[^1].Length == 0)
                    cells.RemoveAt(cells.Count - 1);

                result.Add(cells);
                if (result.Count >= MaxRows)
                    break;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        var n = index;
        while (n >= 0)
        {
            name = (char)('A' + n % 26) + name;
            n = n / 26 - 1;
        }

        return name;
    }

    public void Dispose()
    {
        _webView.Dispose();
    }
}
