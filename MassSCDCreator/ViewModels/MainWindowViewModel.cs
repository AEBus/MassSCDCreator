using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
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
        Steps = [
            new WizardStepItemViewModel { Step = WizardStep.Workflow, Number = 1, TitleKey = "StepWorkflowTitle", DescriptionKey = "StepWorkflowDesc" },
            new WizardStepItemViewModel { Step = WizardStep.Paths, Number = 2, TitleKey = "StepPathsTitle", DescriptionKey = "StepPathsDesc" },
            new WizardStepItemViewModel { Step = WizardStep.TemplateAudio, Number = 3, TitleKey = "StepTemplateTitle", DescriptionKey = "StepTemplateDesc" },
            new WizardStepItemViewModel { Step = WizardStep.Penumbra, Number = 4, TitleKey = "StepPenumbraTitle", DescriptionKey = "StepPenumbraDesc" },
            new WizardStepItemViewModel { Step = WizardStep.Review, Number = 5, TitleKey = "StepReviewTitle", DescriptionKey = "StepReviewDesc" },
            new WizardStepItemViewModel { Step = WizardStep.Result, Number = 6, TitleKey = "StepResultTitle", DescriptionKey = "StepResultDesc" }
        ];

        PropertyChanged += HandlePropertyChanged;
        ExistingPenumbraPlaylistSummary = Texts["ExistingPlaylistSummaryEmpty"];
        StatusText = Texts["StatusReady"];
        SummaryText = Texts["SummaryNoOperations"];
        ResultHeadline = Texts["ResultIdleTitle"];
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
    public ObservableCollection<WizardStepItemViewModel> Steps { get; }
    public ObservableCollection<string> WorkflowIssues { get; } = [];
    public ObservableCollection<string> PathsIssues { get; } = [];
    public ObservableCollection<string> TemplateIssues { get; } = [];
    public ObservableCollection<string> PenumbraIssues { get; } = [];
    public ObservableCollection<string> ReviewIssues { get; } = [];

    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private WizardStep currentStep = WizardStep.Workflow;
    [ObservableProperty] private ProcessingMode selectedMode = ProcessingMode.SingleFile;
    [ObservableProperty] private ThemeMode selectedThemeMode = ThemeMode.System;
    [ObservableProperty] private string inputPath = string.Empty;
    [ObservableProperty] private string outputPath = string.Empty;
    [ObservableProperty] private bool recursiveSearchEnabled;
    [ObservableProperty] private TemplateSourceMode selectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended;
    [ObservableProperty] private string templateScdPath = string.Empty;
    [ObservableProperty] private string ffmpegPath = string.Empty;
    [ObservableProperty] private string ffmpegInstallStatus = string.Empty;
    [ObservableProperty] private bool isInstallingFfmpeg;
    [ObservableProperty] private bool showAdvancedAudioOptions;
    [ObservableProperty] private bool usePresetMode = true;
    [ObservableProperty] private OggPresetMode selectedPresetMode = OggPresetMode.HighQualityCompatible;
    [ObservableProperty] private OggAdvancedMode selectedAdvancedMode = OggAdvancedMode.QualityVbr;
    [ObservableProperty] private string advancedValue = "9";
    [ObservableProperty] private ExistingScdRefreshAction selectedExistingScdRefreshAction = ExistingScdRefreshAction.MatchTemplateOnly;
    [ObservableProperty] private bool enableLoop;
    [ObservableProperty] private bool saveIntermediateOggFiles;
    [ObservableProperty] private bool penumbraExportEnabled;
    [ObservableProperty] private string penumbraModRootPath = string.Empty;
    [ObservableProperty] private PenumbraPlaylistExportMode selectedPenumbraExportMode = PenumbraPlaylistExportMode.CreateNew;
    [ObservableProperty] private string penumbraPlaylistName = string.Empty;
    [ObservableProperty] private string existingPenumbraPlaylistPath = string.Empty;
    [ObservableProperty] private string existingPenumbraPlaylistSummary = string.Empty;
    [ObservableProperty] private string inferredRelativeFolderWarning = string.Empty;
    [ObservableProperty] private string penumbraRelativeScdFolder = "MyPlaylist\\Tracks";
    [ObservableProperty] private string penumbraGamePathsText = "sound/your_playlist_track.scd";
    [ObservableProperty] private double progressPercent;
    [ObservableProperty] private string progressText = "0 / 0";
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string summaryText = string.Empty;
    [ObservableProperty] private string resultHeadline = string.Empty;
    [ObservableProperty] private string resultOutputFolderPath = string.Empty;
    [ObservableProperty] private string resultPlaylistPath = string.Empty;
    [ObservableProperty] private bool isLogExpanded;
    [ObservableProperty] private string logText = string.Empty;

    public bool IsSingleMode => SelectedMode == ProcessingMode.SingleFile;
    public bool IsBatchMode => SelectedMode == ProcessingMode.BatchFolder;
    public bool IsRefreshMode => SelectedMode == ProcessingMode.RepairScdFolder;
    public bool IsWorkflowStepActive => CurrentStep == WizardStep.Workflow;
    public bool IsPathsStepActive => CurrentStep == WizardStep.Paths;
    public bool IsTemplateAudioStepActive => CurrentStep == WizardStep.TemplateAudio;
    public bool IsPenumbraStepActive => CurrentStep == WizardStep.Penumbra;
    public bool IsReviewStepActive => CurrentStep == WizardStep.Review;
    public bool IsResultStepActive => CurrentStep == WizardStep.Result;
    public bool ShowPenumbraStep => !IsRefreshMode;
    public bool ShowOutputSelection => !IsRefreshMode;
    public bool ShowTemplateCurrentOption => IsRefreshMode;
    public bool ShowTemplatePathSelection => SelectedTemplateSourceMode == TemplateSourceMode.CustomFile;
    public bool ShowRefreshActionSelection => IsRefreshMode;
    public bool ShowAudioEncodingOptions => !IsRefreshMode || SelectedExistingScdRefreshAction != ExistingScdRefreshAction.MatchTemplateOnly;
    public bool ShowFfmpegStatus => !string.IsNullOrWhiteSpace( FfmpegInstallStatus );
    public bool ShowPenumbraSettings => ShowPenumbraStep && PenumbraExportEnabled;
    public bool ShowCreatePlaylistFields => ShowPenumbraSettings && SelectedPenumbraExportMode == PenumbraPlaylistExportMode.CreateNew;
    public bool ShowAppendPlaylistFields => ShowPenumbraSettings && SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting;
    public bool ShowExistingPlaylistSummary => ShowAppendPlaylistFields && !string.IsNullOrWhiteSpace( ExistingPenumbraPlaylistPath );
    public bool ShowInferredRelativeFolderWarning => ShowAppendPlaylistFields && !string.IsNullOrWhiteSpace( InferredRelativeFolderWarning );
    public bool ShowIntermediateOggOption => !IsRefreshMode;
    public bool IsBuiltInTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.BuiltInRecommended;
    public bool IsCustomTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.CustomFile;
    public bool IsCurrentTemplateSelected => SelectedTemplateSourceMode == TemplateSourceMode.CurrentFile;
    public bool IsCompatiblePresetSelected => SelectedPresetMode == OggPresetMode.HighQualityCompatible;
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
    public bool HasReviewIssues => ReviewIssues.Count > 0;
    public bool CanToggleLog => !string.IsNullOrWhiteSpace( LogText );
    public string InputLabel => IsBatchMode ? Texts["InputLabelBatch"] : IsRefreshMode ? Texts["InputLabelRefresh"] : Texts["InputLabelSingle"];
    public string OutputLabel => IsBatchMode ? Texts["OutputLabelBatch"] : Texts["OutputLabelSingle"];
    public string AdvancedModeHint => SelectedAdvancedMode == OggAdvancedMode.QualityVbr ? Texts["ValidationQualityNumber"] : Texts["ValidationBitrateNumber"];
    public string SummaryModeValue => IsBatchMode ? Texts["ModeBatchTitle"] : IsRefreshMode ? Texts["ModeRefreshTitle"] : Texts["ModeSingleTitle"];
    public string SummaryInputValue => string.IsNullOrWhiteSpace( InputPath ) ? Texts["SummaryNotConfigured"] : InputPath;
    public string SummaryOutputValue => IsRefreshMode ? Texts["RefreshInPlaceHint"] : string.IsNullOrWhiteSpace( OutputPath ) ? Texts["SummaryNotConfigured"] : OutputPath;
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
    public string SummaryPenumbraValue => !ShowPenumbraStep || !PenumbraExportEnabled ? Texts["PenumbraOff"] : SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting ? Texts["PenumbraAppendSummary"] : Texts["PenumbraCreateSummary"];
    public string SummaryValidationValue => ReviewIssues.Count == 0 ? Texts["ValidationReady"] : $"{Texts["ValidationAttention"]}: {ReviewIssues.Count}";
    public string ReviewChecklistText => BuildReviewChecklistText();

    private string BuildAudioEncodingSummary( string? prefix = null ) {
        var details = UsePresetMode
            ? Texts["AudioSummaryPresetCompatible"]
            : SelectedAdvancedMode == OggAdvancedMode.NominalBitrate ? Texts.Format( "AudioSummaryAdvancedBitrate", AdvancedValue ) : Texts.Format( "AudioSummaryAdvancedQuality", AdvancedValue );
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
