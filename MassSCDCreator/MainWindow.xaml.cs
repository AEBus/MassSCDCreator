using System.Windows;
using System.Windows.Interop;
using MassSCDCreator.Models;
using MassSCDCreator.ViewModels;
using System.Runtime.InteropServices;

namespace MassSCDCreator;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        LocationChanged += OnWindowPlacementChanged;
        SizeChanged += OnWindowPlacementChanged;
        StateChanged += OnWindowPlacementChanged;
        Closed += OnClosed;
    }

    private void OnSourceInitialized( object? sender, EventArgs e ) {
        DesktopBootstrap.ThemeChanged += ApplyWindowTheme;
        ApplyWindowTheme( ThemeMode.System );
    }

    private void OnLoaded( object? sender, RoutedEventArgs e ) {
        if( DataContext is not MainWindowViewModel viewModel ) {
            return;
        }

        if( viewModel.TryGetWindowPlacement( out var left, out var top, out var width, out var height, out var isMaximized ) ) {
            Width = Math.Max( MinWidth, width );
            Height = Math.Max( MinHeight, height );
            Left = Math.Max( 0, left );
            Top = Math.Max( 0, top );

            if( isMaximized ) {
                WindowState = WindowState.Maximized;
            }
        }
    }

    private void OnClosed( object? sender, EventArgs e ) {
        SaveWindowPlacement();
        DesktopBootstrap.ThemeChanged -= ApplyWindowTheme;
    }

    private void OnWindowPlacementChanged( object? sender, EventArgs e ) {
        SaveWindowPlacement();
    }

    private void SaveWindowPlacement() {
        if( DataContext is not MainWindowViewModel viewModel ) {
            return;
        }

        var bounds = WindowState == WindowState.Normal
            ? new Rect( Left, Top, Width, Height )
            : RestoreBounds;
        viewModel.UpdateWindowPlacement( bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized );
    }

    private void ApplyWindowTheme( ThemeMode mode ) {
        var hwnd = new WindowInteropHelper( this ).Handle;
        if( hwnd == IntPtr.Zero ) {
            return;
        }

        var useDark = DesktopBootstrap.ResolveEffectiveTheme( mode ) == ThemeMode.Dark;
        var value = useDark ? 1 : 0;
        try {
            DwmSetWindowAttribute( hwnd, 20, ref value, sizeof( int ) );
        }
        catch {
            try {
                DwmSetWindowAttribute( hwnd, 19, ref value, sizeof( int ) );
            }
            catch {
            }
        }
    }

    [DllImport( "dwmapi.dll" )]
    private static extern int DwmSetWindowAttribute( IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize );
}
