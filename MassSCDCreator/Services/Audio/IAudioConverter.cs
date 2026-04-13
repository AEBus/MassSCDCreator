using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Audio;

public interface IAudioConverter {
    Task<string> ConvertAudioToOggAsync( string inputPath, string oggOutputPath, OggConversionOptions options, CancellationToken cancellationToken );
    Task<string> ConvertOggToOggAsync( string oggPath, string oggOutputPath, OggConversionOptions options, CancellationToken cancellationToken );
    Task<string> ResolveFfmpegPathAsync( string? configuredPath, CancellationToken cancellationToken );
}
