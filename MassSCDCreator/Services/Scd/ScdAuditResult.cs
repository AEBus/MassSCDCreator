namespace MassSCDCreator.Services.Scd;

public sealed class ScdAuditResult {
    public required string SourcePath { get; init; }
    public required int SoundCount { get; init; }
    public required int TrackCount { get; init; }
    public required int AudioCount { get; init; }
    public required int LayoutCount { get; init; }
    public required int AttributeCount { get; init; }
    public required int ParsedTrackCount { get; init; }
    public required int SoundType { get; init; }
    public required int SoundAttributes { get; init; }
    public required bool HasBusDucking { get; init; }
    public required bool HasExtra { get; init; }
    public required int? PlayTimeLengthMs { get; init; }
    public required int SampleRate { get; init; }
    public required int ChannelCount { get; init; }
    public required int DataLength { get; init; }
    public required int LoopStart { get; init; }
    public required int LoopEnd { get; init; }
    public required string AudioFormat { get; init; }
    public required double DurationMs { get; init; }
}
