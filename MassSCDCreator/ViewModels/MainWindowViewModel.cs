using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MassSCDCreator.Localization;
using MassSCDCreator.Models;
using MassSCDCreator.Services.Audio;
using MassSCDCreator.Services.Dialogs;
using MassSCDCreator.Services.Logging;
using MassSCDCreator.Services.Processing;
using MassSCDCreator.Services.Settings;

namespace MassSCDCreator.ViewModels;

public partial class MainWindowViewModel : ObservableObject {
    private const string ExpandedChevron = "\u25BE";
    private const string CollapsedChevron = "\u25B8";
    private const string SummarySeparator = " \u00B7 ";

    private readonly IFileDialogService _dialogService;
    private readonly IFileBatchProcessor _batchProcessor;
    private readonly ILoggerService _logger;
    private readonly IFfmpegInstaller? _ffmpegInstaller;
    private readonly ISettingsService? _settingsService;
    private readonly Action<ThemeMode>? _applyTheme;
    private readonly StringBuilder _logBuilder = new();
    private readonly Queue<string> _pendingLogLines = new();
    private readonly object _pendingLogSync = new();
    private readonly DispatcherTimer _logFlushTimer;
    private CancellationTokenSource? _cts;
    private bool _suspendReactions;
    private bool _settingsSaveFailureReported;
    private string _lastSuggestedOutputPath = string.Empty;
    private bool _outputPathManagedByWizard = true;
    private double? _windowLeft;
    private double? _windowTop;
    private double? _windowWidth;
    private double? _windowHeight;
    private bool _isWindowMaximized;

    public MainWindowViewModel( IFileDialogService dialogService, IFileBatchProcessor batchProcessor, ILoggerService logger, IFfmpegInstaller? ffmpegInstaller = null ) {
        _dialogService = dialogService;
        _batchProcessor = batchProcessor;
        _logger = logger;
        _ffmpegInstaller = ffmpegInstaller;
        _logger.EntryLogged += OnEntryLogged;
        _logFlushTimer = new DispatcherTimer( DispatcherPriority.Background, Application.Current.Dispatcher ) {
            Interval = TimeSpan.FromMilliseconds( 120 )
        };
        _logFlushTimer.Tick += ( _, _ ) => FlushPendingLogEntries();

        Texts = new UiTextCatalog();

        PropertyChanged += HandlePropertyChanged;
        StatusText = Texts["StatusReady"];
        SummaryText = Texts["SummaryNoOperations"];
        RefreshState();
    }

    public MainWindowViewModel(
        IFileDialogService dialogService,
        IFileBatchProcessor batchProcessor,
        ILoggerService logger,
        ISettingsService settingsService,
        Action<ThemeMode> applyTheme,
        IFfmpegInstaller? ffmpegInstaller = null )
        : this( dialogService, batchProcessor, logger, ffmpegInstaller ) {
        _settingsService = settingsService;
        _applyTheme = applyTheme;
        LoadSettings();
        RefreshState();
    }

    public UiTextCatalog Texts { get; }
    public ObservableCollection<string> PathsIssues { get; } = [];
    public ObservableCollection<string> TemplateIssues { get; } = [];
    public ObservableCollection<string> PenumbraIssues { get; } = [];
    public ObservableCollection<string> BlockingIssues { get; } = [];
    public ObservableCollection<PenumbraGamePathCandidate> PenumbraGamePathCandidates { get; } = [];
    public ObservableCollection<PenumbraPlaylistCandidate> PenumbraPlaylistCandidates { get; } = [];

    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private bool hasRun;
    [ObservableProperty] private ProcessingMode selectedMode = ProcessingMode.SingleFile;
    [ObservableProperty] private ThemeMode selectedThemeMode = ThemeMode.System;
    [ObservableProperty] private string inputPath = string.Empty;
    [ObservableProperty] private string outputPath = string.Empty;
    [ObservableProperty] private bool useCustomOutputPath;
    [ObservableProperty] private bool recursiveSearchEnabled;
    [ObservableProperty] private TemplateSourceMode selectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended;
    [ObservableProperty] private string templateScdPath = string.Empty;
    [ObservableProperty] private string ffmpegPath = string.Empty;
    [ObservableProperty] private string ffmpegInstallStatus = string.Empty;
    [ObservableProperty] private bool isInstallingFfmpeg;
    [ObservableProperty] private AudioProfileMode selectedAudioProfileMode = AudioProfileMode.Recommended;
    [ObservableProperty] private OggAdvancedMode selectedAdvancedMode = OggAdvancedMode.QualityVbr;
    [ObservableProperty] private string advancedValue = "9";
    [ObservableProperty] private ExistingScdRefreshAction selectedExistingScdRefreshAction = ExistingScdRefreshAction.MatchTemplateOnly;
    [ObservableProperty] private bool enableLoop;
    [ObservableProperty] private bool normalizeLoudness = true;
    [ObservableProperty] private bool saveIntermediateOggFiles;
    [ObservableProperty] private bool penumbraExportEnabled;
    [ObservableProperty] private string penumbraModRootPath = string.Empty;
    [ObservableProperty] private PenumbraPlaylistExportMode selectedPenumbraExportMode = PenumbraPlaylistExportMode.CreateNew;
    [ObservableProperty] private string penumbraPlaylistName = string.Empty;
    [ObservableProperty] private string existingPenumbraPlaylistPath = string.Empty;
    [ObservableProperty] private string inferredRelativeFolderWarning = string.Empty;
    [ObservableProperty] private string penumbraRelativeScdFolder = "MyPlaylist\\Tracks";
    [ObservableProperty] private string penumbraGamePathsText = "sound/your_playlist_track.scd";
    [ObservableProperty] private PenumbraGamePathCandidate? selectedPenumbraGamePathCandidate;
    [ObservableProperty] private PenumbraPlaylistCandidate? selectedPenumbraPlaylistCandidate;
    [ObservableProperty] private double progressPercent;
    [ObservableProperty] private string progressText = "0 / 0";
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string summaryText = string.Empty;
    [ObservableProperty] private string resultOutputFolderPath = string.Empty;
    [ObservableProperty] private string resultPlaylistPath = string.Empty;
    [ObservableProperty] private bool isAudioExpanded;
    [ObservableProperty] private bool isPenumbraExpanded;
    [ObservableProperty] private bool isLogExpanded;
    [ObservableProperty] private string logText = string.Empty;

    public bool IsSingleMode => SelectedMode == ProcessingMode.SingleFile;
    public bool IsBatchMode => SelectedMode == ProcessingMode.BatchFolder;
    public bool IsRefreshMode => SelectedMode == ProcessingMode.RepairScdFolder;
    public bool ShowPenumbraSection => !IsRefreshMode;
    public bool ShowOutputOption => !IsRefreshMode;
    public bool ShowOutputSelection => !IsRefreshMode && UseCustomOutputPath;
    public bool ShowOutputGoesToPenumbra => ShowOutputOption && !UseCustomOutputPath;
    public bool ShowTemplateCurrentOption => IsRefreshMode;
    public bool ShowTemplatePathSelection => SelectedTemplateSourceMode == TemplateSourceMode.CustomFile;
    public bool ShowRefreshActionSelection => IsRefreshMode;
    public bool ShowAudioEncodingOptions => !IsRefreshMode || SelectedExistingScdRefreshAction != ExistingScdRefreshAction.MatchTemplateOnly;
    public bool ShowFfmpegOptions => ShowAudioEncodingOptions && SelectedAudioProfileMode != AudioProfileMode.OriginalOgg;
    public bool ShowCustomAudioOptions => ShowAudioEncodingOptions && SelectedAudioProfileMode == AudioProfileMode.Custom;
    public bool ShowOriginalOggHint => ShowAudioEncodingOptions && SelectedAudioProfileMode == AudioProfileMode.OriginalOgg;
    public bool ShowFfmpegStatus => ShowFfmpegOptions && !string.IsNullOrWhiteSpace( FfmpegInstallStatus );
    public bool ShowPenumbraSettings => ShowPenumbraSection && PenumbraExportEnabled;
    public bool ShowCreatePlaylistFields => ShowPenumbraSettings && SelectedPenumbraExportMode == PenumbraPlaylistExportMode.CreateNew;
    public bool ShowAppendPlaylistFields => ShowPenumbraSettings && SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting;
    public bool HasPenumbraPlaylistCandidates => PenumbraPlaylistCandidates.Count > 0;
    public bool ShowNoPlaylistsFound => ShowAppendPlaylistFields && !string.IsNullOrWhiteSpace( PenumbraModRootPath ) && PenumbraPlaylistCandidates.Count == 0;
    public bool ShowInferredRelativeFolderWarning => ShowAppendPlaylistFields && !string.IsNullOrWhiteSpace( InferredRelativeFolderWarning );
    public bool ShowPenumbraGamePathCandidates => ShowPenumbraSettings && PenumbraGamePathCandidates.Count > 0;
    public bool ShowIntermediateOggOption => !IsRefreshMode && UseCustomOutputPath;
    public bool ShowResultActions => HasRun && !IsProcessing;
    public bool IsBuiltInTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.BuiltInRecommended;
    public bool IsCustomTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.CustomFile;
    public bool IsCurrentTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.CurrentFile;
    public bool IsRecommendedAudioProfileSelected => SelectedAudioProfileMode == AudioProfileMode.Recommended;
    public bool IsCustomAudioProfileSelected => SelectedAudioProfileMode == AudioProfileMode.Custom;
    public bool IsOriginalOggAudioProfileSelected => SelectedAudioProfileMode == AudioProfileMode.OriginalOgg;
    public bool IsAdvancedQualityModeSelected => SelectedAdvancedMode == OggAdvancedMode.QualityVbr;
    public bool IsAdvancedBitrateModeSelected => SelectedAdvancedMode == OggAdvancedMode.NominalBitrate;
    public bool IsSystemThemeSelected => SelectedThemeMode == ThemeMode.System;
    public bool IsLightThemeSelected => SelectedThemeMode == ThemeMode.Light;
    public bool IsDarkThemeSelected => SelectedThemeMode == ThemeMode.Dark;
    public bool IsRefreshTemplateOnlySelected => SelectedExistingScdRefreshAction == ExistingScdRefreshAction.MatchTemplateOnly;
    public bool IsRefreshAudioOnlySelected => SelectedExistingScdRefreshAction == ExistingScdRefreshAction.ReencodeAudioOnly;
    public bool IsRefreshTemplateAndAudioSelected => SelectedExistingScdRefreshAction == ExistingScdRefreshAction.MatchTemplateAndReencodeAudio;
    public bool IsCreatePlaylistModeSelected => SelectedPenumbraExportMode == PenumbraPlaylistExportMode.CreateNew;
    public bool IsAppendPlaylistModeSelected => SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting;
    public bool HasPathsIssues => PathsIssues.Count > 0;
    public bool HasTemplateIssues => TemplateIssues.Count > 0;
    public bool HasPenumbraIssues => PenumbraIssues.Count > 0;
    public bool HasBlockingIssues => BlockingIssues.Count > 0;

    public string AudioChevron => IsAudioExpanded ? ExpandedChevron : CollapsedChevron;
    public string PenumbraChevron => IsPenumbraExpanded ? ExpandedChevron : CollapsedChevron;
    public string LogChevron => IsLogExpanded ? ExpandedChevron : CollapsedChevron;

    public string InputLabel => IsBatchMode ? Texts["InputLabelBatch"] : IsRefreshMode ? Texts["InputLabelRefresh"] : Texts["InputLabelSingle"];
    public string OutputLabel => IsBatchMode ? Texts["OutputLabelBatch"] : Texts["OutputLabelSingle"];
    public string AdvancedModeHint => SelectedAdvancedMode == OggAdvancedMode.QualityVbr
        ? Texts["AudioCustomValueHintQuality"]
        : Texts["AudioCustomValueHintBitrate"];

    public string LogSummary => string.IsNullOrWhiteSpace( LogText )
        ? Texts["LogEmpty"]
        : Texts.Format( "LogLineCount", LogText.Count( character => character == '\n' ) + 1 );

    public string ActionBarDetailText {
        get {
            if( IsProcessing ) {
                return ProgressText;
            }

            return BlockingIssues.Count > 0 ? BlockingIssues[0] : SummaryText;
        }
    }

    // Validation takes precedence over the previous run's status when Start is blocked.
    public string ActionBarHeadlineText {
        get {
            if( IsProcessing ) {
                return StatusText;
            }

            return BlockingIssues.Count > 0 ? Texts["StatusNotReady"] : StatusText;
        }
    }

    public double AdvancedQualitySliderValue {
        get {
            if( !double.TryParse( AdvancedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue ) ) {
                return 9.0;
            }

            return Math.Clamp( parsedValue, 1.0, 10.0 );
        }
        set {
            var clamped = Math.Clamp( value, 1.0, 10.0 );
            var normalized = clamped.ToString( "0.0", CultureInfo.InvariantCulture );
            if( !string.Equals( AdvancedValue, normalized, StringComparison.Ordinal ) ) {
                AdvancedValue = normalized;
            }
        }
    }

    public string SummaryTemplateValue => SelectedTemplateSourceMode switch {
        TemplateSourceMode.CustomFile => string.IsNullOrWhiteSpace( TemplateScdPath ) ? Texts["TemplateSummaryCustom"] : TemplateScdPath,
        TemplateSourceMode.CurrentFile => Texts["TemplateSummaryCurrent"],
        _ => Texts["TemplateSummaryBuiltIn"]
    };
    public string SummaryAudioValue => IsRefreshMode
        ? SelectedExistingScdRefreshAction switch {
            ExistingScdRefreshAction.MatchTemplateOnly => Texts["RefreshActionSummaryTemplateOnly"],
            ExistingScdRefreshAction.ReencodeAudioOnly => BuildAudioEncodingSummary( Texts["RefreshActionSummaryAudioOnly"] ),
            _ => BuildAudioEncodingSummary( Texts["RefreshActionSummaryTemplateAndAudio"] )
        }
        : BuildAudioEncodingSummary();
    public string SummaryPenumbraValue => !ShowPenumbraSection || !PenumbraExportEnabled ? Texts["PenumbraOff"] : SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting ? Texts["PenumbraAppendSummary"] : Texts["PenumbraCreateSummary"];
    public string AudioSectionSummary => SummaryTemplateValue + SummarySeparator + SummaryAudioValue;

    private string BuildAudioEncodingSummary( string? prefix = null ) {
        var details = SelectedAudioProfileMode switch {
            AudioProfileMode.Custom when SelectedAdvancedMode == OggAdvancedMode.NominalBitrate => Texts.Format( "AudioSummaryCustomBitrate", AdvancedValue ),
            AudioProfileMode.Custom => Texts.Format( "AudioSummaryCustomQuality", AdvancedValue ),
            AudioProfileMode.OriginalOgg => Texts["AudioSummaryOriginalOgg"],
            _ => Texts["AudioSummaryRecommended"]
        };

        return string.IsNullOrWhiteSpace( prefix ) ? details : $"{prefix} | {details}";
    }

    public bool TryGetWindowPlacement( out double left, out double top, out double width, out double height, out bool isMaximized ) {
        if( _windowLeft.HasValue && _windowTop.HasValue && _windowWidth.HasValue && _windowHeight.HasValue ) {
            left = _windowLeft.Value;
            top = _windowTop.Value;
            width = _windowWidth.Value;
            height = _windowHeight.Value;
            isMaximized = _isWindowMaximized;
            return true;
        }

        left = 0;
        top = 0;
        width = 0;
        height = 0;
        isMaximized = false;
        return false;
    }

    public void UpdateWindowPlacement( double left, double top, double width, double height, bool isMaximized ) {
        _windowLeft = left;
        _windowTop = top;
        _windowWidth = width;
        _windowHeight = height;
        _isWindowMaximized = isMaximized;
        SaveSettings();
    }
}
