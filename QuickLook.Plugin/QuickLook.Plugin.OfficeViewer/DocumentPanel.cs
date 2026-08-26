namespace QuickLook.Plugin.OfficeViewer;

/// <summary>
/// v3.14.0: self-rendered Word document preview (.docx -> styled HTML).
/// </summary>
public sealed class DocumentPanel : OfficePanelBase
{
    public DocumentPanel(string path)
    {
        Navigate(DocxToHtml.Convert(path));
    }
}
