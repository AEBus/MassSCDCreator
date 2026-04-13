namespace MassSCDCreator.Models;

public sealed class PenumbraExportOptions {
    public bool Enabled { get; init; }
    public string ModRootPath { get; init; } = string.Empty;
    public PenumbraPlaylistExportMode ExportMode { get; init; } = PenumbraPlaylistExportMode.CreateNew;
    public string PlaylistName { get; init; } = string.Empty;
    public string ExistingPlaylistPath { get; init; } = string.Empty;
    public string RelativeScdFolder { get; init; } = "MyPlaylist\\Tracks";
    public IReadOnlyList<string> GamePaths { get; init; } = [];
}
