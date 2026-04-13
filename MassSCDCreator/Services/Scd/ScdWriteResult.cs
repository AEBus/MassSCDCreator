namespace MassSCDCreator.Services.Scd;

public sealed class ScdWriteResult {
    public required string OutputPath { get; init; }
    public required TimeSpan Duration { get; init; }
    public required int SampleRate { get; init; }
    public required int ChannelCount { get; init; }
}
