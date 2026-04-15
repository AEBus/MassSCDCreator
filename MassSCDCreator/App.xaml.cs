using System.Windows;
using System.Windows.Media;
using MassSCDCreator.Services.Audio;
using MassSCDCreator.Services.Dialogs;
using MassSCDCreator.Services.Logging;
using MassSCDCreator.Services.Penumbra;
using MassSCDCreator.Services.Processing;
using MassSCDCreator.Services.Scd;
using MassSCDCreator.Services.Settings;
using MassSCDCreator.ViewModels;
using Microsoft.Win32;
using MassSCDCreator.Models;
using System.IO;
using System.Diagnostics;

namespace MassSCDCreator;

public partial class DesktopBootstrap : Application {
    public static event Action<ThemeMode>? ThemeChanged;

    public static ThemeMode ResolveEffectiveTheme( ThemeMode mode ) =>
        mode == ThemeMode.System ? GetSystemThemeMode() : mode;

    public static void ApplyTheme( ThemeMode mode ) {
        var effectiveMode = ResolveEffectiveTheme( mode );
        var dark = effectiveMode == ThemeMode.Dark;

        SetBrush( "WindowBackgroundBrush", dark ? "#0E141B" : "#F3F5F7" );
        SetBrush( "SurfaceBrush", dark ? "#15202B" : "#FFFFFF" );
        SetBrush( "PanelSurfaceBrush", dark ? "#1B2835" : "#F7FAFD" );
        SetBrush( "SubtleSurfaceBrush", dark ? "#223243" : "#EEF3F8" );
        SetBrush( "PrimaryTextBrush", dark ? "#E9F0F6" : "#0F2135" );
        SetBrush( "SecondaryTextBrush", dark ? "#B7C5D3" : "#5B6C7E" );
        SetBrush( "MutedTextBrush", dark ? "#91A5B8" : "#7A8898" );
        SetBrush( "CardBorderBrush", dark ? "#314457" : "#D7E1EC" );
        SetBrush( "ControlBackgroundBrush", dark ? "#1C2A38" : "#FFFFFF" );
        SetBrush( "ControlForegroundBrush", dark ? "#E9F0F6" : "#102338" );
        SetBrush( "ControlBorderBrush", dark ? "#466078" : "#BCC8D5" );
        SetBrush( "ControlHoverBrush", dark ? "#284055" : "#EAF1F8" );
        SetBrush( "ControlPressedBrush", dark ? "#35516B" : "#DCE7F2" );
        SetBrush( "AccentBrush", dark ? "#6EAEEB" : "#2B6CB0" );
        SetBrush( "AccentSoftBrush", dark ? "#20364A" : "#DCEBFB" );
        SetBrush( "AccentSelectionBrush", dark ? "#25445E" : "#C9E0FA" );
        SetBrush( "AccentStrongTextBrush", "#F8FBFF" );
        SetBrush( "SelectionIndicatorBrush", dark ? "#F8FBFF" : "#FFFFFF" );
        SetBrush( "SuccessBrush", dark ? "#5FCB8D" : "#1F8F55" );
        SetBrush( "SuccessBackgroundBrush", dark ? "#163428" : "#E7F6EE" );
        SetBrush( "WarningBackgroundBrush", dark ? "#47351B" : "#FFF4E5" );
        SetBrush( "WarningTextBrush", dark ? "#FFD089" : "#8A5A00" );
        SetBrush( "DangerBrush", dark ? "#F07A7A" : "#C23B3B" );
        SetBrush( "DangerBackgroundBrush", dark ? "#3E1E24" : "#FDECEC" );
        SetBrush( "ScrollThumbBrush", dark ? "#546B82" : "#91A5B9" );
        SetBrush( "ScrollTrackBrush", dark ? "#1E2C39" : "#E5ECF3" );
        SetSystemBrush( SystemColors.WindowBrushKey, dark ? "#24303B" : "#FFFFFF" );
        SetSystemBrush( SystemColors.WindowTextBrushKey, dark ? "#E7EEF5" : "#11253A" );
        SetSystemBrush( SystemColors.ControlBrushKey, dark ? "#24303B" : "#FFFFFF" );
        SetSystemBrush( SystemColors.ControlTextBrushKey, dark ? "#E7EEF5" : "#11253A" );
        SetSystemBrush( SystemColors.GrayTextBrushKey, dark ? "#DCE8F4" : "#11253A" );
        SetSystemBrush( SystemColors.HighlightBrushKey, dark ? "#7A94B1" : "#4C87C8" );
        SetSystemBrush( SystemColors.HighlightTextBrushKey, dark ? "#0C1722" : "#FFFFFF" );
        SetSystemBrush( SystemColors.InactiveSelectionHighlightBrushKey, dark ? "#6E88A4" : "#DCE7F2" );
        SetSystemBrush( SystemColors.InactiveSelectionHighlightTextBrushKey, dark ? "#0C1722" : "#11253A" );
        ThemeChanged?.Invoke( effectiveMode );
    }

    protected override void OnStartup( StartupEventArgs e ) {
        base.OnStartup( e );

        var logger = new UiLoggerService();
        var dialogService = new FileDialogService();
        var ffmpegInstaller = new FfmpegInstaller();
        var audioConverter = new FfmpegAudioConverter( ffmpegInstaller );
        var scdService = new ScdService();
        var penumbraExportService = new PenumbraExportService();
        var settingsService = new JsonSettingsService();
        var batchProcessor = new FileBatchProcessor( audioConverter, scdService, logger, penumbraExportService );
        var viewModel = new MainWindowViewModel( dialogService, batchProcessor, logger, settingsService, ApplyTheme, ffmpegInstaller );

        ApplyTheme( viewModel.SelectedThemeMode );

        var window = new MainWindow {
            DataContext = viewModel
        };
        window.Show();
        _ = EnsureFfmpegAvailabilityAsync( window, ffmpegInstaller, settingsService, viewModel );
    }

    private static ThemeMode GetSystemThemeMode() {
        try {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey( @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" );
            var value = personalizeKey?.GetValue( "AppsUseLightTheme" );
            return value is int intValue && intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch {
            return ThemeMode.Light;
        }
    }

    private static void SetBrush( string key, string colorHex ) {
        Current.Resources[key] = new SolidColorBrush( ( Color )ColorConverter.ConvertFromString( colorHex ) );
    }

    private static void SetSystemBrush( ResourceKey key, string colorHex ) {
        Current.Resources[key] = new SolidColorBrush( ( Color )ColorConverter.ConvertFromString( colorHex ) );
    }

    private static async Task EnsureFfmpegAvailabilityAsync( Window owner, IFfmpegInstaller installer, ISettingsService settingsService, MainWindowViewModel viewModel ) {
        try {
            var settings = settingsService.Load();
            if( settings.SkipFfmpegStartupCheck || !ShouldRunFfmpegStartupCheck( settings ) ) {
                return;
            }

            if( File.Exists( installer.ManagedFfmpegPath ) || await IsFfmpegInPathAsync() ) {
                return;
            }

            // Startup prompts are annoying, but shipping a desktop tool that immediately faceplants on first run is more annoying.
            var prompt = "FFmpeg was not found in PATH or in the Tools folder.\n\nYes: download now to Tools/ffmpeg\nNo: skip for now\nCancel: skip this check next time";
            var result = MessageBox.Show( owner, prompt, "Mass SCD Creator", MessageBoxButton.YesNoCancel, MessageBoxImage.Question );
            if( result == MessageBoxResult.Cancel ) {
                settings.SkipFfmpegStartupCheck = true;
                settingsService.Save( settings );
                return;
            }

            if( result != MessageBoxResult.Yes ) {
                return;
            }

            await installer.EnsureInstalledAsync( forceUpdate: false, CancellationToken.None );
            viewModel.FfmpegInstallStatus = "FFmpeg is installed and ready.";
        }
        catch( Exception ex ) {
            MessageBox.Show( owner, $"FFmpeg auto-install failed: {ex.Message}", "Mass SCD Creator", MessageBoxButton.OK, MessageBoxImage.Warning );
        }
    }

    private static bool ShouldRunFfmpegStartupCheck( AppSettings settings ) {
        if( settings.SelectedMode == ProcessingMode.RepairScdFolder && settings.SelectedExistingScdRefreshAction == ExistingScdRefreshAction.MatchTemplateOnly ) {
            return false;
        }

        return ResolveAudioProfileMode( settings ) != AudioProfileMode.OriginalOgg;
    }

    private static AudioProfileMode ResolveAudioProfileMode( AppSettings settings ) {
        if( settings.SelectedAudioProfileMode.HasValue ) {
            return settings.SelectedAudioProfileMode.Value;
        }

        return settings.UsePresetMode ? AudioProfileMode.Recommended : AudioProfileMode.Custom;
    }

    private static async Task<bool> IsFfmpegInPathAsync() {
        var psi = new ProcessStartInfo {
            FileName = "where.exe",
            Arguments = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start( psi );
        if( process is null ) {
            return false;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if( process.ExitCode != 0 ) {
            return false;
        }

        var firstMatch = output
            .Split( [Environment.NewLine], StringSplitOptions.RemoveEmptyEntries )
            .Select( line => line.Trim() )
            .FirstOrDefault( File.Exists );

        return !string.IsNullOrWhiteSpace( firstMatch );
    }
}
