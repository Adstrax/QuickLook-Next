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

using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using QuickLook.Common.Helpers;
using QuickLook.Plugin.TextViewer.Detectors;
using QuickLook.Plugin.TextViewer.Themes.HighlightingDefinitions;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace QuickLook.Plugin.TextViewer.Themes;

public class HighlightingThemeManager
{
    public static HighlightingManager Light { get; internal set; }

    public static HighlightingManager Dark { get; internal set; }

    public static void Initialize()
    {
        InitHighlightingManager();
        InitCustomHighlighting();
    }

    public static HighlightingTheme GetHighlightingByExtensionOrDetector(string path, string extension, string text = null)
    {
        if (Light is null || Dark is null) return HighlightingTheme.Default;

        var useFormatDetector = SettingHelper.Get("UseFormatDetector", true, "QuickLook.Plugin.TextViewer");
        // v1.2.35: the format-detector pass scans the whole text and is only
        // meaningful when syntax highlighting will actually be applied.
        // TextViewer disables highlighting above 0.5 MB, so skip the expensive
        // scan for large files (e.g. package-lock.json) instead of walking MBs
        // of text on the UI thread for a result that is thrown away.
        const int MaxHighlightingLength = 512 * 1024;
        if (text is { Length: > MaxHighlightingLength })
            useFormatDetector = false;

        var highlightingTheme = GetDefinitionByExtension(nameof(Dark), extension);

        if (useFormatDetector && FormatDetector.Confuse(path, text) is IFormatDetector confusedFormatDetector)
        {
            if (!string.IsNullOrEmpty(confusedFormatDetector.Extension))
            {
                highlightingTheme = GetDefinitionByExtension(nameof(Dark), confusedFormatDetector.Extension)
                                 ?? GetDefinitionByExtension(nameof(Light), confusedFormatDetector.Extension);
            }
            else
            {
                highlightingTheme = GetDefinition(nameof(Dark), confusedFormatDetector.Name)
                                 ?? GetDefinition(nameof(Light), confusedFormatDetector.Name);
            }
        }

        if (highlightingTheme == null)
        {
            highlightingTheme = GetDefinitionByExtension(nameof(Light), extension);

            if (highlightingTheme == null)
            {
                if (useFormatDetector && FormatDetector.Detect(path, text)?.Extension is string detectExtension)
                {
                    highlightingTheme = GetDefinitionByExtension(nameof(Dark), detectExtension)
                                     ?? GetDefinitionByExtension(nameof(Light), detectExtension);
                }
            }
        }

        // The unsupported highlighting will be fallback to not highlighted text
        highlightingTheme ??= GetDefinitionByExtension(nameof(Dark), ".txt")
                          ?? GetDefinitionByExtension(nameof(Light), ".txt")
                          ?? HighlightingTheme.Default;

        var darkThemeAllowed = SettingHelper.Get("AllowDarkTheme", highlightingTheme.IsDark, "QuickLook.Plugin.TextViewer");
        var isDark = darkThemeAllowed && OSThemeHelper.AppsUseDarkTheme();

        // The current environment does not require dark mode so revert to light mode
        if (!isDark && highlightingTheme.IsDark)
        {
            highlightingTheme.Theme = nameof(Light);
            highlightingTheme.HighlightingManager = Light;
            highlightingTheme.SyntaxHighlighting
                // The extension that supports dark mode must support light mode also
                = Light.GetDefinitionByExtension(highlightingTheme.Extension);
        }

        return highlightingTheme;
    }

    private static HighlightingTheme GetDefinition(string theme, string extension)
    {
        var highlightingManager = theme == nameof(Dark) ? Dark : Light;
        var def = highlightingManager.GetDefinition(extension);

        if (def != null)
        {
            return new HighlightingTheme()
            {
                Theme = theme,
                HighlightingManager = highlightingManager,
                SyntaxHighlighting = def,
                Extension = extension,
            };
        }
        return null;
    }

    private static HighlightingTheme GetDefinitionByExtension(string theme, string extension)
    {
        var highlightingManager = theme == nameof(Dark) ? Dark : Light;
        var def = highlightingManager.GetDefinitionByExtension(extension);

        if (def != null)
        {
            return new HighlightingTheme()
            {
                Theme = theme,
                HighlightingManager = highlightingManager,
                SyntaxHighlighting = def,
                Extension = extension,
            };
        }
        return null;
    }

    private static void InitHighlightingManager()
    {
        Light = new HighlightingManager();
        Dark = new HighlightingManager();

        var items = new ConcurrentQueue<(string Name, HighlightingManager Hlm,
            string[] Exts, XshdSyntaxDefinition Xshd)>();

        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();

        // v3.2.0: loading ~250 XSHD syntax files (XML parse + regex rule
        // compilation) serially cost ~1 s of startup. Parsing (LoadXshd) is
        // fully independent, and compilation (HighlightingLoader.Load) only
        // needs referenced definitions to already be registered - a handful
        // of files reference C#/JavaScript, so RegisterHighlightings retries
        // those in a second round after the base definitions land.
        Parallel.ForEach(resourceNames.Where(name => name.Contains(".Syntax.")), resourceName =>
        {
            using Stream s = assembly.GetManifestResourceStream(resourceName);

            if (s == null)
                return;

            Debug.WriteLine(resourceName);

            try
            {
                var hlm = resourceName.Contains(".Syntax.Dark.") ? Dark : Light;
                var name = EmbeddedResource.GetFileNameWithoutExtension(resourceName);
                using var reader = new XmlTextReader(s);
                var xshd = HighlightingLoader.LoadXshd(reader);
                if (xshd.Extensions.Count > 0)
                    items.Enqueue((name, hlm, [.. xshd.Extensions], xshd));
            }
            catch (Exception e)
            {
                ProcessHelper.WriteLog(e.ToString());
            }
        });

        AddHighlightingManager(Light, nameof(Light), items);
        AddHighlightingManager(Dark, nameof(Dark), items);

        RegisterHighlightings(items);
    }

    private static void AddHighlightingManager(HighlightingManager hlm, string dirName,
        ConcurrentQueue<(string Name, HighlightingManager Hlm, string[] Exts, XshdSyntaxDefinition Xshd)> items)
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyPath))
            return;

        var syntaxPath = Path.Combine(assemblyPath, "Syntax", dirName);
        if (!Directory.Exists(syntaxPath))
            return;

        var files = Directory.EnumerateFiles(syntaxPath, "*.xshd").OrderBy(f => f).ToArray();
        if (files.Length == 0)
            return;

        Parallel.ForEach(files, file =>
        {
            try
            {
                Debug.WriteLine(file);
                var ext = Path.GetFileNameWithoutExtension(file);
                using var fileStream = new FileStream(Path.GetFullPath(file), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new XmlTextReader(fileStream);
                var xshd = HighlightingLoader.LoadXshd(reader);
                if (xshd.Extensions.Count > 0)
                    items.Enqueue((ext, hlm, [.. xshd.Extensions], xshd));
            }
            catch (Exception e)
            {
                ProcessHelper.WriteLog(e.ToString());
            }
        });
    }

    /// <summary>
    /// Compiles and registers parsed XSHD definitions. A definition whose
    /// rules reference another definition (e.g. <c>reference="JavaScript"</c>)
    /// can only compile once that definition is registered, so the batch is
    /// retried in rounds: every round compiles everything it can in parallel,
    /// registers the successes, and defers the rest to the next round.
    /// </summary>
    private static void RegisterHighlightings(
        ConcurrentQueue<(string Name, HighlightingManager Hlm, string[] Exts, XshdSyntaxDefinition Xshd)> items)
    {
        var remaining = items.ToList();

        while (remaining.Count > 0)
        {
            var loaded = new ConcurrentQueue<(HighlightingManager Hlm, string Name,
                string[] Exts, IHighlightingDefinition Def)>();

            Parallel.ForEach(remaining, item =>
            {
                try
                {
                    var def = HighlightingLoader.Load(item.Xshd, item.Hlm);
                    loaded.Enqueue((item.Hlm, item.Name, item.Exts, def));
                }
                catch (HighlightingDefinitionInvalidException)
                {
                    // Referenced definition not registered yet; retry next round.
                }
                catch (Exception e)
                {
                    ProcessHelper.WriteLog(e.ToString());
                }
            });

            if (loaded.IsEmpty)
                break; // circular or unresolvable references; nothing more to do

            var loadedList = loaded.ToList();

            // Registration mutates the HighlightingManager dictionaries; do it
            // on one thread to stay thread-safe.
            foreach (var item in loadedList)
            {
                try
                {
                    item.Hlm.RegisterHighlighting(item.Name, item.Exts, item.Def);
                }
                catch (Exception e)
                {
                    ProcessHelper.WriteLog(e.ToString());
                }
            }

            remaining = remaining
                .Where(item => !loadedList.Any(l =>
                    l.Name == item.Name && ReferenceEquals(l.Hlm, item.Hlm)))
                .ToList();
        }

        foreach (var leftover in remaining)
        {
            ProcessHelper.WriteLog(
                $"Skipped highlighting definition {leftover.Name}: references could not be resolved.");
        }
    }

    private static void InitCustomHighlighting()
    {
        foreach (var definitionClass in LoadAllDefinitions())
        {
            var hlm = definitionClass.Theme == nameof(Dark) ? Dark : Light;

            AddCustomHighlighting(hlm, definitionClass.Instance);
        }

        static IEnumerable<CustomHighlightingDefinitionClass> LoadAllDefinitions()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(t => t.IsClass
                        && !t.IsAbstract
                        && typeof(ICustomHighlightingDefinition).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in types)
            {
                if (type.GetCustomAttribute<CustomHighlightingDefinitionAttribute>() is CustomHighlightingDefinitionAttribute { } attr)
                {
                    if (Activator.CreateInstance(type) is ICustomHighlightingDefinition instance)
                    {
                        yield return new CustomHighlightingDefinitionClass(instance, attr.Theme);
                    }
                }
            }
        }
    }

    private static void AddCustomHighlighting(HighlightingManager hlm, ICustomHighlightingDefinition definition)
    {
        try
        {
            hlm.RegisterHighlighting(definition.Name, definition.Extension.Split(';'), definition);
        }
        catch (Exception e)
        {
            ProcessHelper.WriteLog(e.ToString());
        }
    }
}

file static class EmbeddedResource
{
    public static string GetFileNameWithoutExtension(string resourceName)
    {
        // Requires the embedded resource file name
        // must have a file extension and have only one '.' character
        int start = int.MinValue, end = int.MinValue;

        for (int i = resourceName.Length - 1; i >= 0; i--)
        {
            if (resourceName[i] == '.')
            {
                if (end == int.MinValue)
                {
                    end = i;
                    continue;
                }

                if (start == int.MinValue)
                {
                    start = i + 1; // Exinclude '.' character
                    break;
                }
            }
        }

        if ((start != int.MinValue) && (end != int.MinValue))
        {
            return resourceName.Substring(start, end - start);
        }
        return resourceName;
    }
}
