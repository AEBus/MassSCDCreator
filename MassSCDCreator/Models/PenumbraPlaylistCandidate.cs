namespace MassSCDCreator.Models;

public sealed class PenumbraPlaylistCandidate {
    public string Path { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string RelativeFolder { get; init; } = string.Empty;
    public string PreferredGamePath { get; init; } = string.Empty;
    public IReadOnlyList<string> GamePaths { get; init; } = [];
}
