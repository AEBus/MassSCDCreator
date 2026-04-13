using System.Diagnostics;
using System.Globalization;
using System.IO;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Audio;

public sealed class FfmpegAudioConverter : IAudioConverter {
    private const string LoudnessNormalizationFilter = "loudnorm=I=-14:TP=-1:LRA=11";
    private readonly IFfmpegInstaller? _ffmpegInstaller;

    public FfmpegAudioConverter() {
    }

    public FfmpegAudioConverter( IFfmpegInstaller ffmpegInstaller ) {
        _ffmpegInstaller = ffmpegInstaller;
    }

    public async Task<string> ConvertAudioToOggAsync( string inputPath, string oggOutputPath, OggConversionOptions options, CancellationToken cancellationToken ) {
        var ffmpegPath = await ResolveFfmpegPathAsync( options.FfmpegPath, cancellationToken );
        return await ConvertToOggCoreAsync( ffmpegPath, inputPath, oggOutputPath, options, cancellationToken );
    }

    public async Task<string> ConvertOggToOggAsync( string oggPath, string oggOutputPath, OggConversionOptions options, CancellationToken cancellationToken ) {
        var ffmpegPath = await ResolveFfmpegPathAsync( options.FfmpegPath, cancellationToken );
        return await ConvertToOggCoreAsync( ffmpegPath, oggPath, oggOutputPath, options, cancellationToken );
    }

    private static async Task<string> ConvertToOggCoreAsync( string ffmpegPath, string inputPath, string oggOutputPath, OggConversionOptions options, CancellationToken cancellationToken ) {
        Directory.CreateDirectory( Path.GetDirectoryName( oggOutputPath )! );
        var samePath = string.Equals(
            Path.GetFullPath( inputPath ),
            Path.GetFullPath( oggOutputPath ),
            StringComparison.OrdinalIgnoreCase );
        // I used to sneer at temp-file dances. Then I got older, calmer, and less interested in debugging self-overwrites from ffmpeg.
        var actualOutputPath = samePath
            ? Path.Combine(
                Path.GetDirectoryName( oggOutputPath )!,
                Path.GetFileNameWithoutExtension( oggOutputPath ) + "." + Guid.NewGuid().ToString( "N" ) + ".tmp.ogg" )
            : oggOutputPath;

        if( File.Exists( actualOutputPath ) ) {
            File.Delete( actualOutputPath );
        }

        var qualityArg = options.AdvancedMode == OggAdvancedMode.QualityVbr
            ? $"-q:a {options.QualityLevel.ToString( "0.0", CultureInfo.InvariantCulture )}"
            : $"-b:a {options.NominalBitrateKbps}k";

        var loudnessArgs = options.NormalizeLoudness ? $"-af \"{LoudnessNormalizationFilter}\" " : string.Empty;
        var arguments = $"-y -hide_banner -loglevel error -i \"{inputPath}\" -vn -map_metadata -1 {loudnessArgs}-ac 2 -ar 44100 -c:a libvorbis {qualityArg} \"{actualOutputPath}\"";
        await RunProcessAsync( ffmpegPath, arguments, cancellationToken );

        if( !File.Exists( actualOutputPath ) ) {
            throw new InvalidOperationException( "FFmpeg exited without an error, but the OGG file was not created." );
        }

        if( samePath ) {
            File.Delete( oggOutputPath );
            File.Move( actualOutputPath, oggOutputPath );
        }

        return oggOutputPath;
    }

    public async Task<string> ResolveFfmpegPathAsync( string? configuredPath, CancellationToken cancellationToken ) {
        if( !string.IsNullOrWhiteSpace( configuredPath ) ) {
            if( File.Exists( configuredPath ) ) {
                return configuredPath;
            }

            throw new FileNotFoundException( $"The configured ffmpeg executable was not found: {configuredPath}" );
        }

        if( _ffmpegInstaller is not null && File.Exists( _ffmpegInstaller.ManagedFfmpegPath ) ) {
            return _ffmpegInstaller.ManagedFfmpegPath;
        }

        var bundledPath = GetBundledFfmpegPath();
        if( File.Exists( bundledPath ) ) {
            return bundledPath;
        }

        var psi = new ProcessStartInfo {
            FileName = "where.exe",
            Arguments = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start( psi ) ?? throw new InvalidOperationException( "Could not start where.exe to locate ffmpeg." );
        using var registration = cancellationToken.Register( () => TryKill( process ) );

        var output = await process.StandardOutput.ReadToEndAsync( cancellationToken );
        var error = await process.StandardError.ReadToEndAsync( cancellationToken );
        await process.WaitForExitAsync( cancellationToken );

        if( process.ExitCode != 0 ) {
            throw new FileNotFoundException( "ffmpeg.exe was not found in PATH. Please select it manually.", error );
        }

        var firstMatch = output
            .Split( [Environment.NewLine], StringSplitOptions.RemoveEmptyEntries )
            .Select( line => line.Trim() )
            .FirstOrDefault( File.Exists );

        return firstMatch ?? throw new FileNotFoundException( "ffmpeg.exe was not found in PATH. Please select it manually." );
    }

    private static string GetBundledFfmpegPath() => Path.Combine( AppContext.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe" );

    private static async Task RunProcessAsync( string fileName, string arguments, CancellationToken cancellationToken ) {
        var psi = new ProcessStartInfo {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start( psi ) ?? throw new InvalidOperationException( $"Could not start process {fileName}." );
        using var registration = cancellationToken.Register( () => TryKill( process ) );

        var stderrTask = process.StandardError.ReadToEndAsync( cancellationToken );
        var stdoutTask = process.StandardOutput.ReadToEndAsync( cancellationToken );
        await process.WaitForExitAsync( cancellationToken );

        var stderr = await stderrTask;
        var stdout = await stdoutTask;
        if( process.ExitCode != 0 ) {
            throw new InvalidOperationException( string.IsNullOrWhiteSpace( stderr ) ? stdout : stderr );
        }
    }

    private static void TryKill( Process process ) {
        try {
            if( !process.HasExited ) {
                process.Kill( true );
            }
        }
        catch {
        }
    }
}
