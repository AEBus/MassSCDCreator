using System.Buffers.Binary;
using System.IO;

namespace MassSCDCreator.Services.Scd;

internal sealed class ScdSoundEntryModel {
    private const int RoutingFlag = 0x0100;
    private const int BusDuckingFlag = 0x0400;
    private const int AccelerationFlag = 0x0800;
    private const int ExtraDescFlag = 0x2000;
    private const int AtomosgearFlag = 0x8000;
    private const int BypassFlag = 0x0040;
    private const int LoopFlag = 0x0001;
    private const byte EmptyType = 15;
    private const byte RandomType = 2;
    private const byte CycleType = 4;
    private const byte GroupRandomType = 11;
    private const byte GroupOrderType = 12;

    public byte TrackCount { get; set; }
    public byte BusNumber { get; set; }
    public byte Priority { get; set; }
    public byte Type { get; set; }
    public int Attributes { get; set; }
    public float Volume { get; set; }
    public short LocalNumber { get; set; }
    public byte UserId { get; set; }
    public sbyte PlayHistory { get; set; }

    public byte[]? RoutingBlock { get; set; }
    public byte[]? BusDuckingBlock { get; set; }
    public byte[]? AccelerationBlock { get; set; }
    public byte[]? AtomosBlock { get; set; }
    public byte[]? ExtraBlock { get; set; }
    public byte[]? BypassBlock { get; set; }
    public byte[]? EmptyLoopBlock { get; set; }
    public List<ScdSoundTrackInfoModel> Tracks { get; } = [];
    public List<ScdSoundRandomTrackInfoModel> RandomTracks { get; } = [];
    public int? CycleInterval { get; set; }
    public short? CycleNumPlayTrack { get; set; }
    public short? CycleRange { get; set; }
    public byte[] TailPayload { get; set; } = [];

    public static ScdSoundEntryModel Read( byte[] rawEntry ) {
        using var stream = new MemoryStream( rawEntry, writable: false );
        using var reader = new BinaryReader( stream );

        var entry = new ScdSoundEntryModel {
            TrackCount = reader.ReadByte(),
            BusNumber = reader.ReadByte(),
            Priority = reader.ReadByte(),
            Type = reader.ReadByte(),
            Attributes = reader.ReadInt32(),
            Volume = reader.ReadSingle(),
            LocalNumber = reader.ReadInt16(),
            UserId = reader.ReadByte(),
            PlayHistory = reader.ReadSByte()
        };

        if( entry.HasRouting ) {
            entry.RoutingBlock = ReadRoutingBlock( reader );
        }

        if( entry.HasBusDucking ) {
            entry.BusDuckingBlock = reader.ReadBytes( 0x10 );
        }

        if( entry.HasAcceleration ) {
            entry.AccelerationBlock = reader.ReadBytes( 0x50 );
        }

        if( entry.HasAtomos ) {
            entry.AtomosBlock = reader.ReadBytes( 0x10 );
        }

        if( entry.HasExtra ) {
            entry.ExtraBlock = reader.ReadBytes( 0x10 );
        }

        if( entry.HasBypass ) {
            entry.BypassBlock = reader.ReadBytes( 0x20 );
        }

        if( entry.HasEmptyLoop ) {
            entry.EmptyLoopBlock = reader.ReadBytes( 0x08 );
        }

        if( entry.HasRandomTracks ) {
            for( var index = 0; index < entry.TrackCount; index++ ) {
                entry.RandomTracks.Add( ScdSoundRandomTrackInfoModel.Read( reader ) );
            }

            if( entry.IsCycleType && stream.Position + 8 <= stream.Length ) {
                entry.CycleInterval = reader.ReadInt32();
                entry.CycleNumPlayTrack = reader.ReadInt16();
                entry.CycleRange = reader.ReadInt16();
            }
        }
        else {
            for( var index = 0; index < entry.TrackCount; index++ ) {
                entry.Tracks.Add( ScdSoundTrackInfoModel.Read( reader ) );
            }
        }

        entry.TailPayload = reader.ReadBytes( ( int )( stream.Length - stream.Position ) );
        return entry;
    }

    public ScdSoundEntryModel Clone() {
        var clone = new ScdSoundEntryModel {
            TrackCount = TrackCount,
            BusNumber = BusNumber,
            Priority = Priority,
            Type = Type,
            Attributes = Attributes,
            Volume = Volume,
            LocalNumber = LocalNumber,
            UserId = UserId,
            PlayHistory = PlayHistory,
            RoutingBlock = RoutingBlock?.ToArray(),
            BusDuckingBlock = BusDuckingBlock?.ToArray(),
            AccelerationBlock = AccelerationBlock?.ToArray(),
            AtomosBlock = AtomosBlock?.ToArray(),
            ExtraBlock = ExtraBlock?.ToArray(),
            BypassBlock = BypassBlock?.ToArray(),
            EmptyLoopBlock = EmptyLoopBlock?.ToArray(),
            CycleInterval = CycleInterval,
            CycleNumPlayTrack = CycleNumPlayTrack,
            CycleRange = CycleRange,
            TailPayload = TailPayload.ToArray()
        };

        clone.Tracks.AddRange( Tracks.Select( entry => entry.Clone() ) );
        clone.RandomTracks.AddRange( RandomTracks.Select( entry => entry.Clone() ) );
        return clone;
    }

    public void UpdatePlayTimeLength( TimeSpan duration ) {
        if( !HasExtra || ExtraBlock is null || ExtraBlock.Length < 8 ) {
            return;
        }

        var playTimeLength = checked( ( int )Math.Round( duration.TotalMilliseconds ) );
        BinaryPrimitives.WriteInt32LittleEndian( ExtraBlock.AsSpan( 4, 4 ), playTimeLength );
    }

    public void Write( BinaryWriter writer ) {
        writer.Write( GetEffectiveTrackCount() );
        writer.Write( BusNumber );
        writer.Write( Priority );
        writer.Write( Type );
        writer.Write( Attributes );
        writer.Write( Volume );
        writer.Write( LocalNumber );
        writer.Write( UserId );
        writer.Write( PlayHistory );

        if( RoutingBlock is not null ) {
            writer.Write( RoutingBlock );
        }

        if( BusDuckingBlock is not null ) {
            writer.Write( BusDuckingBlock );
        }

        if( AccelerationBlock is not null ) {
            writer.Write( AccelerationBlock );
        }

        if( AtomosBlock is not null ) {
            writer.Write( AtomosBlock );
        }

        if( ExtraBlock is not null ) {
            writer.Write( ExtraBlock );
        }

        if( BypassBlock is not null ) {
            writer.Write( BypassBlock );
        }

        if( EmptyLoopBlock is not null ) {
            writer.Write( EmptyLoopBlock );
        }

        if( HasRandomTracks ) {
            foreach( var track in RandomTracks ) {
                track.Write( writer );
            }

            if( IsCycleType && CycleInterval.HasValue && CycleNumPlayTrack.HasValue && CycleRange.HasValue ) {
                writer.Write( CycleInterval.Value );
                writer.Write( CycleNumPlayTrack.Value );
                writer.Write( CycleRange.Value );
            }
        }
        else {
            foreach( var track in Tracks ) {
                track.Write( writer );
            }
        }

        writer.Write( TailPayload );
    }

    private bool HasRouting => ( Attributes & RoutingFlag ) != 0;
    private bool HasBusDucking => ( Attributes & BusDuckingFlag ) != 0;
    private bool HasAcceleration => ( Attributes & AccelerationFlag ) != 0;
    private bool HasAtomos => ( Attributes & AtomosgearFlag ) != 0;
    private bool HasExtra => ( Attributes & ExtraDescFlag ) != 0;
    private bool HasBypass => ( Attributes & BypassFlag ) != 0;
    private bool HasEmptyLoop => Type == EmptyType && ( Attributes & LoopFlag ) != 0;
    private bool HasRandomTracks => Type is RandomType or CycleType or GroupRandomType or GroupOrderType;
    private bool IsCycleType => Type == CycleType;

    private byte GetEffectiveTrackCount() {
        if( HasRandomTracks ) {
            return RandomTracks.Count > 0 ? checked( ( byte )RandomTracks.Count ) : TrackCount;
        }

        return Tracks.Count > 0 ? checked( ( byte )Tracks.Count ) : TrackCount;
    }

    private static byte[] ReadRoutingBlock( BinaryReader reader ) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );

        var dataSize = reader.ReadUInt32();
        var sendCount = reader.ReadByte();
        writer.Write( dataSize );
        writer.Write( sendCount );

        var reserved = reader.ReadBytes( 11 );
        writer.Write( reserved );

        for( var index = 0; index < sendCount; index++ ) {
            writer.Write( reader.ReadBytes( 16 ) );
        }

        writer.Write( reader.ReadBytes( 0x90 ) );
        return stream.ToArray();
    }
}

internal sealed class ScdSoundTrackInfoModel {
    public short TrackIndex { get; set; }
    public short AudioIndex { get; set; }

    public static ScdSoundTrackInfoModel Read( BinaryReader reader ) => new() {
        TrackIndex = reader.ReadInt16(),
        AudioIndex = reader.ReadInt16()
    };

    public ScdSoundTrackInfoModel Clone() => new() {
        TrackIndex = TrackIndex,
        AudioIndex = AudioIndex
    };

    public void Write( BinaryWriter writer ) {
        writer.Write( TrackIndex );
        writer.Write( AudioIndex );
    }
}

internal sealed class ScdSoundRandomTrackInfoModel {
    public ScdSoundTrackInfoModel Track { get; set; } = new();
    public short LimitLower { get; set; }
    public short LimitUpper { get; set; }

    public static ScdSoundRandomTrackInfoModel Read( BinaryReader reader ) => new() {
        Track = ScdSoundTrackInfoModel.Read( reader ),
        LimitLower = reader.ReadInt16(),
        LimitUpper = reader.ReadInt16()
    };

    public ScdSoundRandomTrackInfoModel Clone() => new() {
        Track = Track.Clone(),
        LimitLower = LimitLower,
        LimitUpper = LimitUpper
    };

    public void Write( BinaryWriter writer ) {
        Track.Write( writer );
        writer.Write( LimitLower );
        writer.Write( LimitUpper );
    }
}
