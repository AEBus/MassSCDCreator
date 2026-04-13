namespace MassSCDCreator.Models;

public sealed class ProcessRequest {
    public required ProcessingMode Mode { get; init; }
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required TemplateSourceMode TemplateSourceMode { get; init; }
    public string TemplateScdPath { get; init; } = string.Empty;
    public required OggConversionOptions Conversion { get; init; }
    public required PenumbraExportOptions PenumbraExport { get; init; }
}
