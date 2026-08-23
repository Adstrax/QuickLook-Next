using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLookNext.Plugin.TextViewer.Themes.HighlightingDefinitions.Light;

/// <summary>
/// Rainbow PSV Highlighting Definition
/// </summary>
public class PSVHighlightingDefinition : LightHighlightingDefinition
{
    public override string Name => "PSV";

    public override string Extension => ".psv";

    public override DocumentColorizingTransformer[] LineTransformers { get; } = [new CSVHighlightingDefinition.RainbowTransformer('|')];
}
