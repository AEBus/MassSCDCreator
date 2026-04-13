namespace MassSCDCreator.Services.Audio;

public interface IFfmpegInstaller {
    string ManagedFfmpegPath { get; }
    Task<string> EnsureInstalledAsync( bool forceUpdate, CancellationToken cancellationToken );
}

