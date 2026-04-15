namespace MassSCDCreator.Models;

public sealed class AppSettings {
    public ProcessingMode SelectedMode { get; set; } = ProcessingMode.SingleFile;
    public ThemeMode SelectedThemeMode { get; set; } = ThemeMode.System;
    public TemplateSourceMode SelectedTemplateSourceMode { get; set; } = TemplateSourceMode.BuiltInRecommended;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string TemplateScdPath { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;
    public bool SkipFfmpegStartupCheck { get; set; }
    public bool BatchRecursiveSearchEnabled { get; set; }
    public bool RefreshRecursiveSearchEnabled { get; set; }
    public bool SaveIntermediateOggFiles { get; set; }
    public bool EnableLoop { get; set; }
    public AudioProfileMode? SelectedAudioProfileMode { get; set; }

    // Legacy fields kept for backward-compatible settings migration.
    public bool UsePresetMode { get; set; } = true;
    public OggPresetMode SelectedPresetMode { get; set; } = OggPresetMode.HighQualityCompatible;

    public OggAdvancedMode SelectedAdvancedMode { get; set; } = OggAdvancedMode.QualityVbr;
    public string AdvancedValue { get; set; } = "9";
    public ExistingScdRefreshAction SelectedExistingScdRefreshAction { get; set; } = ExistingScdRefreshAction.MatchTemplateOnly;
    public bool PenumbraExportEnabled { get; set; }
    public string PenumbraModRootPath { get; set; } = string.Empty;
    public PenumbraPlaylistExportMode SelectedPenumbraExportMode { get; set; } = PenumbraPlaylistExportMode.CreateNew;
    public string PenumbraPlaylistName { get; set; } = string.Empty;
    public string ExistingPenumbraPlaylistPath { get; set; } = string.Empty;
    public string PenumbraCreateRelativeScdFolder { get; set; } = "MyPlaylist\\Tracks";
    public string PenumbraAppendRelativeScdFolder { get; set; } = "MyPlaylist\\Tracks";
    public string PenumbraCreateGamePathsText { get; set; } = "sound/your_playlist_track.scd";
    public string PenumbraAppendGamePathsText { get; set; } = "sound/your_playlist_track.scd";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool IsWindowMaximized { get; set; }
}
