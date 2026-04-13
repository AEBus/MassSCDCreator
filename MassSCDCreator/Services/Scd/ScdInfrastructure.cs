using System.IO;
using System.Text;

namespace MassSCDCreator.Services.Scd;

internal sealed record ScdHeader( int Magic, int SectionType, int SedbVersion, byte Endian, byte AlignmentBits, short HeaderSize, int FileSize, byte[] Padding ) {
    public static ScdHeader Read( BinaryReader reader ) {
        return new ScdHeader(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadInt16(),
            reader.ReadInt32(),
            reader.ReadBytes( 28 )
        );
    }

    public void Write( BinaryWriter writer ) {
        writer.Write( Magic );
        writer.Write( SectionType );
        writer.Write( SedbVersion );
        writer.Write( Endian );
        writer.Write( AlignmentBits );
        writer.Write( HeaderSize );
        writer.Write( FileSize );
        writer.Write( Padding );
    }
}

internal sealed class ScdOffsetTable {
    public required List<int> SoundOffsets { get; init; }
    public required List<int> TrackOffsets { get; init; }
    public required List<int> AudioOffsets { get; init; }
    public required List<int> LayoutOffsets { get; init; }
    public required List<int> AttributeOffsets { get; init; }
    public required short UnknownOffset { get; init; }
    public required int EofPaddingSize { get; init; }

    public IEnumerable<int> AllNonZeroOffsets =>
        SoundOffsets.Concat( TrackOffsets ).Concat( AudioOffsets ).Concat( LayoutOffsets ).Concat( AttributeOffsets ).Where( offset => offset > 0 );

    public static ScdOffsetTable Read( BinaryReader reader ) {
        var soundCount = reader.ReadInt16();
        var trackCount = reader.ReadInt16();
        var audioCount = reader.ReadInt16();
        var unknownOffset = reader.ReadInt16();
        reader.ReadInt32();
        reader.ReadInt32();
        var layoutOffset = reader.ReadInt32();
        reader.ReadInt32();
        var attributeOffset = reader.ReadInt32();
        var eofPaddingSize = reader.ReadInt32();

        var firstOffset = int.MaxValue;

        var soundOffsets = ReadOffsets( reader, soundCount, ref firstOffset );
        var trackOffsets = ReadOffsets( reader, trackCount, ref firstOffset );
        var audioOffsets = ReadOffsets( reader, audioCount, ref firstOffset );
        var layoutOffsets = layoutOffset != 0 ? ReadOffsets( reader, soundCount, ref firstOffset ) : [];

        var attributeOffsets = new List<int>();
        if( attributeOffset != 0 && firstOffset != int.MaxValue ) {
            var attributeCount = ( firstOffset - attributeOffset ) / 4;
            attributeOffsets = ReadOffsets( reader, attributeCount, ref firstOffset );
        }

        return new ScdOffsetTable {
            SoundOffsets = soundOffsets,
            TrackOffsets = trackOffsets,
            AudioOffsets = audioOffsets,
            LayoutOffsets = layoutOffsets,
            AttributeOffsets = attributeOffsets,
            UnknownOffset = unknownOffset,
            EofPaddingSize = eofPaddingSize
        };
    }

    private static List<int> ReadOffsets( BinaryReader reader, int count, ref int firstOffset ) {
        var result = new List<int>( count );
        for( var index = 0; index < count; index++ ) {
            var offset = reader.ReadInt32();
            if( offset > 0 && offset < firstOffset ) {
                firstOffset = offset;
            }
            result.Add( offset );
        }

        ScdBinaryHelpers.PadReaderTo( reader, 16 );
        return result;
    }
}

internal static class ScdBinaryHelpers {
    public static readonly byte[] OggPagePattern = [0x4F, 0x67, 0x67, 0x53, 0x00];
    public static readonly int[] XorTable = [
        0x003A, 0x0032, 0x0032, 0x0032, 0x0003, 0x007E, 0x0012, 0x00F7, 0x00B2, 0x00E2, 0x00A2, 0x0067, 0x0032, 0x0032, 0x0022, 0x0032,
        0x0032, 0x0052, 0x0016, 0x001B, 0x003C, 0x00A1, 0x0054, 0x007B, 0x001B, 0x0097, 0x00A6, 0x0093, 0x001A, 0x004B, 0x00AA, 0x00A6,
        0x007A, 0x007B, 0x001B, 0x0097, 0x00A6, 0x00F7, 0x0002, 0x00BB, 0x00AA, 0x00A6, 0x00BB, 0x00F7, 0x002A, 0x0051, 0x00BE, 0x0003,
        0x00F4, 0x002A, 0x0051, 0x00BE, 0x0003, 0x00F4, 0x002A, 0x0051, 0x00BE, 0x0012, 0x0006, 0x0056, 0x0027, 0x0032, 0x0032, 0x0036,
        0x0032, 0x00B2, 0x001A, 0x003B, 0x00BC, 0x0091, 0x00D4, 0x007B, 0x0058, 0x00FC, 0x000B, 0x0055, 0x002A, 0x0015, 0x00BC, 0x0040,
        0x0092, 0x000B, 0x005B, 0x007C, 0x000A, 0x0095, 0x0012, 0x0035, 0x00B8, 0x0063, 0x00D2, 0x000B, 0x003B, 0x00F0, 0x00C7, 0x0014,
        0x0051, 0x005C, 0x0094, 0x0086, 0x0094, 0x0059, 0x005C, 0x00FC, 0x001B, 0x0017, 0x003A, 0x003F, 0x006B, 0x0037, 0x0032, 0x0032,
        0x0030, 0x0032, 0x0072, 0x007A, 0x0013, 0x00B7, 0x0026, 0x0060, 0x007A, 0x0013, 0x00B7, 0x0026, 0x0050, 0x00BA, 0x0013, 0x00B4,
        0x002A, 0x0050, 0x00BA, 0x0013, 0x00B5, 0x002E, 0x0040, 0x00FA, 0x0013, 0x0095, 0x00AE, 0x0040, 0x0038, 0x0018, 0x009A, 0x0092,
        0x00B0, 0x0038, 0x0000, 0x00FA, 0x0012, 0x00B1, 0x007E, 0x0000, 0x00DB, 0x0096, 0x00A1, 0x007C, 0x0008, 0x00DB, 0x00AA, 0x007C,
        0x0008, 0x00E6, 0x0026, 0x0056, 0x0089, 0x00E8, 0x007E, 0x009E, 0x00F5, 0x007C, 0x000E, 0x0095, 0x001A, 0x0090, 0x00B5, 0x005C,
        0x0091, 0x00D4, 0x003B, 0x00BC, 0x0091, 0x00D4, 0x003B, 0x00BC, 0x0090, 0x00D8, 0x003F, 0x00BC, 0x0094, 0x00D4, 0x003F, 0x00BC,
        0x00E3, 0x002A, 0x0056, 0x0055, 0x00BF, 0x00E2, 0x007E, 0x0094, 0x00F1, 0x0080, 0x0016, 0x009A, 0x0018, 0x00E8, 0x0086, 0x00F7,
        0x005E, 0x005C, 0x009B, 0x0057, 0x0095, 0x0086, 0x00FB, 0x0012, 0x001A, 0x00B3, 0x00B2, 0x00E7, 0x003E, 0x00A8, 0x00BA, 0x0032,
        0x00B6, 0x0063, 0x002A, 0x0056, 0x0055, 0x00BF, 0x00E2, 0x007E, 0x0094, 0x00F1, 0x0080, 0x0016, 0x009A, 0x0018, 0x00E8, 0x0086,
        0x00F7, 0x005E, 0x005C, 0x009B, 0x0057, 0x0095, 0x0086, 0x00FB, 0x0012, 0x001A, 0x00B3, 0x00B2, 0x00E7, 0x003E, 0x00A8, 0x00BA
    ];

    public static string ReadFixedString( BinaryReader reader, int size ) {
        return Encoding.ASCII.GetString( reader.ReadBytes( size ) ).TrimEnd( '\0' );
    }

    public static void WriteFixedString( BinaryWriter writer, string value, int size ) {
        var bytes = Encoding.ASCII.GetBytes( value );
        writer.Write( bytes.Take( size ).ToArray() );
        for( var index = Math.Min( bytes.Length, size ); index < size; index++ ) {
            writer.Write( ( byte )0 );
        }
    }

    public static void PadWriterTo( BinaryWriter writer, int multiple ) {
        var bytesToPad = NumberToPad( writer.BaseStream.Position, multiple );
        for( var index = 0; index < bytesToPad; index++ ) {
            writer.Write( ( byte )0 );
        }
    }

    public static void PadReaderTo( BinaryReader reader, int multiple ) {
        var bytesToPad = NumberToPad( reader.BaseStream.Position, multiple );
        if( bytesToPad > 0 ) {
            reader.ReadBytes( ( int )bytesToPad );
        }
    }

    public static long NumberToPad( long position, long multiple ) {
        return position % multiple == 0 ? 0 : multiple - ( position % multiple );
    }

    public static void XorDecode( byte[] bytes, byte encodeByte ) {
        for( var index = 0; index < bytes.Length; index++ ) {
            bytes[index] ^= encodeByte;
        }
    }

    public static void XorDecodeFromTable( byte[] bytes, int dataLength ) {
        var byte1 = dataLength & 0xFF & 0x7F;
        var byte2 = byte1 & 0x3F;
        for( var index = 0; index < bytes.Length; index++ ) {
            var xorByte = XorTable[( byte2 + index ) & 0xFF];
            xorByte &= 0xFF;
            xorByte ^= bytes[index] & 0xFF;
            xorByte ^= byte1;
            bytes[index] = ( byte )xorByte;
        }
    }
}

internal enum SscfWaveFormat : int {
    Empty = -1,
    Pcm = 0x01,
    Atrac3 = 0x05,
    Vorbis = 0x06,
    Xma = 0x0B,
    MsAdPcm = 0x0C,
    Atrac3Too = 0x0D
}
