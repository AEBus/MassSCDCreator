namespace MassSCDCreator.Models;

public sealed class PenumbraGamePathCandidate {
    public required string Path { get; init; }
    public required int Occurrences { get; init; }
    public string DisplayText => $"{Path} ({Occurrences})";

    // ComboBox accessibility uses ToString(); return the label instead of the type name.
    public override string ToString() => DisplayText;
}
