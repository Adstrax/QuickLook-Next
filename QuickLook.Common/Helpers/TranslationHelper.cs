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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.XPath;

namespace QuickLook.Common.Helpers;

public static class TranslationHelper
{
    private static readonly CultureInfo CurrentCultureInfo = CultureInfo.CurrentUICulture;

    private static readonly Dictionary<string, XPathDocument> FileCache = [];
    private static readonly object SyncRoot = new();

    public static string Get(string id, string file = null, CultureInfo locale = null, string failsafe = null,
        string domain = "QuickLookNext")
    {
        if (file == null)
        {
            var subDir = domain == "QuickLookNext" ? string.Empty : $"QuickLook.Plugin\\{domain}";
            // v3.2.0 fix: resolve against the app root (exe directory), not
            // the location of QuickLook.Common.dll. The release package keeps
            // QuickLook.Common.dll inside lib\, and resolving against the
            // assembly location would look for Translations.config in lib\.
            file = Path.Combine(AppContext.BaseDirectory, subDir, "Translations.config");
        }

        if (!File.Exists(file))
            return failsafe ?? id;

        if (locale == null)
            locale = ResolveLocale();

        var nav = GetLangFile(file).CreateNavigator();

        // try to get string
        var s = GetStringFromXml(nav, id, locale);
        if (s != null)
            return s;

        // try again for parent language
        if (locale.Parent.Name != string.Empty)
            s = GetStringFromXml(nav, id, locale.Parent);
        if (s != null)
            return s;

        // use fallback language
        s = GetStringFromXml(nav, id, CultureInfo.GetCultureInfo("en"));
        if (s != null)
            return s;

        return failsafe ?? id;
    }

    /// <summary>
    /// v1.5.0: the language override chosen in the tray menu. When set, it
    /// wins over the OS UI culture; an empty value means "follow system".
    /// </summary>
    private static CultureInfo ResolveLocale()
    {
        var overrideName = SettingHelper.Get("Language", string.Empty, "QuickLookNext");
        if (!string.IsNullOrWhiteSpace(overrideName))
        {
            try
            {
                return CultureInfo.GetCultureInfo(overrideName);
            }
            catch
            {
                // Invalid stored value - fall through to the OS culture.
            }
        }

        return CurrentCultureInfo;
    }

    /// <summary>
    /// v1.5.0: lists the language blocks shipped in the main Translations.config
    /// so the tray menu can offer a language picker.
    /// </summary>
    public static IReadOnlyList<string> GetAvailableLanguages(string file = null)
    {
        if (file == null)
        {
            file = Path.Combine(AppContext.BaseDirectory, "Translations.config");
        }

        if (!File.Exists(file))
            return ["en"];

        var nav = GetLangFile(file).CreateNavigator();
        var languages = new List<string>();
        var iterator = nav.Select("/Translations/*");
        while (iterator.MoveNext())
            languages.Add(iterator.Current.Name);

        return languages;
    }

    private static string GetStringFromXml(XPathNavigator nav, string id, CultureInfo locale)
    {
        var result = nav.SelectSingleNode($@"/Translations/{locale.Name}/{id}");

        return result?.Value;
    }

    private static XPathDocument GetLangFile(string file)
    {
        lock (SyncRoot)
        {
            if (FileCache.TryGetValue(file, out var existing))
                return existing;

            var doc = new XPathDocument(file);
            FileCache[file] = doc;
            return doc;
        }
    }
}
