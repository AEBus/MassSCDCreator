namespace MassSCDCreator.Models;

public sealed class ProcessingProgress {
    public int CurrentIndex { get; init; }
    public int TotalCount { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;

    public double Percent => TotalCount == 0 ? 0 : ( double )CurrentIndex / TotalCount * 100d;
}
