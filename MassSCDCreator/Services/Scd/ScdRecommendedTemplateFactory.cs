namespace MassSCDCreator.Services.Scd;

internal static class ScdRecommendedTemplateFactory {
    private const string LayoutHex =
        "80-00-03-01-84-00-00-00-00-00-00-00-00-00-00-00-00-00-80-3F-00-00-80-3F-00-00-80-3F-00-00-80-3F-" +
        "00-00-00-00-00-00-00-00-00-00-00-00-00-00-80-3F-00-00-48-43-00-00-FA-43-00-00-20-41-00-00-00-00-00-00-" +
        "80-3F-00-00-80-3F-00-00-80-3F-00-00-80-3F-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
        "00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-D0-07-D0-07-00-00-00-00-00-00-00-00";

    private const string SoundHex =
        "01-10-C2-01-09-24-00-00-00-00-80-3F-01-00-00-00-10-01-00-00-B0-04-00-00-00-00-00-00-00-00-00-00-" +
        "02-00-10-00-01-88-02-00-00-00-00-00-00-C0-79-44-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00";

    private const string TrackHex =
        "2F-00-0B-00-05-00-00-00-00-00-1D-00-03-00-00-00-1D-00-02-00-00-00-1D-00-01-00-00-00-01-00-00-00-" +
        "80-3F-00-00-00-00-02-00-00-00-80-3F-00-00-00-00-06-00-00-00-00-00-00-00-00-00-21-00-00-00-00-00-" +
        "00-00-00-00-3F-00-00-00-06-00-00-00-07-00-03-00-00-00-00-00-0D-00-00-00-00-00-00-00-00-00-03-00-" +
        "01-88-02-00-0E-00-00-00-00-00-00-00-00-00-00-00";

    private const string AttributeHex =
        "02-00-22-00-22-00-01-01-00-00-00-00-00-00-00-00-00-01-00-00-00-00-00-00-00-00-00-00-15-40-00-01-" +
        "00-00-00-00-00-00-00-00-07-00-00-00-00-00-00-00-00-00-00-00-40-40-00-00-00-00-00-00-00-00-00-00-" +
        "00-00-00-00-00-00-00-00-00-00-00-00-40-40-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-" +
        "00-00-00-00-40-40-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00";

    public static ScdTemplate Create() {
        var model = new ScdFileModel {
            Header = new ScdHeader(
                Magic: 1111770451,
                SectionType: 1178817363,
                SedbVersion: 3,
                Endian: 0,
                AlignmentBits: 4,
                HeaderSize: 48,
                FileSize: 0,
                Padding: new byte[28]
            ),
            SoundCount = 1,
            TrackCount = 1,
            AudioCount = 1,
            UnknownOffset = 8206,
            EofPaddingSize = 0,
            LayoutEntries = [ScdLayoutEntryModel.Read( ParseHex( LayoutHex ) )],
            SoundEntries = [ScdSoundEntryModel.Read( ParseHex( SoundHex ) )],
            TrackEntries = [ScdTrackEntryModel.Read( ParseHex( TrackHex ) )],
            AttributeEntries = [ScdAttributeEntryModel.Read( ParseHex( AttributeHex ) )],
            AudioEntries = [CreatePlaceholderAudio()]
        };

        return new ScdTemplate( "Built-in recommended SCD profile", model );
    }

    private static ScdAudioEntry CreatePlaceholderAudio() {
        return new ScdAudioEntry {
            DataLength = 0,
            NumChannels = 2,
            SampleRate = 44100,
            Format = SscfWaveFormat.Vorbis,
            LoopStart = 0,
            LoopEnd = 0,
            Flags = 0x01000000,
            Marker = null,
            Data = ScdVorbisData.Empty,
            Duration = TimeSpan.Zero
        };
    }

    private static byte[] ParseHex( string hex ) {
        return Convert.FromHexString( hex.Replace( "-", string.Empty, StringComparison.Ordinal )
            .Replace( " ", string.Empty, StringComparison.Ordinal ) );
    }
}
