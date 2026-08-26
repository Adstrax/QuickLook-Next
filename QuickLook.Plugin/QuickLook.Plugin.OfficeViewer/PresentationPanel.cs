using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.15.0: self-rendered PowerPoint preview (.pptx -> styled HTML slides).
/// </summary>
public sealed class PresentationPanel : OfficePanelBase
{
    public PresentationPanel(string path)
    {
        SetFrameInfo(path, "PowerPoint 演示文稿");
        Navigate(PptxToHtml.Convert(path, OSThemeHelper.AppsUseDarkTheme()));
    }
}
