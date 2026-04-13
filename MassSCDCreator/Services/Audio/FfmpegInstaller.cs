using System.IO.Compression;
using System.Net.Http;
using System.IO;
using System.Diagnostics;

namespace MassSCDCreator.Services.Audio;

public sealed class FfmpegInstaller : IFfmpegInstaller {
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes( 10 ) };

    public string ManagedFfmpegPath => Path.Combine( GetManagedRootPath(), "ffmpeg.exe" );

    public async Task<string> EnsureInstalledAsync( bool forceUpdate, CancellationToken cancellationToken ) {
        var outputPath = ManagedFfmpegPath;
        if( !forceUpdate && File.Exists( outputPath ) ) {
            return outputPath;
        }

        var rootPath = GetManagedRootPath();
        Directory.CreateDirectory( rootPath );

        var tempPath = Path.Combine( Path.GetTempPath(), "MassSCDCreatorFfmpeg", Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( tempPath );

        try {
            var zipPath = Path.Combine( tempPath, "ffmpeg.zip" );
            await using( var zipStream = File.Create( zipPath ) ) {
                using var response = await HttpClient.GetAsync( DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken );
                response.EnsureSuccessStatusCode();
                await using var responseStream = await response.Content.ReadAsStreamAsync( cancellationToken );
                await responseStream.CopyToAsync( zipStream, cancellationToken );
            }

            using var archive = ZipFile.OpenRead( zipPath );
            var ffmpegEntry = archive.Entries
                .FirstOrDefault( entry =>
                    entry.FullName.EndsWith( "/ffmpeg.exe", StringComparison.OrdinalIgnoreCase ) &&
                    entry.FullName.Contains( "/bin/", StringComparison.OrdinalIgnoreCase ) )
                ?? archive.Entries.FirstOrDefault( entry => entry.FullName.EndsWith( "/ffmpeg.exe", StringComparison.OrdinalIgnoreCase ) );

            if( ffmpegEntry is null ) {
                throw new InvalidOperationException( "Downloaded archive does not contain ffmpeg.exe in bin." );
            }

            var extractedExePath = Path.Combine( tempPath, "ffmpeg.exe" );
            ffmpegEntry.ExtractToFile( extractedExePath, true );

            foreach( var oldFile in Directory.EnumerateFiles( rootPath, "*.exe", SearchOption.TopDirectoryOnly ) ) {
                try {
                    File.Delete( oldFile );
                }
                catch {
                }
            }

            File.Copy( extractedExePath, outputPath, true );

            await ValidateRequiredFormatSupportAsync( outputPath, cancellationToken );
            return outputPath;
        }
        finally {
            try {
                if( Directory.Exists( tempPath ) ) {
                    Directory.Delete( tempPath, true );
                }
            }
            catch {
            }
        }
    }

    private static string GetManagedRootPath() {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Tools",
            "ffmpeg" );
    }

    private static async Task ValidateRequiredFormatSupportAsync( string ffmpegPath, CancellationToken cancellationToken ) {
        var formatsOutput = await RunProcessCaptureStdoutAsync( ffmpegPath, "-hide_banner -formats", cancellationToken );
        var requiredDemuxers = new[] { " flac ", " ogg ", " wav ", " mov,mp4,m4a,3gp,3g2,mj2 ", " mp3 " };
        foreach( var required in requiredDemuxers ) {
            if( formatsOutput.IndexOf( required, StringComparison.OrdinalIgnoreCase ) < 0 ) {
                throw new InvalidOperationException( $"Installed FFmpeg build is missing required format support: {required.Trim()}" );
            }
        }
    }

    private static async Task<string> RunProcessCaptureStdoutAsync( string fileName, string arguments, CancellationToken cancellationToken ) {
        var psi = new ProcessStartInfo {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start( psi ) ?? throw new InvalidOperationException( $"Could not start process: {fileName}" );
        var stdoutTask = process.StandardOutput.ReadToEndAsync( cancellationToken );
        var stderrTask = process.StandardError.ReadToEndAsync( cancellationToken );
        await process.WaitForExitAsync( cancellationToken );

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if( process.ExitCode != 0 ) {
            throw new InvalidOperationException( string.IsNullOrWhiteSpace( stderr ) ? stdout : stderr );
        }

        return stdout;
    }
}
