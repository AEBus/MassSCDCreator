namespace MassSCDCreator.Models;

public sealed class OggConversionOptions {
    public ExistingScdRefreshAction ExistingScdRefreshAction { get; init; } = ExistingScdRefreshAction.MatchTemplateOnly;
    public bool UsePresetMode { get; init; } = false;
    public OggPresetMode PresetMode { get; init; } = OggPresetMode.HighQualityCompatible;
    public OggAdvancedMode AdvancedMode { get; init; } = OggAdvancedMode.QualityVbr;
    public double QualityLevel { get; init; } = 7.0;
    public int NominalBitrateKbps { get; init; } = 320;
    public bool NormalizeLoudness { get; init; } = true;
    public bool EnableLoop { get; init; }
    public bool SaveIntermediateOggFiles { get; init; }
    public bool RecursiveSearchEnabled { get; init; }
    public string? FfmpegPath { get; init; }
}
