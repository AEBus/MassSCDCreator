using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MassSCDCreator.Models;
using MassSCDCreator.Services.Logging;

namespace MassSCDCreator.ViewModels;

public partial class MainWindowViewModel {
    private static readonly HashSet<string> SupportedAudioExtensions = [
        ".mp3", ".flac", ".ogg", ".m4a", ".wav", ".aac", ".wma", ".opus", ".aiff", ".aif", ".mp4", ".m4b"
    ];

    [RelayCommand( CanExecute = nameof( CanStartProcessing ) )]
    private async Task StartAsync() {
        try {
            var request = BuildRequest();
            IsProcessing = true;
            CurrentStep = WizardStep.Result;
            IsLogExpanded = true;
            ProgressPercent = 0;
            ProgressText = "0 / 0";
            StatusText = Texts["StatusRunning"];
            SummaryText = Texts["SummaryProcessing"];
            ResultHeadline = Texts["ResultRunningTitle"];
            ResultOutputFolderPath = ResolveOutputFolder( request );
            ResultPlaylistPath = string.Empty;
            ClearLog();

            _cts = new CancellationTokenSource();
            var progress = new Progress<ProcessingProgress>( value => {
                ProgressPercent = value.Percent;
                ProgressText = $"{value.CurrentIndex} / {value.TotalCount} | {value.Stage} | {Path.GetFileName( value.CurrentFile )}";
                StatusText = value.Stage;
            } );

            var result = await Task.Run(
                () => _batchProcessor.ProcessAsync( request, progress, _cts.Token ),
                _cts.Token );
            ResultPlaylistPath = result.ExportedPlaylistPath ?? string.Empty;
            SummaryText = BuildResultSummary( result );
            StatusText = result.WasCancelled ? Texts["StatusCancelled"] : Texts["StatusCompleted"];
            ResultHeadline = result.WasCancelled
                ? Texts["ResultCancelledTitle"]
                : result.ErrorCount == 0
                    ? Texts["ResultSuccessTitle"]
                    : Texts["ResultPartialTitle"];
        }
        catch( OperationCanceledException ) {
            SummaryText = Texts["ResultCancelledTitle"];
            StatusText = Texts["StatusCancelled"];
            ResultHeadline = Texts["ResultCancelledTitle"];
            _logger.Info( "Operation cancelled by user." );
        }
        catch( Exception ex ) {
            SummaryText = ex.Message;
            StatusText = Texts["StatusError"];
            ResultHeadline = Texts["ResultErrorTitle"];
            _logger.Error( ex.ToString() );
            MessageBox.Show( ex.Message, Texts["WindowTitle"], MessageBoxButton.OK, MessageBoxImage.Error );
        }
        finally {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
            RefreshState();
        }
    }

    private bool CanStartProcessing() => !IsProcessing && CurrentStep == WizardStep.Review && ReviewIssues.Count == 0;

    [RelayCommand( CanExecute = nameof( CanCancel ) )]
    private void Cancel() {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsProcessing;

    private void RefreshValidationIssues() {
        ReplaceIssues( WorkflowIssues );
        ReplaceIssues( PathsIssues, GetPathIssues() );
        ReplaceIssues( TemplateIssues, GetTemplateIssues() );
        ReplaceIssues( PenumbraIssues, GetPenumbraIssues() );

        var reviewIssues = new List<string>();
        reviewIssues.AddRange( PathsIssues );
        reviewIssues.AddRange( TemplateIssues );
        if( ShowPenumbraStep ) {
            reviewIssues.AddRange( PenumbraIssues );
        }

        ReplaceIssues( ReviewIssues, reviewIssues );
    }

    private IEnumerable<string> GetPathIssues() {
        if( string.IsNullOrWhiteSpace( InputPath ) ) {
            yield return Texts["ValidationInputRequired"];
            yield break;
        }

        if( IsSingleMode ) {
            if( !File.Exists( InputPath ) ) {
                yield return Texts["ValidationInputFileMissing"];
            }
            else if( ShowAudioEncodingOptions && SelectedAudioProfileMode == AudioProfileMode.OriginalOgg && !IsOggFilePath( InputPath ) ) {
                yield return Texts["ValidationOriginalOggSingleInputOnly"];
            }
        }
        else if( !Directory.Exists( InputPath ) ) {
            yield return IsRefreshMode ? Texts["ValidationRefreshFolderMissing"] : Texts["ValidationInputFolderMissing"];
        }
        else if( IsBatchMode && ShowAudioEncodingOptions && SelectedAudioProfileMode == AudioProfileMode.OriginalOgg ) {
            if( !HasAnyOggInputFile( InputPath ) ) {
                yield return Texts["ValidationOriginalOggBatchNoOggFiles"];
            }
        }

        if( !ShowOutputSelection ) {
            yield break;
        }

        if( string.IsNullOrWhiteSpace( OutputPath ) ) {
            yield return Texts["ValidationOutputRequired"];
            yield break;
        }

        var directory = IsSingleMode ? Path.GetDirectoryName( OutputPath ) : OutputPath;
        if( string.IsNullOrWhiteSpace( directory ) ) {
            yield return Texts["ValidationOutputFolderMissing"];
        }
    }

    private IEnumerable<string> GetTemplateIssues() {
        if( SelectedTemplateSourceMode == TemplateSourceMode.CustomFile ) {
            if( string.IsNullOrWhiteSpace( TemplateScdPath ) ) {
                yield return Texts["ValidationTemplatePathRequired"];
            }
            else if( !File.Exists( TemplateScdPath ) ) {
                yield return Texts["ValidationTemplateFileMissing"];
            }
        }

        if( !ShowAudioEncodingOptions ) {
            yield break;
        }

        if( IsFfmpegRequiredForSelectedAudioProfile() && !string.IsNullOrWhiteSpace( FfmpegPath ) && !File.Exists( FfmpegPath ) ) {
            yield return Texts["ValidationFfmpegFileMissing"];
        }

        if( SelectedAudioProfileMode != AudioProfileMode.Custom ) {
            yield break;
        }

        if( SelectedAdvancedMode == OggAdvancedMode.QualityVbr ) {
            if( !double.TryParse( AdvancedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _ ) ) {
                yield return Texts["ValidationQualityNumber"];
            }

            yield break;
        }

        if( !int.TryParse( AdvancedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _ ) ) {
            yield return Texts["ValidationBitrateNumber"];
        }
    }

    private IEnumerable<string> GetPenumbraIssues() {
        if( !ShowPenumbraStep || !PenumbraExportEnabled ) {
            yield break;
        }

        if( string.IsNullOrWhiteSpace( PenumbraModRootPath ) ) {
            yield return Texts["ValidationModRootRequired"];
        }

        if( SelectedPenumbraExportMode == PenumbraPlaylistExportMode.CreateNew && string.IsNullOrWhiteSpace( PenumbraPlaylistName ) ) {
            yield return Texts["ValidationPlaylistNameRequired"];
        }

        if( SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting && string.IsNullOrWhiteSpace( ExistingPenumbraPlaylistPath ) ) {
            yield return Texts["ValidationExistingPlaylistRequired"];
        }

        if( ParseGamePaths().Count == 0 ) {
            yield return Texts["ValidationGamePathsRequired"];
        }
    }

    private ProcessRequest BuildRequest() {
        if( ReviewIssues.Count > 0 ) {
            throw new InvalidOperationException( ReviewIssues[0] );
        }

        return new ProcessRequest {
            Mode = SelectedMode,
            InputPath = InputPath.Trim(),
            OutputPath = IsRefreshMode ? string.Empty : OutputPath.Trim(),
            TemplateSourceMode = SelectedTemplateSourceMode,
            TemplateScdPath = SelectedTemplateSourceMode == TemplateSourceMode.CustomFile ? TemplateScdPath.Trim() : string.Empty,
            Conversion = new OggConversionOptions {
                ExistingScdRefreshAction = SelectedExistingScdRefreshAction,
                AudioProfileMode = SelectedAudioProfileMode,
                AdvancedMode = SelectedAudioProfileMode == AudioProfileMode.Custom ? SelectedAdvancedMode : OggAdvancedMode.QualityVbr,
                QualityLevel = ParseQuality(),
                NominalBitrateKbps = ParseNominalBitrate(),
                EnableLoop = EnableLoop,
                SaveIntermediateOggFiles = SaveIntermediateOggFiles,
                RecursiveSearchEnabled = RecursiveSearchEnabled,
                FfmpegPath = IsFfmpegRequiredForSelectedAudioProfile() && !string.IsNullOrWhiteSpace( FfmpegPath ) ? FfmpegPath.Trim() : null
            },
            PenumbraExport = new PenumbraExportOptions {
                Enabled = ShowPenumbraStep && PenumbraExportEnabled,
                ModRootPath = PenumbraModRootPath.Trim(),
                ExportMode = SelectedPenumbraExportMode,
                PlaylistName = PenumbraPlaylistName.Trim(),
                ExistingPlaylistPath = ExistingPenumbraPlaylistPath.Trim(),
                RelativeScdFolder = string.IsNullOrWhiteSpace( PenumbraRelativeScdFolder ) ? "MyPlaylist\\Tracks" : PenumbraRelativeScdFolder.Trim(),
                GamePaths = ParseGamePaths()
            }
        };
    }

    private double ParseQuality() {
        if( SelectedAudioProfileMode != AudioProfileMode.Custom || SelectedAdvancedMode != OggAdvancedMode.QualityVbr ) {
            return 7.0;
        }

        if( !double.TryParse( AdvancedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result ) ) {
            throw new InvalidOperationException( Texts["ValidationQualityNumber"] );
        }

        return Math.Clamp( result, 1.0, 10.0 );
    }

    private int ParseNominalBitrate() {
        if( SelectedAudioProfileMode != AudioProfileMode.Custom || SelectedAdvancedMode != OggAdvancedMode.NominalBitrate ) {
            return 320;
        }

        if( !int.TryParse( AdvancedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result ) ) {
            throw new InvalidOperationException( Texts["ValidationBitrateNumber"] );
        }

        return result;
    }

    private bool IsFfmpegRequiredForSelectedAudioProfile() =>
        ShowAudioEncodingOptions && SelectedAudioProfileMode != AudioProfileMode.OriginalOgg;


    private bool HasAnyOggInputFile( string inputFolder ) {
        var option = RecursiveSearchEnabled ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles( inputFolder, "*.*", option )
            .Any( path => IsSupportedAudioPath( path ) && IsOggFilePath( path ) );
    }

    private static bool IsSupportedAudioPath( string path ) =>
        SupportedAudioExtensions.Contains( Path.GetExtension( path ) );

    private static bool IsOggFilePath( string path ) =>
        string.Equals( Path.GetExtension( path ), ".ogg", StringComparison.OrdinalIgnoreCase );

    private static AudioProfileMode ResolveAudioProfileModeFromSettings( AppSettings settings ) {
        if( settings.SelectedAudioProfileMode.HasValue ) {
            return settings.SelectedAudioProfileMode.Value;
        }

        return settings.UsePresetMode ? AudioProfileMode.Recommended : AudioProfileMode.Custom;
    }

    private IReadOnlyList<string> ParseGamePaths() {
        return PenumbraGamePathsText
            .Split( ["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries )
            .Select( path => path.Trim() )
            .Where( path => !string.IsNullOrWhiteSpace( path ) )
            .Distinct( StringComparer.OrdinalIgnoreCase )
            .ToArray();
    }

    private string BuildReviewChecklistText() {
        var builder = new StringBuilder();
        builder.AppendLine( $"{Texts["SummaryMode"]}: {SummaryModeValue}" );
        builder.AppendLine( $"{Texts["SummaryInput"]}: {SummaryInputValue}" );
        builder.AppendLine( $"{Texts["SummaryOutput"]}: {SummaryOutputValue}" );
        builder.AppendLine( $"{Texts["SummaryTemplate"]}: {SummaryTemplateValue}" );
        builder.AppendLine( $"{Texts["SummaryAudio"]}: {SummaryAudioValue}" );
        builder.Append( $"{Texts["SummaryExport"]}: {SummaryPenumbraValue}" );
        return builder.ToString();
    }

    private string BuildResultSummary( BatchProcessResult result ) {
        var builder = new StringBuilder();
        builder.Append( Texts.Format( "ResultSummaryCounts", result.SuccessCount, result.ErrorCount, result.TotalCount ) );
        if( !string.IsNullOrWhiteSpace( result.ExportedPlaylistPath ) ) {
            builder.Append( "; " );
            builder.Append( Texts.Format( "ResultSummaryPlaylist", result.ExportedPlaylistPath ) );
        }

        return builder.ToString();
    }

    private string ResolveOutputFolder( ProcessRequest request ) {
        if( request.Mode == ProcessingMode.SingleFile ) {
            return Path.GetDirectoryName( request.OutputPath ) ?? string.Empty;
        }

        return request.Mode == ProcessingMode.RepairScdFolder ? request.InputPath : request.OutputPath;
    }

    private static void ReplaceIssues( ObservableCollection<string> target, IEnumerable<string>? source = null ) {
        target.Clear();
        if( source is null ) {
            return;
        }

        foreach( var issue in source.Distinct() ) {
            target.Add( issue );
        }
    }

    private string TryDescribePenumbraPlaylist( string path ) {
        if( string.IsNullOrWhiteSpace( path ) ) {
            return Texts["ExistingPlaylistSummaryEmpty"];
        }

        if( !File.Exists( path ) ) {
            return Texts["ExistingPlaylistSummaryMissing"];
        }

        try {
            using var stream = File.OpenRead( path );
            using var document = JsonDocument.Parse( stream );
            var root = document.RootElement;

            var name = root.TryGetProperty( "Name", out var nameElement ) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;
            var type = root.TryGetProperty( "Type", out var typeElement ) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;
            var optionsCount = root.TryGetProperty( "Options", out var optionsElement ) && optionsElement.ValueKind == JsonValueKind.Array
                ? optionsElement.GetArrayLength()
                : 0;

            var displayName = string.IsNullOrWhiteSpace( name ) ? Path.GetFileNameWithoutExtension( path ) : name;
            var displayType = string.IsNullOrWhiteSpace( type ) ? "Unknown" : type;
            return Texts.Format( "ExistingPlaylistSummaryDetails", displayName, displayType, optionsCount );
        }
        catch( Exception ex ) {
            return Texts.Format( "ExistingPlaylistSummaryError", ex.Message );
        }
    }

    private void ApplyPenumbraPlaylistDefaults( string path ) {
        if( string.IsNullOrWhiteSpace( path ) || !File.Exists( path ) ) {
            InferredRelativeFolderWarning = string.Empty;
            return;
        }

        var modRoot = Path.GetDirectoryName( path );
        if( !string.IsNullOrWhiteSpace( modRoot ) ) {
            PenumbraModRootPath = modRoot;
        }

        try {
            using var stream = File.OpenRead( path );
            using var document = JsonDocument.Parse( stream );
            var root = document.RootElement;
            if( !root.TryGetProperty( "Options", out var optionsElement ) || optionsElement.ValueKind != JsonValueKind.Array ) {
                return;
            }

            var relativeFolderAnalysis = AnalyzeRelativeFoldersFromOptions( optionsElement );
            if( !string.IsNullOrWhiteSpace( relativeFolderAnalysis.SelectedFolder ) ) {
                PenumbraRelativeScdFolder = relativeFolderAnalysis.SelectedFolder;
            }

            InferredRelativeFolderWarning = relativeFolderAnalysis.HasMultipleFolders
                ? Texts.Format( "ExistingPlaylistRelativeFolderWarning", relativeFolderAnalysis.SelectedFolder )
                : string.Empty;

            var gamePaths = InferGamePathsFromOptions( optionsElement );
            if( gamePaths.Count > 0 ) {
                PenumbraGamePathsText = string.Join( Environment.NewLine, gamePaths );
            }
        }
        catch {
            InferredRelativeFolderWarning = string.Empty;
        }
    }

    private static RelativeFolderAnalysis AnalyzeRelativeFoldersFromOptions( JsonElement optionsElement ) {
        var candidateFolders = new List<string>();

        foreach( var option in optionsElement.EnumerateArray() ) {
            if( !option.TryGetProperty( "Files", out var filesElement ) || filesElement.ValueKind != JsonValueKind.Object ) {
                continue;
            }

            foreach( var fileProperty in filesElement.EnumerateObject() ) {
                var filePath = fileProperty.Value.GetString();
                if( string.IsNullOrWhiteSpace( filePath ) || !IsScdFilePath( filePath ) ) {
                    continue;
                }

                var normalized = filePath.Replace( '/', '\\' ).Trim();
                var folder = Path.GetDirectoryName( normalized )?.Replace( '/', '\\' )?.Trim( '\\' );
                if( !string.IsNullOrWhiteSpace( folder ) ) {
                    candidateFolders.Add( folder );
                }
            }
        }

        if( candidateFolders.Count == 0 ) {
            return new RelativeFolderAnalysis( string.Empty, false );
        }

        // This is one of those "be boring on purpose" heuristics. Playlist JSON in the wild is messy, and the most common folder is usually the least surprising default.
        var groupedFolders = candidateFolders
            .GroupBy( folder => folder, StringComparer.OrdinalIgnoreCase )
            .ToList();

        var selectedGroup = groupedFolders
            .OrderByDescending( group => group.Count() )
            .ThenBy( group => group.Key, StringComparer.OrdinalIgnoreCase )
            .First();

        return new RelativeFolderAnalysis( selectedGroup.Key, groupedFolders.Count > 1 );
    }

    private static IReadOnlyList<string> InferGamePathsFromOptions( JsonElement optionsElement ) {
        var gamePaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

        foreach( var option in optionsElement.EnumerateArray() ) {
            if( !option.TryGetProperty( "Files", out var filesElement ) || filesElement.ValueKind != JsonValueKind.Object ) {
                continue;
            }

            foreach( var fileProperty in filesElement.EnumerateObject() ) {
                var filePath = fileProperty.Value.GetString();
                if( string.IsNullOrWhiteSpace( filePath ) || !IsScdFilePath( filePath ) ) {
                    continue;
                }

                var gamePath = fileProperty.Name?.Trim();
                if( !string.IsNullOrWhiteSpace( gamePath ) ) {
                    gamePaths.Add( gamePath.Replace( '\\', '/' ) );
                }
            }
        }

        return gamePaths
            .OrderBy( path => path, StringComparer.OrdinalIgnoreCase )
            .ToArray();
    }

    private static bool IsScdFilePath( string path ) =>
        path.EndsWith( ".scd", StringComparison.OrdinalIgnoreCase );

    private sealed record RelativeFolderAnalysis( string SelectedFolder, bool HasMultipleFolders );

    private void LoadSettings() {
        if( _settingsService is null ) {
            return;
        }

        var settings = _settingsService.Load();
        RunSilently( () => {
            SelectedMode = settings.SelectedMode;
            SelectedThemeMode = settings.SelectedThemeMode;
            InputPath = settings.InputPath;
            OutputPath = settings.OutputPath;
            TemplateScdPath = settings.TemplateScdPath;
            FfmpegPath = settings.FfmpegPath;
            SelectedTemplateSourceMode = settings.SelectedTemplateSourceMode;
            RecursiveSearchEnabled = settings.SelectedMode == ProcessingMode.RepairScdFolder
                ? settings.RefreshRecursiveSearchEnabled
                : settings.BatchRecursiveSearchEnabled;
            SaveIntermediateOggFiles = settings.SaveIntermediateOggFiles;
            EnableLoop = settings.EnableLoop;
            SelectedAudioProfileMode = ResolveAudioProfileModeFromSettings( settings );
            SelectedAdvancedMode = settings.SelectedAdvancedMode;
            AdvancedValue = settings.AdvancedValue;
            SelectedExistingScdRefreshAction = settings.SelectedExistingScdRefreshAction;
            PenumbraExportEnabled = settings.PenumbraExportEnabled;
            PenumbraModRootPath = settings.PenumbraModRootPath;
            SelectedPenumbraExportMode = settings.SelectedPenumbraExportMode;
            PenumbraPlaylistName = settings.PenumbraPlaylistName;
            ExistingPenumbraPlaylistPath = settings.ExistingPenumbraPlaylistPath;
            PenumbraRelativeScdFolder = settings.SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting
                ? settings.PenumbraAppendRelativeScdFolder
                : settings.PenumbraCreateRelativeScdFolder;
            PenumbraGamePathsText = settings.SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting
                ? settings.PenumbraAppendGamePathsText
                : settings.PenumbraCreateGamePathsText;
            _windowLeft = settings.WindowLeft;
            _windowTop = settings.WindowTop;
            _windowWidth = settings.WindowWidth;
            _windowHeight = settings.WindowHeight;
            _isWindowMaximized = settings.IsWindowMaximized;
        } );

        ExistingPenumbraPlaylistSummary = TryDescribePenumbraPlaylist( ExistingPenumbraPlaylistPath );
        CoerceAdvancedValue();
        _outputPathManagedByWizard = string.IsNullOrWhiteSpace( OutputPath );
    }

    private void SaveSettings() {
        if( _settingsService is null ) {
            return;
        }

        var settings = _settingsService.Load();
        settings.SelectedMode = SelectedMode;
        settings.SelectedThemeMode = SelectedThemeMode;
        settings.InputPath = InputPath;
        settings.OutputPath = OutputPath;
        settings.TemplateScdPath = TemplateScdPath;
        settings.FfmpegPath = FfmpegPath;
        settings.SelectedTemplateSourceMode = SelectedTemplateSourceMode;
        if( IsRefreshMode ) {
            settings.RefreshRecursiveSearchEnabled = RecursiveSearchEnabled;
        }
        else {
            settings.BatchRecursiveSearchEnabled = RecursiveSearchEnabled;
        }

        settings.SaveIntermediateOggFiles = SaveIntermediateOggFiles;
        settings.EnableLoop = EnableLoop;
        settings.SelectedAudioProfileMode = SelectedAudioProfileMode;
        settings.UsePresetMode = SelectedAudioProfileMode != AudioProfileMode.Custom;
        settings.SelectedPresetMode = OggPresetMode.HighQualityCompatible;
        settings.SelectedAdvancedMode = SelectedAdvancedMode;
        settings.AdvancedValue = AdvancedValue;
        settings.SelectedExistingScdRefreshAction = SelectedExistingScdRefreshAction;
        settings.PenumbraExportEnabled = PenumbraExportEnabled;
        settings.PenumbraModRootPath = PenumbraModRootPath;
        settings.SelectedPenumbraExportMode = SelectedPenumbraExportMode;
        settings.PenumbraPlaylistName = PenumbraPlaylistName;
        settings.ExistingPenumbraPlaylistPath = ExistingPenumbraPlaylistPath;
        if( SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting ) {
            settings.PenumbraAppendRelativeScdFolder = PenumbraRelativeScdFolder;
            settings.PenumbraAppendGamePathsText = PenumbraGamePathsText;
        }
        else {
            settings.PenumbraCreateRelativeScdFolder = PenumbraRelativeScdFolder;
            settings.PenumbraCreateGamePathsText = PenumbraGamePathsText;
        }

        settings.WindowLeft = _windowLeft;
        settings.WindowTop = _windowTop;
        settings.WindowWidth = _windowWidth;
        settings.WindowHeight = _windowHeight;
        settings.IsWindowMaximized = _isWindowMaximized;
        _settingsService.Save( settings );
    }

    private void OnEntryLogged( LogEntry entry ) {
        var prefix = entry.Level switch {
            LogLevel.Success => "OK ",
            LogLevel.Error => "ERR",
            _ => "INF"
        };
        var line = $"[{entry.Timestamp:HH:mm:ss}] {prefix} {entry.Message}";

        lock( _pendingLogSync ) {
            _pendingLogLines.Enqueue( line );
        }

        var dispatcher = Application.Current.Dispatcher;
        if( dispatcher.CheckAccess() ) {
            if( !_logFlushTimer.IsEnabled ) {
                _logFlushTimer.Start();
            }
            return;
        }

        _ = dispatcher.InvokeAsync( () => {
            if( !_logFlushTimer.IsEnabled ) {
                _logFlushTimer.Start();
            }
        } );
    }

    private void FlushPendingLogEntries() {
        List<string> lines;
        lock( _pendingLogSync ) {
            if( _pendingLogLines.Count == 0 ) {
                _logFlushTimer.Stop();
                return;
            }

            lines = new List<string>( _pendingLogLines.Count );
            while( _pendingLogLines.Count > 0 ) {
                lines.Add( _pendingLogLines.Dequeue() );
            }
        }

        foreach( var line in lines ) {
            _logBuilder.AppendLine( line );
        }

        LogText = _logBuilder.ToString();
    }

    private void ClearLog() {
        lock( _pendingLogSync ) {
            _pendingLogLines.Clear();
        }
        _logFlushTimer.Stop();
        _logBuilder.Clear();
        LogText = string.Empty;
    }
}
