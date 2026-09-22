using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MassSCDCreator.Models;
using MassSCDCreator.Services.Penumbra;

namespace MassSCDCreator.ViewModels;

public partial class MainWindowViewModel {
    private static readonly HashSet<string> PresentationOnlyProperties = [
        nameof( ProgressPercent ),
        nameof( ProgressText ),
        nameof( StatusText ),
        nameof( SummaryText ),
        nameof( LogText ),
        nameof( HasRun ),
        nameof( IsAudioExpanded ),
        nameof( IsPenumbraExpanded ),
        nameof( IsLogExpanded ),
        nameof( FfmpegInstallStatus ),
        nameof( IsInstallingFfmpeg ),
        nameof( ResultOutputFolderPath ),
        nameof( ResultPlaylistPath )
    ];

    [RelayCommand]
    private void SelectSingleMode() => SelectedMode = ProcessingMode.SingleFile;

    [RelayCommand]
    private void SelectBatchMode() => SelectedMode = ProcessingMode.BatchFolder;

    [RelayCommand]
    private void SelectRefreshMode() => SelectedMode = ProcessingMode.RepairScdFolder;

    [RelayCommand]
    private void SelectSystemTheme() => SelectedThemeMode = ThemeMode.System;

    [RelayCommand]
    private void SelectLightTheme() => SelectedThemeMode = ThemeMode.Light;

    [RelayCommand]
    private void SelectDarkTheme() => SelectedThemeMode = ThemeMode.Dark;

    [RelayCommand]
    private void SelectBuiltInTemplate() => SelectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended;

    [RelayCommand]
    private void SelectCustomTemplate() => SelectedTemplateSourceMode = TemplateSourceMode.CustomFile;

    [RelayCommand]
    private void SelectCurrentTemplate() => SelectedTemplateSourceMode = TemplateSourceMode.CurrentFile;

    [RelayCommand]
    private void SelectRecommendedAudioProfile() => SelectedAudioProfileMode = AudioProfileMode.Recommended;

    [RelayCommand]
    private void SelectCustomAudioProfile() => SelectedAudioProfileMode = AudioProfileMode.Custom;

    [RelayCommand]
    private void SelectOriginalOggAudioProfile() => SelectedAudioProfileMode = AudioProfileMode.OriginalOgg;

    [RelayCommand]
    private void SelectAdvancedQualityMode() => SelectedAdvancedMode = OggAdvancedMode.QualityVbr;

    [RelayCommand]
    private void SelectAdvancedBitrateMode() => SelectedAdvancedMode = OggAdvancedMode.NominalBitrate;

    [RelayCommand]
    private void SelectRefreshTemplateOnly() => SelectedExistingScdRefreshAction = ExistingScdRefreshAction.MatchTemplateOnly;

    [RelayCommand]
    private void SelectRefreshAudioOnly() => SelectedExistingScdRefreshAction = ExistingScdRefreshAction.ReencodeAudioOnly;

    [RelayCommand]
    private void SelectRefreshTemplateAndAudio() => SelectedExistingScdRefreshAction = ExistingScdRefreshAction.MatchTemplateAndReencodeAudio;

    [RelayCommand]
    private void SelectCreatePlaylistMode() => SelectedPenumbraExportMode = PenumbraPlaylistExportMode.CreateNew;

    [RelayCommand]
    private void SelectAppendPlaylistMode() => SelectedPenumbraExportMode = PenumbraPlaylistExportMode.AppendExisting;

    [RelayCommand]
    private void BrowseInput() {
        string? selected;
        if( IsRefreshMode ) {
            selected = _dialogService.PickFolder( Texts["DialogSelectRefreshFolder"] );
        }
        else if( IsBatchMode ) {
            selected = _dialogService.PickFolder( Texts["DialogSelectInputFolder"] );
        }
        else {
            var inputFilter = SelectedAudioProfileMode == AudioProfileMode.OriginalOgg
                ? "OGG files (*.ogg)|*.ogg|All files (*.*)|*.*"
                : "Audio files (*.mp3;*.flac;*.ogg;*.m4a;*.wav;*.aac;*.wma;*.opus;*.aiff;*.aif;*.mp4;*.m4b)|*.mp3;*.flac;*.ogg;*.m4a;*.wav;*.aac;*.wma;*.opus;*.aiff;*.aif;*.mp4;*.m4b|All files (*.*)|*.*";
            selected = _dialogService.PickInputFile( Texts["DialogSelectInputFile"], inputFilter );
        }

        if( !string.IsNullOrWhiteSpace( selected ) ) {
            InputPath = selected;
        }
    }

    [RelayCommand]
    private void BrowseOutput() {
        string? selected;
        if( IsBatchMode ) {
            selected = _dialogService.PickFolder( Texts["DialogSelectOutputFolder"] );
        }
        else {
            var defaultName = string.IsNullOrWhiteSpace( InputPath ) ? "output.scd" : Path.GetFileNameWithoutExtension( InputPath ) + ".scd";
            selected = _dialogService.PickOutputFile( Texts["DialogSelectOutputFile"], "SCD files (*.scd)|*.scd", ".scd", defaultName );
        }

        if( !string.IsNullOrWhiteSpace( selected ) ) {
            OutputPath = selected;
            _outputPathManagedByWizard = false;
        }
    }

    [RelayCommand]
    private void BrowseTemplate() {
        var selected = _dialogService.PickInputFile( Texts["DialogSelectTemplateFile"], "SCD files (*.scd)|*.scd|All files (*.*)|*.*" );
        if( !string.IsNullOrWhiteSpace( selected ) ) {
            TemplateScdPath = selected;
        }
    }

    [RelayCommand]
    private void BrowseFfmpeg() {
        var selected = _dialogService.PickInputFile( Texts["DialogSelectFfmpegFile"], "Executable (*.exe)|*.exe|All files (*.*)|*.*" );
        if( !string.IsNullOrWhiteSpace( selected ) ) {
            FfmpegPath = selected;
            FfmpegInstallStatus = string.Empty;
        }
    }

    [RelayCommand]
    private async Task DownloadOrUpdateFfmpegAsync() {
        if( _ffmpegInstaller is null || IsInstallingFfmpeg ) {
            return;
        }

        try {
            IsInstallingFfmpeg = true;
            FfmpegInstallStatus = Texts["FfmpegInstallStatusDownloading"];
            await _ffmpegInstaller.EnsureInstalledAsync( true, CancellationToken.None );
            FfmpegInstallStatus = Texts["FfmpegInstallStatusReady"];
        }
        catch( Exception ex ) {
            FfmpegInstallStatus = $"{Texts["FfmpegInstallStatusFailed"]} {ex.Message}";
            MessageBox.Show( ex.Message, Texts["WindowTitle"], MessageBoxButton.OK, MessageBoxImage.Error );
        }
        finally {
            IsInstallingFfmpeg = false;
        }
    }

    [RelayCommand]
    private void ResetFfmpegStartupCheck() {
        if( _settingsService is null ) {
            return;
        }

        var settings = _settingsService.Load();
        settings.SkipFfmpegStartupCheck = false;
        _settingsService.Save( settings );
        FfmpegInstallStatus = Texts["FfmpegStartupCheckResetDone"];
    }

    [RelayCommand]
    private void BrowsePenumbraModRoot() {
        var selected = _dialogService.PickFolder( Texts["DialogSelectPenumbraFolder"] );
        if( !string.IsNullOrWhiteSpace( selected ) ) {
            PenumbraModRootPath = selected;
        }
    }

    [RelayCommand( CanExecute = nameof( CanOpenOutputFolder ) )]
    private void OpenOutputFolder() => OpenPath( ResultOutputFolderPath );

    private bool CanOpenOutputFolder() =>
        !string.IsNullOrWhiteSpace( ResultOutputFolderPath ) &&
        Directory.Exists( ResultOutputFolderPath );

    [RelayCommand( CanExecute = nameof( CanOpenPlaylist ) )]
    private void OpenPlaylist() => OpenPath( ResultPlaylistPath );

    private bool CanOpenPlaylist() =>
        !string.IsNullOrWhiteSpace( ResultPlaylistPath ) &&
        File.Exists( ResultPlaylistPath );

    private void HandlePropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if( _suspendReactions || string.IsNullOrWhiteSpace( e.PropertyName ) ) {
            return;
        }

        switch( e.PropertyName ) {
            case nameof( SelectedThemeMode ):
                _applyTheme?.Invoke( SelectedThemeMode );
                break;
            case nameof( SelectedMode ):
                ApplyModeDefaults();
                break;
            case nameof( InputPath ):
                UpdateSuggestedOutputPath();
                break;
            case nameof( UseCustomOutputPath ):
                if( !UseCustomOutputPath ) {
                    _outputPathManagedByWizard = true;
                    UpdateSuggestedOutputPath();
                }
                break;
            case nameof( OutputPath ):
                _outputPathManagedByWizard = string.IsNullOrWhiteSpace( OutputPath ) ||
                    string.Equals( OutputPath, _lastSuggestedOutputPath, StringComparison.OrdinalIgnoreCase );
                break;
            case nameof( SelectedTemplateSourceMode ):
                if( !IsRefreshMode && SelectedTemplateSourceMode == TemplateSourceMode.CurrentFile ) {
                    RunSilently( () => SelectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended );
                }
                break;
            case nameof( SelectedAudioProfileMode ):
                if( SelectedAudioProfileMode == AudioProfileMode.Custom ) {
                    CoerceAdvancedValue();
                }
                break;
            case nameof( SelectedAdvancedMode ):
                CoerceAdvancedValue();
                break;
            case nameof( AdvancedValue ):
                OnPropertyChanged( nameof( AdvancedQualitySliderValue ) );
                break;
            case nameof( PenumbraModRootPath ):
                RefreshPenumbraGamePathCandidates( PenumbraModRootPath, true );
                RefreshPenumbraPlaylistCandidates( PenumbraModRootPath );
                break;
            case nameof( ExistingPenumbraPlaylistPath ):
                ApplyPenumbraPlaylistDefaults( ExistingPenumbraPlaylistPath );
                break;
            case nameof( PenumbraPlaylistName ):
                if( SelectedPenumbraExportMode == PenumbraPlaylistExportMode.AppendExisting && IsV4MetadataFile( ExistingPenumbraPlaylistPath ) ) {
                    ApplyPenumbraPlaylistDefaults( ExistingPenumbraPlaylistPath );
                }
                break;
            case nameof( SelectedPenumbraPlaylistCandidate ):
                ApplySelectedPlaylistCandidate();
                break;
            case nameof( SelectedPenumbraGamePathCandidate ):
                if( SelectedPenumbraGamePathCandidate is not null ) {
                    RunSilently( () => PenumbraGamePathsText = SelectedPenumbraGamePathCandidate.Path );
                }
                break;
            case nameof( PenumbraGamePathsText ):
                SynchronizeSelectedGamePathCandidate();
                break;
        }

        // Presentation updates need no validation or persistence. Skip repeated folder scans
        // and settings writes while still refreshing bindings and commands.
        if( PresentationOnlyProperties.Contains( e.PropertyName ) ) {
            UpdateCommandState();
            OnPropertyChanged( string.Empty );
            return;
        }

        RefreshState();
        SaveSettings();
    }


    private void RefreshPenumbraPlaylistCandidates( string modRootPath ) {
        var previous = SelectedPenumbraPlaylistCandidate;
        var candidates = PenumbraPlaylistDiscovery.Discover( modRootPath );

        PenumbraPlaylistCandidates.Clear();
        foreach( var candidate in candidates ) {
            PenumbraPlaylistCandidates.Add( candidate );
        }

        var restored = previous is null
            ? null
            : PenumbraPlaylistCandidates.FirstOrDefault( candidate =>
                string.Equals( candidate.Path, previous.Path, StringComparison.OrdinalIgnoreCase ) &&
                string.Equals( candidate.GroupName, previous.GroupName, StringComparison.OrdinalIgnoreCase ) );

        RunSilently( () => SelectedPenumbraPlaylistCandidate = restored );
        if( restored is null && previous is not null ) {
            RunSilently( () => {
                ExistingPenumbraPlaylistPath = string.Empty;
                InferredRelativeFolderWarning = string.Empty;
            } );
        }

        OnPropertyChanged( nameof( HasPenumbraPlaylistCandidates ) );
        OnPropertyChanged( nameof( ShowNoPlaylistsFound ) );
    }

    private void ApplySelectedPlaylistCandidate() {
        if( SelectedPenumbraPlaylistCandidate is not { } candidate ) {
            return;
        }

        RunSilently( () => {
            ExistingPenumbraPlaylistPath = candidate.Path;
            PenumbraPlaylistName = candidate.GroupName;

            if( !string.IsNullOrWhiteSpace( candidate.RelativeFolder ) ) {
                PenumbraRelativeScdFolder = candidate.RelativeFolder;
            }

            if( candidate.GamePaths.Count > 0 ) {
                PenumbraGamePathsText = string.Join( Environment.NewLine, candidate.GamePaths );
            }

            InferredRelativeFolderWarning = candidate.HasMixedFolders
                ? Texts.Format( "ExistingPlaylistRelativeFolderWarning", candidate.RelativeFolder )
                : string.Empty;
        } );

    }
    private void RefreshPenumbraGamePathCandidates( string modRootPath, bool replaceCurrentPath ) {
        var candidates = PenumbraGamePathDiscovery.Discover( modRootPath );
        PenumbraGamePathCandidates.Clear();
        foreach( var candidate in candidates ) {
            PenumbraGamePathCandidates.Add( candidate );
        }

        var preferred = PenumbraGamePathCandidates.FirstOrDefault();
        RunSilently( () => {
            SelectedPenumbraGamePathCandidate = preferred;
            if( replaceCurrentPath && preferred is not null ) {
                PenumbraGamePathsText = preferred.Path;
            }
        } );
        OnPropertyChanged( nameof( ShowPenumbraGamePathCandidates ) );
    }

    private void SynchronizeSelectedGamePathCandidate() {
        var normalized = PenumbraGamePathsText.Trim();
        var candidate = normalized.Contains( '\n' ) || normalized.Contains( '\r' )
            ? null
            : PenumbraGamePathCandidates.FirstOrDefault( item =>
                string.Equals( item.Path, normalized, StringComparison.OrdinalIgnoreCase ) );
        if( !ReferenceEquals( SelectedPenumbraGamePathCandidate, candidate ) ) {
            RunSilently( () => SelectedPenumbraGamePathCandidate = candidate );
        }
    }

    private void ApplyModeDefaults() {
        RunSilently( () => {
            if( IsRefreshMode ) {
                if( SelectedTemplateSourceMode == TemplateSourceMode.BuiltInRecommended && string.IsNullOrWhiteSpace( TemplateScdPath ) ) {
                    SelectedTemplateSourceMode = TemplateSourceMode.CurrentFile;
                }

                PenumbraExportEnabled = false;
                RecursiveSearchEnabled = _settingsService?.Load().RefreshRecursiveSearchEnabled ?? RecursiveSearchEnabled;
            }
            else {
                if( SelectedTemplateSourceMode == TemplateSourceMode.CurrentFile ) {
                    SelectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended;
                }

                RecursiveSearchEnabled = _settingsService?.Load().BatchRecursiveSearchEnabled ?? RecursiveSearchEnabled;
            }
        } );

        UpdateSuggestedOutputPath();
    }

    private void CoerceAdvancedValue() {
        RunSilently( () => {
            if( SelectedAdvancedMode == OggAdvancedMode.QualityVbr ) {
                if( !double.TryParse( AdvancedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var quality ) ) {
                    AdvancedValue = "9";
                    return;
                }

                var clamped = Math.Clamp( quality, 1.0, 10.0 );
                AdvancedValue = clamped.ToString( "0.0", CultureInfo.InvariantCulture );
            }
            else if( SelectedAdvancedMode == OggAdvancedMode.NominalBitrate && !int.TryParse( AdvancedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _ ) ) {
                AdvancedValue = "320";
            }
        } );
    }

    private void UpdateSuggestedOutputPath() {
        if( IsRefreshMode ) {
            return;
        }

        var suggested = GetSuggestedOutputPath();
        if( string.IsNullOrWhiteSpace( suggested ) ) {
            return;
        }

        // Preserve a user-specified path until it is cleared or custom output is disabled.
        var derivedPathOwnsTheField =
            !UseCustomOutputPath ||
            _outputPathManagedByWizard ||
            string.IsNullOrWhiteSpace( OutputPath ) ||
            string.Equals( OutputPath, _lastSuggestedOutputPath, StringComparison.OrdinalIgnoreCase );

        if( derivedPathOwnsTheField ) {
            RunSilently( () => OutputPath = suggested );
            _outputPathManagedByWizard = true;
        }

        _lastSuggestedOutputPath = suggested;
    }

    private string GetSuggestedOutputPath() {
        if( string.IsNullOrWhiteSpace( InputPath ) ) {
            return string.Empty;
        }

        if( IsSingleMode ) {
            var fullInputPath = Path.GetFullPath( InputPath );
            var directory = Path.GetDirectoryName( fullInputPath );
            return string.IsNullOrWhiteSpace( directory )
                ? string.Empty
                : Path.Combine( directory, Path.GetFileNameWithoutExtension( fullInputPath ) + ".scd" );
        }

        if( IsBatchMode ) {
            var fullInputPath = Path.GetFullPath( InputPath );
            var parent = Path.GetDirectoryName( fullInputPath );
            return string.IsNullOrWhiteSpace( parent )
                ? Path.Combine( fullInputPath, "_scd_output" )
                : Path.Combine( parent, Path.GetFileName( fullInputPath ) + "_scd_output" );
        }

        return string.Empty;
    }

    private void RefreshState() {
        RefreshValidationIssues();
        UpdateCommandState();
        OnPropertyChanged( string.Empty );
    }

    private static void OpenPath( string path ) {
        if( string.IsNullOrWhiteSpace( path ) ) {
            return;
        }

        Process.Start( new ProcessStartInfo {
            FileName = path,
            UseShellExecute = true
        } );
    }

    private void UpdateCommandState() {
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OpenOutputFolderCommand.NotifyCanExecuteChanged();
        OpenPlaylistCommand.NotifyCanExecuteChanged();
    }

    private void RunSilently( Action action ) {
        _suspendReactions = true;
        try {
            action();
        }
        finally {
            _suspendReactions = false;
        }
    }
}
