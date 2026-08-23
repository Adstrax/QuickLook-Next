// Copyright © 2017-2026 QL-Win Contributors
//
// This file is part of QuickLookNext program.
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace QuickLookNext.Common.Helpers;

/// <summary>
/// The app's current effective light/dark theme, shared between the preview
/// window and plugin web content (WebView2). Updated by the in-app theme toggle;
/// initialized from the OS setting.
/// </summary>
public static class AppThemeState
{
    public static bool IsDark { get; set; } = OSThemeHelper.AppsUseDarkTheme();
}
