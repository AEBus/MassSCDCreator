using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MassSCDCreator.Models;

namespace MassSCDCreator.ViewModels;

public partial class MainWindowViewModel {
    [RelayCommand( CanExecute = nameof( CanGoBack ) )]
    private void PreviousStep() {
        var visibleSteps = GetVisibleSteps();
        var currentIndex = visibleSteps.IndexOf( CurrentStep );
        if( currentIndex > 0 ) {
            CurrentStep = visibleSteps[currentIndex - 1];
        }
    }

    private bool CanGoBack() => !IsProcessing && GetVisibleSteps().IndexOf( CurrentStep ) > 0;

    [RelayCommand( CanExecute = nameof( CanGoNext ) )]
    private void NextStep() {
        var visibleSteps = GetVisibleSteps();
        var currentIndex = visibleSteps.IndexOf( CurrentStep );
        if( currentIndex >= 0 && currentIndex < visibleSteps.Count - 1 ) {
            CurrentStep = visibleSteps[currentIndex + 1];
        }
    }

    private bool CanGoNext() =>
        !IsProcessing &&
        CurrentStep is not WizardStep.Review and not WizardStep.Result &&
        !CurrentStepHasBlockingIssues();

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
    private void SelectCompatiblePreset() => SelectedPresetMode = OggPresetMode.HighQualityCompatible;

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
            selected = _dialogService.PickInputFile( Texts["DialogSelectInputFile"], "Audio files (*.mp3;*.flac;*.ogg;*.m4a;*.wav;*.aac;*.wma;*.opus;*.aiff;*.aif;*.mp4;*.m4b)|*.mp3;*.flac;*.ogg;*.m4a;*.wav;*.aac;*.wma;*.opus;*.aiff;*.aif;*.mp4;*.m4b|All files (*.*)|*.*" );
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

    [RelayCommand]
    private void BrowseExistingPenumbraPlaylist() {
        var selected = _dialogService.PickExistingPlaylistFile( Texts["DialogSelectPlaylistFile"] );
        if( !string.IsNullOrWhiteSpace( selected ) ) {
            ExistingPenumbraPlaylistPath = selected;
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

    [RelayCommand]
    private void ToggleLog() => IsLogExpanded = !IsLogExpanded;

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
            case nameof( OutputPath ):
                _outputPathManagedByWizard = string.IsNullOrWhiteSpace( OutputPath ) ||
                    string.Equals( OutputPath, _lastSuggestedOutputPath, StringComparison.OrdinalIgnoreCase );
                break;
            case nameof( SelectedTemplateSourceMode ):
                if( !IsRefreshMode && SelectedTemplateSourceMode == TemplateSourceMode.CurrentFile ) {
                    RunSilently( () => SelectedTemplateSourceMode = TemplateSourceMode.BuiltInRecommended );
                }
                break;
            case nameof( SelectedAdvancedMode ):
                CoerceAdvancedValue();
                break;
            case nameof( ExistingPenumbraPlaylistPath ):
                ExistingPenumbraPlaylistSummary = TryDescribePenumbraPlaylist( ExistingPenumbraPlaylistPath );
                ApplyPenumbraPlaylistDefaults( ExistingPenumbraPlaylistPath );
                break;
        }

        RefreshState();
        SaveSettings();
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
        EnsureCurrentStepIsVisible();
    }

    private void CoerceAdvancedValue() {
        RunSilently( () => {
            if( SelectedAdvancedMode == OggAdvancedMode.QualityVbr && !double.TryParse( AdvancedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _ ) ) {
                AdvancedValue = "9";
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

        // This little guardrail exists because users absolutely should win once they type a custom path. The wizard is here to help, not to become their overbearing roommate.
        if( _outputPathManagedByWizard || string.IsNullOrWhiteSpace( OutputPath ) || string.Equals( OutputPath, _lastSuggestedOutputPath, StringComparison.OrdinalIgnoreCase ) ) {
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

    private void EnsureCurrentStepIsVisible() {
        var visibleSteps = GetVisibleSteps();
        if( !visibleSteps.Contains( CurrentStep ) ) {
            CurrentStep = visibleSteps.Last();
        }
    }

    private void RefreshState() {
        RefreshStepItems();
        RefreshValidationIssues();
        UpdateCommandState();
        OnPropertyChanged( string.Empty );
    }

    private void RefreshStepItems() {
        var visibleSteps = GetVisibleSteps();
        var currentIndex = visibleSteps.IndexOf( CurrentStep );

        foreach( var step in Steps ) {
            var stepIndex = visibleSteps.IndexOf( step.Step );
            step.DisplayTitle = Texts[step.TitleKey];
            step.DisplayDescription = Texts[step.DescriptionKey];
            step.IsVisible = stepIndex >= 0;
            step.IsCurrent = step.Step == CurrentStep;
            step.IsCompleted = stepIndex >= 0 && currentIndex > stepIndex;
        }
    }

    private List<WizardStep> GetVisibleSteps() {
        var steps = new List<WizardStep> {
            WizardStep.Workflow,
            WizardStep.Paths,
            WizardStep.TemplateAudio
        };

        if( ShowPenumbraStep ) {
            steps.Add( WizardStep.Penumbra );
        }

        steps.Add( WizardStep.Review );
        steps.Add( WizardStep.Result );
        return steps;
    }

    private bool CurrentStepHasBlockingIssues() => CurrentStep switch {
        WizardStep.Paths => PathsIssues.Count > 0,
        WizardStep.TemplateAudio => TemplateIssues.Count > 0,
        WizardStep.Penumbra => ShowPenumbraStep && PenumbraIssues.Count > 0,
        WizardStep.Review => ReviewIssues.Count > 0,
        _ => false
    };

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
        PreviousStepCommand.NotifyCanExecuteChanged();
        NextStepCommand.NotifyCanExecuteChanged();
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
