namespace MassSCDCreator.Models;

public sealed class PenumbraGamePathCandidate {
    public required string Path { get; init; }
    public required int Occurrences { get; init; }
    public string DisplayText => $"{Path} ({Occurrences})";
}
