namespace MassSCDCreator.Models;

public sealed class BatchProcessResult {
    public int TotalCount { get; init; }
    public int SuccessCount { get; init; }
    public int ErrorCount { get; init; }
    public bool WasCancelled { get; init; }
    public string? ExportedPlaylistPath { get; init; }
}
