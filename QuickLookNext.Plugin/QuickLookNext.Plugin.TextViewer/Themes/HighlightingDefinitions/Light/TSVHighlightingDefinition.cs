using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLookNext.Plugin.TextViewer.Themes.HighlightingDefinitions.Light;

/// <summary>
/// Rainbow TSV Highlighting Definition
/// </summary>
public class TSVHighlightingDefinition : LightHighlightingDefinition
{
    public override string Name => "TSV";

    public override string Extension => ".tsv";

    public override DocumentColorizingTransformer[] LineTransformers { get; } = [new CSVHighlightingDefinition.RainbowTransformer('\t')];
}
