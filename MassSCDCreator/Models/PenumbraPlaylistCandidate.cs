namespace MassSCDCreator.Models;

/// <summary>
/// An existing playlist in a v3 group file or a v4 <c>meta.json</c>.
/// In v4, <see cref="GroupName"/> distinguishes playlists sharing the same path.
/// </summary>
public sealed class PenumbraPlaylistCandidate {
    public string Path { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public bool IsV4 { get; init; }
    public int TrackCount { get; init; }
    public string RelativeFolder { get; init; } = string.Empty;
    public bool HasMixedFolders { get; init; }
    public IReadOnlyList<string> GamePaths { get; init; } = [];

    public string DisplayText => TrackCount == 1
        ? $"{GroupName} (1 track)"
        : $"{GroupName} ({TrackCount} tracks)";

    // ComboBox accessibility uses ToString(); return the label instead of the type name.
    public override string ToString() => DisplayText;
}
