using System.Buffers.Binary;
using System.IO;

namespace MassSCDCreator.Services.Scd;

internal sealed class ScdFileModel {
    public required ScdHeader Header { get; init; }
    public required short SoundCount { get; init; }
    public required short TrackCount { get; init; }
    public required short AudioCount { get; init; }
    public required short UnknownOffset { get; init; }
    public required int EofPaddingSize { get; init; }
    public required List<ScdLayoutEntryModel> LayoutEntries { get; init; }
    public required List<ScdSoundEntryModel> SoundEntries { get; init; }
    public required List<ScdTrackEntryModel> TrackEntries { get; init; }
    public required List<ScdAttributeEntryModel> AttributeEntries { get; init; }
    public required List<ScdAudioEntry> AudioEntries { get; init; }

    public static ScdFileModel Load( BinaryReader reader, ScdHeader header, ScdOffsetTable offsetTable ) {
        var allOffsets = offsetTable.AllNonZeroOffsets.OrderBy( value => value ).ToArray();
        return new ScdFileModel {
            Header = header,
            SoundCount = ( short )offsetTable.SoundOffsets.Count,
            TrackCount = ( short )offsetTable.TrackOffsets.Count,
            AudioCount = ( short )offsetTable.AudioOffsets.Count,
            UnknownOffset = offsetTable.UnknownOffset,
            EofPaddingSize = offsetTable.EofPaddingSize,
            LayoutEntries = ReadLayoutEntries( reader, offsetTable.LayoutOffsets, allOffsets, header.FileSize ),
            SoundEntries = ReadSoundEntries( reader, offsetTable.SoundOffsets, allOffsets, header.FileSize ),
            TrackEntries = ReadTrackEntries( reader, offsetTable.TrackOffsets, allOffsets, header.FileSize ),
            AttributeEntries = ReadAttributeEntries( reader, offsetTable.AttributeOffsets, allOffsets, header.FileSize ),
            AudioEntries = ReadAudioEntries( reader, offsetTable.AudioOffsets )
        };
    }

    public ScdFileModel Clone() {
        return new ScdFileModel {
            Header = Header with { },
            SoundCount = SoundCount,
            TrackCount = TrackCount,
            AudioCount = AudioCount,
            UnknownOffset = UnknownOffset,
            EofPaddingSize = EofPaddingSize,
            LayoutEntries = LayoutEntries.Select( entry => entry.Clone() ).ToList(),
            SoundEntries = SoundEntries.Select( entry => entry.Clone() ).ToList(),
            TrackEntries = TrackEntries.Select( entry => entry.Clone() ).ToList(),
            AttributeEntries = AttributeEntries.Select( entry => entry.Clone() ).ToList(),
            AudioEntries = AudioEntries.Select( entry => entry.Clone() ).ToList()
        };
    }

    public void UpdateSoundPlayTimeLength( TimeSpan duration ) {
        foreach( var entry in SoundEntries ) {
            entry.UpdatePlayTimeLength( duration );
        }
    }

    public void Write( BinaryWriter writer ) {
        Header.Write( writer );

        writer.Write( SoundCount );
        writer.Write( TrackCount );
        writer.Write( AudioCount );
        writer.Write( UnknownOffset );

        var mainOffsetsPosition = writer.BaseStream.Position;
        writer.Write( 0 );
        writer.Write( 0 );
        writer.Write( 0 );
        writer.Write( 0 );
        writer.Write( 0 );
        writer.Write( EofPaddingSize );

        var soundOffsetTablePosition = ReserveOffsetTable( writer, SoundEntries.Count );
        var trackOffsetTablePosition = ReserveOffsetTable( writer, TrackEntries.Count );
        var audioOffsetTablePosition = ReserveOffsetTable( writer, AudioEntries.Count );
        var layoutOffsetTablePosition = ReserveOffsetTable( writer, LayoutEntries.Count );
        var attributeOffsetTablePosition = ReserveOffsetTable( writer, AttributeEntries.Count, zeroWhenEmpty: true );

        WriteMainOffsetTable( writer, mainOffsetsPosition, trackOffsetTablePosition, audioOffsetTablePosition, layoutOffsetTablePosition, attributeOffsetTablePosition );

        WriteLayoutSection( writer, layoutOffsetTablePosition, LayoutEntries );
        WriteSoundSection( writer, soundOffsetTablePosition, SoundEntries );
        WriteTrackSection( writer, trackOffsetTablePosition, TrackEntries );
        ScdBinaryHelpers.PadWriterTo( writer, 16 );
        WriteAttributeSection( writer, attributeOffsetTablePosition, AttributeEntries );
        WriteAudioSection( writer, audioOffsetTablePosition, AudioEntries );
    }

    private static List<byte[]> ReadRawEntries( BinaryReader reader, IReadOnlyList<int> offsets, IReadOnlyList<int> allOffsets, int fileSize ) {
        var result = new List<byte[]>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            var nextOffset = allOffsets.FirstOrDefault( candidate => candidate > offset );
            var endOffset = nextOffset == 0 ? fileSize : nextOffset;
            reader.BaseStream.Position = offset;
            result.Add( reader.ReadBytes( endOffset - offset ) );
        }

        return result;
    }

    private static List<ScdAudioEntry> ReadAudioEntries( BinaryReader reader, IReadOnlyList<int> offsets ) {
        var result = new List<ScdAudioEntry>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            reader.BaseStream.Position = offset;
            var entry = ScdAudioEntry.Read( reader );
            if( entry.DataLength > 0 ) {
                result.Add( entry );
            }
        }

        return result;
    }

    private static List<ScdLayoutEntryModel> ReadLayoutEntries( BinaryReader reader, IReadOnlyList<int> offsets, IReadOnlyList<int> allOffsets, int fileSize ) {
        var result = new List<ScdLayoutEntryModel>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            var nextOffset = allOffsets.FirstOrDefault( candidate => candidate > offset );
            var endOffset = nextOffset == 0 ? fileSize : nextOffset;
            reader.BaseStream.Position = offset;
            result.Add( ScdLayoutEntryModel.Read( reader.ReadBytes( endOffset - offset ) ) );
        }

        return result;
    }

    private static List<ScdSoundEntryModel> ReadSoundEntries( BinaryReader reader, IReadOnlyList<int> offsets, IReadOnlyList<int> allOffsets, int fileSize ) {
        var result = new List<ScdSoundEntryModel>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            var nextOffset = allOffsets.FirstOrDefault( candidate => candidate > offset );
            var endOffset = nextOffset == 0 ? fileSize : nextOffset;
            reader.BaseStream.Position = offset;
            result.Add( ScdSoundEntryModel.Read( reader.ReadBytes( endOffset - offset ) ) );
        }

        return result;
    }

    private static List<ScdAttributeEntryModel> ReadAttributeEntries( BinaryReader reader, IReadOnlyList<int> offsets, IReadOnlyList<int> allOffsets, int fileSize ) {
        var result = new List<ScdAttributeEntryModel>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            var nextOffset = allOffsets.FirstOrDefault( candidate => candidate > offset );
            var endOffset = nextOffset == 0 ? fileSize : nextOffset;
            reader.BaseStream.Position = offset;
            result.Add( ScdAttributeEntryModel.Read( reader.ReadBytes( endOffset - offset ) ) );
        }

        return result;
    }

    private static int ReserveOffsetTable( BinaryWriter writer, int count, bool zeroWhenEmpty = false ) {
        if( count == 0 ) {
            return zeroWhenEmpty ? 0 : ( int )writer.BaseStream.Position;
        }

        var position = ( int )writer.BaseStream.Position;
        for( var index = 0; index < count; index++ ) {
            writer.Write( 0 );
        }

        ScdBinaryHelpers.PadWriterTo( writer, 16 );
        return position;
    }

    private static void WriteMainOffsetTable( BinaryWriter writer, long position, int trackOffset, int audioOffset, int layoutOffset, int attributeOffset ) {
        var savedPosition = writer.BaseStream.Position;
        writer.BaseStream.Position = position;
        writer.Write( trackOffset );
        writer.Write( audioOffset );
        writer.Write( layoutOffset );
        writer.Write( 0 );
        writer.Write( attributeOffset );
        writer.BaseStream.Position = savedPosition;
    }

    private static void WriteRawSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<byte[]> entries ) {
        if( entries.Count == 0 || offsetTablePosition == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            writer.Write( entry );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }

    private static void WriteAudioSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<ScdAudioEntry> entries ) {
        if( entries.Count == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            entry.Write( writer );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }

    private static List<ScdTrackEntryModel> ReadTrackEntries( BinaryReader reader, IReadOnlyList<int> offsets, IReadOnlyList<int> allOffsets, int fileSize ) {
        var result = new List<ScdTrackEntryModel>();
        foreach( var offset in offsets.Where( offset => offset > 0 ) ) {
            var nextOffset = allOffsets.FirstOrDefault( candidate => candidate > offset );
            var endOffset = nextOffset == 0 ? fileSize : nextOffset;
            reader.BaseStream.Position = offset;
            result.Add( ScdTrackEntryModel.Read( reader.ReadBytes( endOffset - offset ) ) );
        }

        return result;
    }

    private static void WriteTrackSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<ScdTrackEntryModel> entries ) {
        if( entries.Count == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            entry.Write( writer );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }

    private static void WriteSoundSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<ScdSoundEntryModel> entries ) {
        if( entries.Count == 0 || offsetTablePosition == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            entry.Write( writer );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }

    private static void WriteLayoutSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<ScdLayoutEntryModel> entries ) {
        if( entries.Count == 0 || offsetTablePosition == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            entry.Write( writer );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }

    private static void WriteAttributeSection( BinaryWriter writer, int offsetTablePosition, IReadOnlyList<ScdAttributeEntryModel> entries ) {
        if( entries.Count == 0 || offsetTablePosition == 0 ) {
            return;
        }

        var offsets = new List<int>( entries.Count );
        foreach( var entry in entries ) {
            offsets.Add( ( int )writer.BaseStream.Position );
            entry.Write( writer );
        }

        var savePosition = writer.BaseStream.Position;
        writer.BaseStream.Position = offsetTablePosition;
        foreach( var offset in offsets ) {
            writer.Write( offset );
        }
        writer.BaseStream.Position = savePosition;
    }
}

internal sealed class ScdAudioEntry {
    private const int AudioMarkerFlag = 0x01;

    public int DataLength { get; set; }
    public int NumChannels { get; set; }
    public int SampleRate { get; set; }
    public SscfWaveFormat Format { get; set; }
    public int LoopStart { get; set; }
    public int LoopEnd { get; set; }
    public int Flags { get; set; }
    public ScdAudioMarker? Marker { get; set; }
    public required ScdAudioDataModel Data { get; set; }
    public TimeSpan Duration { get; set; }
    public bool HasMarker => ( Flags & AudioMarkerFlag ) != 0;

    public static ScdAudioEntry Read( BinaryReader reader ) {
        var entry = new ScdAudioEntry {
            DataLength = reader.ReadInt32(),
            NumChannels = reader.ReadInt32(),
            SampleRate = reader.ReadInt32(),
            Format = ( SscfWaveFormat )reader.ReadInt32(),
            LoopStart = reader.ReadInt32(),
            LoopEnd = reader.ReadInt32(),
            Data = ScdRawAudioData.Empty
        };

        var subInfoSize = reader.ReadInt32();
        entry.Flags = reader.ReadInt32();

        if( entry.HasMarker ) {
            entry.Marker = ScdAudioMarker.Read( reader, entry.SampleRate );
            subInfoSize -= entry.Marker.GetSize();
        }

        if( entry.DataLength > 0 ) {
            entry.Data = entry.Format switch {
                SscfWaveFormat.Vorbis => ScdVorbisData.Read( reader, entry ),
                _ => ScdRawAudioData.Read( reader, entry, subInfoSize )
            };
            var totalSamples = entry.Data.GetTotalSamples();
            entry.Duration = totalSamples > 0
                ? TimeSpan.FromSeconds( totalSamples / ( double )entry.SampleRate )
                : TimeSpan.Zero;
            ScdBinaryHelpers.PadReaderTo( reader, 16 );
        }

        return entry;
    }

    public ScdAudioEntry Clone() {
        return new ScdAudioEntry {
            DataLength = DataLength,
            NumChannels = NumChannels,
            SampleRate = SampleRate,
            Format = Format,
            LoopStart = LoopStart,
            LoopEnd = LoopEnd,
            Flags = Flags,
            Marker = Marker?.Clone(),
            Data = Data.Clone(),
            Duration = Duration
        };
    }

    public void Write( BinaryWriter writer ) {
        writer.Write( DataLength );
        writer.Write( NumChannels );
        writer.Write( SampleRate );
        writer.Write( ( int )Format );
        writer.Write( LoopStart );
        writer.Write( LoopEnd );

        var markerSize = HasMarker ? Marker?.GetSize() ?? 0 : 0;
        writer.Write( markerSize + Data.GetSubInfoSize() );
        writer.Write( Flags );

        if( HasMarker ) {
            ( Marker ?? new ScdAudioMarker() ).Write( writer, SampleRate );
        }

        Data.Write( writer );
        ScdBinaryHelpers.PadWriterTo( writer, 16 );
    }
}

internal abstract class ScdAudioDataModel {
    public abstract int DataLength { get; }
    public abstract void Write( BinaryWriter writer );
    public abstract int GetSubInfoSize();
    public abstract int GetTotalSamples();
    public abstract int SamplesToBytes( long samples );
    public abstract ScdAudioDataModel Clone();
}

internal sealed class ScdRawAudioData : ScdAudioDataModel {
    public static readonly ScdRawAudioData Empty = new( [], [] );

    public byte[] SubInfo { get; init; }
    public byte[] AudioBytes { get; init; }
    public override int DataLength => AudioBytes.Length;

    private ScdRawAudioData( byte[] subInfo, byte[] audioBytes ) {
        SubInfo = subInfo;
        AudioBytes = audioBytes;
    }

    public static ScdRawAudioData Read( BinaryReader reader, ScdAudioEntry entry, int subInfoSize ) {
        var subInfo = subInfoSize > 0 ? reader.ReadBytes( subInfoSize ) : [];
        var audioBytes = entry.DataLength > 0 ? reader.ReadBytes( entry.DataLength ) : [];
        return new ScdRawAudioData( subInfo, audioBytes );
    }

    public override void Write( BinaryWriter writer ) {
        writer.Write( SubInfo );
        writer.Write( AudioBytes );
    }

    public override int GetSubInfoSize() => SubInfo.Length;
    public override int GetTotalSamples() => 0;
    public override int SamplesToBytes( long samples ) => 0;
    public override ScdAudioDataModel Clone() => new ScdRawAudioData( SubInfo.ToArray(), AudioBytes.ToArray() );
}

internal sealed class ScdAudioMarker {
    public string Id { get; set; } = "MARK";
    public double LoopStartSeconds { get; set; }
    public double LoopEndSeconds { get; set; }
    public List<double> Markers { get; } = [];

    public static ScdAudioMarker Read( BinaryReader reader, int sampleRate ) {
        var marker = new ScdAudioMarker {
            Id = ScdBinaryHelpers.ReadFixedString( reader, 4 )
        };

        var size = reader.ReadInt32();
        marker.LoopStartSeconds = reader.ReadInt32() / ( double )sampleRate;
        marker.LoopEndSeconds = reader.ReadInt32() / ( double )sampleRate;
        var markerCount = reader.ReadInt32();

        for( var index = 0; index < markerCount; index++ ) {
            marker.Markers.Add( reader.ReadInt32() / ( double )sampleRate );
        }

        var consumed = 20 + ( markerCount * 4 );
        var padding = size - consumed;
        if( padding > 0 ) {
            reader.ReadBytes( padding );
        }

        return marker;
    }

    public int GetSize() {
        var size = 20 + ( Markers.Count * 4 );
        return size + ( int )ScdBinaryHelpers.NumberToPad( size, 16 );
    }

    public ScdAudioMarker Clone() {
        var marker = new ScdAudioMarker {
            Id = Id,
            LoopStartSeconds = LoopStartSeconds,
            LoopEndSeconds = LoopEndSeconds
        };
        marker.Markers.AddRange( Markers );
        return marker;
    }

    public void ApplyReplacement( int sampleRate, long? loopStartSamples, long? loopEndSamples ) {
        LoopStartSeconds = loopStartSamples.HasValue ? loopStartSamples.Value / ( double )sampleRate : 0d;
        LoopEndSeconds = loopEndSamples.HasValue ? loopEndSamples.Value / ( double )sampleRate : 0d;
        Markers.Clear();
    }

    public void Write( BinaryWriter writer, int sampleRate ) {
        ScdBinaryHelpers.WriteFixedString( writer, Id, 4 );
        writer.Write( GetSize() );
        writer.Write( ( int )Math.Round( LoopStartSeconds * sampleRate, MidpointRounding.AwayFromZero ) );
        writer.Write( ( int )Math.Round( LoopEndSeconds * sampleRate, MidpointRounding.AwayFromZero ) );
        writer.Write( Markers.Count );

        foreach( var marker in Markers ) {
            writer.Write( ( int )Math.Round( marker * sampleRate, MidpointRounding.AwayFromZero ) );
        }

        ScdBinaryHelpers.PadWriterTo( writer, 16 );
    }
}

internal sealed class ScdVorbisData : ScdAudioDataModel {
    public static readonly ScdVorbisData Empty = new( [], [], [], 0, 0, 0, 0, 0.1f, [], 0, 0, 0, 44100, false );

    public byte[] OggData { get; init; }
    public byte[] EncodedHeader { get; init; }
    public byte[] DecodedHeader { get; init; }
    public short EncodeMode { get; init; }
    public short EncodeByte { get; init; }
    public int XorOffset { get; init; }
    public int XorSize { get; init; }
    public int VorbisHeaderSize { get; init; }
    public float SeekStep { get; private set; }
    public List<int> SeekTable { get; init; }
    public int Unknown1 { get; init; }
    public int Unknown2 { get; init; }
    public int SampleRate { get; private set; }
    public bool LegacyImported { get; init; }
    public override int DataLength => OggData.Length;

    private ScdVorbisData( byte[] oggData, byte[] encodedHeader, byte[] decodedHeader, short encodeMode, short encodeByte, int xorOffset, int xorSize, float seekStep, List<int> seekTable, int vorbisHeaderSize, int unknown1, int unknown2, int sampleRate, bool legacyImported ) {
        OggData = oggData;
        EncodedHeader = encodedHeader;
        DecodedHeader = decodedHeader;
        EncodeMode = encodeMode;
        EncodeByte = encodeByte;
        XorOffset = xorOffset;
        XorSize = xorSize;
        VorbisHeaderSize = vorbisHeaderSize;
        SeekStep = seekStep;
        SeekTable = seekTable;
        Unknown1 = unknown1;
        Unknown2 = unknown2;
        SampleRate = sampleRate;
        LegacyImported = legacyImported;
    }

    public static ScdVorbisData Read( BinaryReader reader, ScdAudioEntry entry ) {
        var encodeMode = reader.ReadInt16();
        var encodeByte = reader.ReadInt16();
        var xorOffset = reader.ReadInt32();
        var xorSize = reader.ReadInt32();
        var seekStep = reader.ReadSingle();
        var seekTableSize = reader.ReadInt32();

        if( seekTableSize >= 0x00726F76 && seekTableSize <= 0x7A726F76 ) {
            reader.ReadBytes( 0x35C );
            var legacyOggData = reader.ReadBytes( entry.DataLength + 0x10 );
            return new ScdVorbisData( legacyOggData, [], [], 0, 0, 0, 0, 0.1f, [], 0, 0, 0, entry.SampleRate, true );
        }

        var vorbisHeaderSize = reader.ReadInt32();
        var unknown1 = reader.ReadInt32();
        var unknown2 = reader.ReadInt32();
        var seekTable = new List<int>( Math.Max( seekTableSize / 4, 0 ) );
        for( var index = 0; index < seekTableSize / 4; index++ ) {
            seekTable.Add( reader.ReadInt32() );
        }

        var encodedHeader = reader.ReadBytes( vorbisHeaderSize );
        var decodedHeader = encodedHeader.ToArray();
        if( encodeMode == 0x2002 && encodeByte != 0 ) {
            ScdBinaryHelpers.XorDecode( decodedHeader, ( byte )encodeByte );
        }

        var decodedData = reader.ReadBytes( entry.DataLength );
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( decodedHeader );
        writer.Write( decodedData );
        var oggData = stream.ToArray();

        if( encodeMode == 0x2003 ) {
            ScdBinaryHelpers.XorDecodeFromTable( oggData, decodedData.Length );
        }

        return new ScdVorbisData( oggData, encodedHeader, decodedHeader, encodeMode, encodeByte, xorOffset, xorSize, seekStep, seekTable, vorbisHeaderSize, unknown1, unknown2, entry.SampleRate, false );
    }

    public static ScdVorbisData Create( byte[] oggData, int sampleRate ) {
        var data = new ScdVorbisData( oggData, [], [], 0, 0, 0, 0, 0.1f, [], 0, 0, 0, sampleRate, false );
        data.PopulateSeekTable();
        return data;
    }

    public override ScdAudioDataModel Clone() => new ScdVorbisData( OggData.ToArray(), EncodedHeader.ToArray(), DecodedHeader.ToArray(), EncodeMode, EncodeByte, XorOffset, XorSize, SeekStep, SeekTable.ToList(), VorbisHeaderSize, Unknown1, Unknown2, SampleRate, LegacyImported );

    public override int SamplesToBytes( long samples ) {
        var time = samples / ( double )SampleRate;
        return TimeToBytes( time );
    }

    public int TimeToBytes( double time ) {
        if( SeekTable.Count == 0 ) {
            return 0;
        }

        for( var index = 0; index < SeekTable.Count; index++ ) {
            if( index * SeekStep > time ) {
                return SeekTable[Math.Max( index - 1, 0 )];
            }
        }

        return OggData.Length - VorbisHeaderSize;
    }

    public override void Write( BinaryWriter writer ) {
        writer.Write( EncodeMode );
        writer.Write( EncodeByte );
        writer.Write( XorOffset );
        writer.Write( XorSize );
        writer.Write( SeekStep );
        writer.Write( SeekTable.Count * 4 );
        writer.Write( VorbisHeaderSize );
        writer.Write( Unknown1 );
        writer.Write( Unknown2 );
        foreach( var item in SeekTable ) {
            writer.Write( item );
        }
        if( VorbisHeaderSize > 0 && EncodedHeader.Length > 0 ) {
            writer.Write( EncodedHeader );
            writer.Write( OggData.AsSpan( DecodedHeader.Length ) );
        }
        else {
            writer.Write( OggData );
        }
    }

    public override int GetSubInfoSize() => 0x20 + ( SeekTable.Count * 4 ) + VorbisHeaderSize;
    public override int GetTotalSamples() {
        long maxGranule = 0;
        var end = OggData.Length - 5;
        for( var index = 0; index <= end; index++ ) {
            if( OggData[index] == 0x4F && OggData[index + 1] == 0x67 && OggData[index + 2] == 0x67 && OggData[index + 3] == 0x53 && OggData[index + 4] == 0x00 ) {
                var granule = BinaryPrimitives.ReadInt64LittleEndian( OggData.AsSpan( index + 6, 8 ) );
                if( granule > maxGranule ) {
                    maxGranule = granule;
                }
            }
        }

        return maxGranule > 0 && maxGranule <= int.MaxValue ? ( int )maxGranule : 0;
    }

    private void PopulateSeekTable() {
        SeekTable.Clear();

        foreach( var offset in Locate( OggData, ScdBinaryHelpers.OggPagePattern ) ) {
            var position = offset - VorbisHeaderSize;
            if( position < 0 || offset + 10 > OggData.Length ) {
                continue;
            }

            var maxSamples = BinaryPrimitives.ReadInt32LittleEndian( OggData.AsSpan( offset + 6, 4 ) );
            var maxTime = maxSamples / ( float )SampleRate;

            if( SeekTable.Count == 1 && maxTime > SeekStep ) {
                SeekStep = maxTime;
            }

            if( ( ( SeekStep * SeekTable.Count ) - maxTime ) < 0.02f ) {
                SeekTable.Add( position );
            }
        }
    }

    private static IEnumerable<int> Locate( byte[] data, byte[] candidate ) {
        if( data.Length == 0 || candidate.Length == 0 || candidate.Length > data.Length ) {
            yield break;
        }

        for( var index = 0; index <= data.Length - candidate.Length; index++ ) {
            var match = true;
            for( var inner = 0; inner < candidate.Length; inner++ ) {
                if( data[index + inner] != candidate[inner] ) {
                    match = false;
                    break;
                }
            }

            if( match ) {
                yield return index;
            }
        }
    }
}
