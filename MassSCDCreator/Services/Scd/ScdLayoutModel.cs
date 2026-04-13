using System.IO;
using System.Numerics;

namespace MassSCDCreator.Services.Scd;

internal enum ScdSoundObjectType : byte {
    Null = 0,
    Ambient = 1,
    Direction = 2,
    Point = 3,
    PointDir = 4,
    Line = 5,
    Polyline = 6,
    Surface = 7,
    BoardObstruction = 8,
    BoxObstruction = 9,
    PolylineObstruction = 10,
    Polygon = 11,
    BoxExtController = 12,
    LineExtController = 13,
    PolygonObstruction = 14
}

internal sealed class ScdLayoutEntryModel {
    public ushort Size { get; set; }
    public byte Type { get; set; }
    public byte Version { get; set; }
    public byte Flag1 { get; set; }
    public byte GroupNumber { get; set; }
    public short LocalId { get; set; }
    public int BankId { get; set; }
    public byte Flag2 { get; set; }
    public byte ReverbType { get; set; }
    public short AbGroupNumber { get; set; }
    public Vector4 Volume { get; set; }
    public ScdLayoutDataModel Data { get; set; } = new ScdLayoutRawDataModel();

    public static ScdLayoutEntryModel Read( byte[] rawEntry ) {
        using var stream = new MemoryStream( rawEntry, writable: false );
        using var reader = new BinaryReader( stream );

        var entry = new ScdLayoutEntryModel {
            Size = reader.ReadUInt16(),
            Type = reader.ReadByte(),
            Version = reader.ReadByte(),
            Flag1 = reader.ReadByte(),
            GroupNumber = reader.ReadByte(),
            LocalId = reader.ReadInt16(),
            BankId = reader.ReadInt32(),
            Flag2 = reader.ReadByte(),
            ReverbType = reader.ReadByte(),
            AbGroupNumber = reader.ReadInt16(),
            Volume = ScdLayoutBinary.ReadVector4( reader )
        };

        var payload = reader.ReadBytes( ( int )( stream.Length - stream.Position ) );
        entry.Data = ScdLayoutDataFactory.Read( entry.Type, payload );
        return entry;
    }

    public ScdLayoutEntryModel Clone() => new() {
        Size = Size,
        Type = Type,
        Version = Version,
        Flag1 = Flag1,
        GroupNumber = GroupNumber,
        LocalId = LocalId,
        BankId = BankId,
        Flag2 = Flag2,
        ReverbType = ReverbType,
        AbGroupNumber = AbGroupNumber,
        Volume = Volume,
        Data = Data.Clone()
    };

    public void Write( BinaryWriter writer ) {
        using var stream = new MemoryStream();
        using var entryWriter = new BinaryWriter( stream );

        entryWriter.Write( Size );
        entryWriter.Write( Type );
        entryWriter.Write( Version );
        entryWriter.Write( Flag1 );
        entryWriter.Write( GroupNumber );
        entryWriter.Write( LocalId );
        entryWriter.Write( BankId );
        entryWriter.Write( Flag2 );
        entryWriter.Write( ReverbType );
        entryWriter.Write( AbGroupNumber );
        ScdLayoutBinary.Write( entryWriter, Volume );
        Data.Write( entryWriter );
        entryWriter.Flush();

        var entryBytes = stream.ToArray();
        var expectedLength = Math.Max( Size, ( ushort )entryBytes.Length );
        if( Size > 0 ) {
            expectedLength = Size;
        }

        writer.Write( ScdLayoutBinary.Resize( entryBytes, expectedLength ) );
    }
}

internal abstract class ScdLayoutDataModel {
    public byte[] TailPayload { get; set; } = [];
    public abstract void ReadCore( BinaryReader reader, int length );
    public abstract void WriteCore( BinaryWriter writer );
    public abstract ScdLayoutDataModel Clone();

    public void Read( byte[] payload ) {
        using var stream = new MemoryStream( payload, writable: false );
        using var reader = new BinaryReader( stream );
        ReadCore( reader, payload.Length );
        TailPayload = reader.ReadBytes( ( int )( stream.Length - stream.Position ) );
    }

    public void Write( BinaryWriter writer ) {
        WriteCore( writer );
        writer.Write( TailPayload );
    }
}

internal sealed class ScdLayoutRawDataModel : ScdLayoutDataModel {
    public byte[] Payload { get; set; } = [];

    public override void ReadCore( BinaryReader reader, int length ) {
        Payload = reader.ReadBytes( length );
        TailPayload = [];
    }

    public override void WriteCore( BinaryWriter writer ) {
        writer.Write( Payload );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutRawDataModel {
        Payload = Payload.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal static class ScdLayoutBinary {
    public static Vector2 ReadVector2( BinaryReader reader ) => new( reader.ReadSingle(), reader.ReadSingle() );
    public static Vector4 ReadVector4( BinaryReader reader ) => new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );

    public static void Write( BinaryWriter writer, Vector2 value ) {
        writer.Write( value.X );
        writer.Write( value.Y );
    }

    public static void Write( BinaryWriter writer, Vector4 value ) {
        writer.Write( value.X );
        writer.Write( value.Y );
        writer.Write( value.Z );
        writer.Write( value.W );
    }

    public static byte[] Resize( byte[] bytes, int expectedLength ) {
        if( bytes.Length == expectedLength ) {
            return bytes;
        }

        var resized = new byte[expectedLength];
        Array.Copy( bytes, resized, Math.Min( bytes.Length, expectedLength ) );
        return resized;
    }
}

internal sealed class ScdLayoutAmbientDataModel : ScdLayoutDataModel {
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public Vector4 DirectVolume1 { get; set; }
    public Vector4 DirectVolume2 { get; set; }
    public byte[] Reserved { get; set; } = new byte[4];

    public override void ReadCore( BinaryReader reader, int length ) {
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DirectVolume1 = ScdLayoutBinary.ReadVector4( reader );
        DirectVolume2 = ScdLayoutBinary.ReadVector4( reader );
        Reserved = reader.ReadBytes( 4 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        ScdLayoutBinary.Write( writer, DirectVolume1 );
        ScdLayoutBinary.Write( writer, DirectVolume2 );
        writer.Write( ScdLayoutBinary.Resize( Reserved, 4 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutAmbientDataModel {
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        DirectVolume1 = DirectVolume1,
        DirectVolume2 = DirectVolume2,
        Reserved = Reserved.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutDirectionDataModel : ScdLayoutDataModel {
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float Direction { get; set; }
    public float RotSpeed { get; set; }
    public byte[] Reserved { get; set; } = new byte[12];

    public override void ReadCore( BinaryReader reader, int length ) {
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
        RotSpeed = reader.ReadSingle();
        Reserved = reader.ReadBytes( 12 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( Direction );
        writer.Write( RotSpeed );
        writer.Write( ScdLayoutBinary.Resize( Reserved, 12 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutDirectionDataModel {
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        Direction = Direction,
        RotSpeed = RotSpeed,
        Reserved = Reserved.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutPointDataModel : ScdLayoutDataModel {
    public Vector4 Position { get; set; }
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public float CenterFac { get; set; }
    public float InteriorFac { get; set; }
    public float Direction { get; set; }
    public float NearFadeStart { get; set; }
    public float NearFadeEnd { get; set; }
    public float FarDelayFac { get; set; }
    public byte Environment { get; set; }
    public byte Flag { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[2];
    public float LowerLimit { get; set; }
    public short FadeInTime { get; set; }
    public short FadeOutTime { get; set; }
    public float ConvergenceFac { get; set; }
    public byte[] Reserved2 { get; set; } = new byte[4];

    public override void ReadCore( BinaryReader reader, int length ) {
        Position = ScdLayoutBinary.ReadVector4( reader );
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        CenterFac = reader.ReadSingle();
        InteriorFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
        NearFadeStart = reader.ReadSingle();
        NearFadeEnd = reader.ReadSingle();
        FarDelayFac = reader.ReadSingle();
        Environment = reader.ReadByte();
        Flag = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 2 );
        LowerLimit = reader.ReadSingle();
        FadeInTime = reader.ReadInt16();
        FadeOutTime = reader.ReadInt16();
        ConvergenceFac = reader.ReadSingle();
        Reserved2 = reader.ReadBytes( 4 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, Position );
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( CenterFac );
        writer.Write( InteriorFac );
        writer.Write( Direction );
        writer.Write( NearFadeStart );
        writer.Write( NearFadeEnd );
        writer.Write( FarDelayFac );
        writer.Write( Environment );
        writer.Write( Flag );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 2 ) );
        writer.Write( LowerLimit );
        writer.Write( FadeInTime );
        writer.Write( FadeOutTime );
        writer.Write( ConvergenceFac );
        writer.Write( ScdLayoutBinary.Resize( Reserved2, 4 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutPointDataModel {
        Position = Position,
        MaxRange = MaxRange,
        MinRange = MinRange,
        Height = Height,
        RangeVolume = RangeVolume,
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        DopplerFac = DopplerFac,
        CenterFac = CenterFac,
        InteriorFac = InteriorFac,
        Direction = Direction,
        NearFadeStart = NearFadeStart,
        NearFadeEnd = NearFadeEnd,
        FarDelayFac = FarDelayFac,
        Environment = Environment,
        Flag = Flag,
        Reserved1 = Reserved1.ToArray(),
        LowerLimit = LowerLimit,
        FadeInTime = FadeInTime,
        FadeOutTime = FadeOutTime,
        ConvergenceFac = ConvergenceFac,
        Reserved2 = Reserved2.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutPointDirDataModel : ScdLayoutDataModel {
    public Vector4 Position { get; set; }
    public Vector4 DirectionVector { get; set; }
    public float RangeX { get; set; }
    public float RangeY { get; set; }
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public float InteriorFac { get; set; }
    public float FixedDirection { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[12];

    public override void ReadCore( BinaryReader reader, int length ) {
        Position = ScdLayoutBinary.ReadVector4( reader );
        DirectionVector = ScdLayoutBinary.ReadVector4( reader );
        RangeX = reader.ReadSingle();
        RangeY = reader.ReadSingle();
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        InteriorFac = reader.ReadSingle();
        FixedDirection = reader.ReadSingle();
        Reserved1 = reader.ReadBytes( 12 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, Position );
        ScdLayoutBinary.Write( writer, DirectionVector );
        writer.Write( RangeX );
        writer.Write( RangeY );
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( InteriorFac );
        writer.Write( FixedDirection );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 12 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutPointDirDataModel {
        Position = Position,
        DirectionVector = DirectionVector,
        RangeX = RangeX,
        RangeY = RangeY,
        MaxRange = MaxRange,
        MinRange = MinRange,
        Height = Height,
        RangeVolume = RangeVolume,
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        DopplerFac = DopplerFac,
        InteriorFac = InteriorFac,
        FixedDirection = FixedDirection,
        Reserved1 = Reserved1.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutLineDataModel : ScdLayoutDataModel {
    public Vector4 StartPosition { get; set; }
    public Vector4 EndPosition { get; set; }
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public float InteriorFac { get; set; }
    public float Direction { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[4];

    public override void ReadCore( BinaryReader reader, int length ) {
        StartPosition = ScdLayoutBinary.ReadVector4( reader );
        EndPosition = ScdLayoutBinary.ReadVector4( reader );
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        InteriorFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
        Reserved1 = reader.ReadBytes( 4 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, StartPosition );
        ScdLayoutBinary.Write( writer, EndPosition );
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( InteriorFac );
        writer.Write( Direction );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 4 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutLineDataModel {
        StartPosition = StartPosition,
        EndPosition = EndPosition,
        MaxRange = MaxRange,
        MinRange = MinRange,
        Height = Height,
        RangeVolume = RangeVolume,
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        DopplerFac = DopplerFac,
        InteriorFac = InteriorFac,
        Direction = Direction,
        Reserved1 = Reserved1.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutPolylineDataModel : ScdLayoutDataModel {
    public List<Vector4> Positions { get; } = [];
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public byte VertexCount { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[3];
    public float InteriorFac { get; set; }
    public float Direction { get; set; }

    public override void ReadCore( BinaryReader reader, int length ) {
        Positions.Clear();
        for( var i = 0; i < 16; i++ ) {
            Positions.Add( ScdLayoutBinary.ReadVector4( reader ) );
        }

        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        VertexCount = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 3 );
        InteriorFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
    }

    public override void WriteCore( BinaryWriter writer ) {
        for( var i = 0; i < 16; i++ ) {
            ScdLayoutBinary.Write( writer, i < Positions.Count ? Positions[i] : Vector4.Zero );
        }

        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( VertexCount );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 3 ) );
        writer.Write( InteriorFac );
        writer.Write( Direction );
    }

    public override ScdLayoutDataModel Clone() {
        var clone = new ScdLayoutPolylineDataModel {
            MaxRange = MaxRange,
            MinRange = MinRange,
            Height = Height,
            RangeVolume = RangeVolume,
            VolumeValue = VolumeValue,
            Pitch = Pitch,
            ReverbFac = ReverbFac,
            DopplerFac = DopplerFac,
            VertexCount = VertexCount,
            Reserved1 = Reserved1.ToArray(),
            InteriorFac = InteriorFac,
            Direction = Direction,
            TailPayload = TailPayload.ToArray()
        };
        clone.Positions.AddRange( Positions );
        return clone;
    }
}

internal sealed class ScdLayoutSurfaceDataModel : ScdLayoutDataModel {
    public Vector4 Position1 { get; set; }
    public Vector4 Position2 { get; set; }
    public Vector4 Position3 { get; set; }
    public Vector4 Position4 { get; set; }
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public float InteriorFac { get; set; }
    public float Direction { get; set; }
    public byte SubSoundType { get; set; }
    public byte Flags { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[2];
    public float RotSpeed { get; set; }
    public byte[] Reserved2 { get; set; } = new byte[12];

    public override void ReadCore( BinaryReader reader, int length ) {
        Position1 = ScdLayoutBinary.ReadVector4( reader );
        Position2 = ScdLayoutBinary.ReadVector4( reader );
        Position3 = ScdLayoutBinary.ReadVector4( reader );
        Position4 = ScdLayoutBinary.ReadVector4( reader );
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        InteriorFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
        SubSoundType = reader.ReadByte();
        Flags = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 2 );
        RotSpeed = reader.ReadSingle();
        Reserved2 = reader.ReadBytes( 12 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, Position1 );
        ScdLayoutBinary.Write( writer, Position2 );
        ScdLayoutBinary.Write( writer, Position3 );
        ScdLayoutBinary.Write( writer, Position4 );
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( InteriorFac );
        writer.Write( Direction );
        writer.Write( SubSoundType );
        writer.Write( Flags );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 2 ) );
        writer.Write( RotSpeed );
        writer.Write( ScdLayoutBinary.Resize( Reserved2, 12 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutSurfaceDataModel {
        Position1 = Position1,
        Position2 = Position2,
        Position3 = Position3,
        Position4 = Position4,
        MaxRange = MaxRange,
        MinRange = MinRange,
        Height = Height,
        RangeVolume = RangeVolume,
        VolumeValue = VolumeValue,
        Pitch = Pitch,
        ReverbFac = ReverbFac,
        DopplerFac = DopplerFac,
        InteriorFac = InteriorFac,
        Direction = Direction,
        SubSoundType = SubSoundType,
        Flags = Flags,
        Reserved1 = Reserved1.ToArray(),
        RotSpeed = RotSpeed,
        Reserved2 = Reserved2.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutPolygonDataModel : ScdLayoutDataModel {
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float Pitch { get; set; }
    public float ReverbFac { get; set; }
    public float DopplerFac { get; set; }
    public float InteriorFac { get; set; }
    public float Direction { get; set; }
    public byte SubSoundType { get; set; }
    public byte Flag { get; set; }
    public byte VertexCount { get; set; }
    public byte Reserved1 { get; set; }
    public float RotSpeed { get; set; }
    public byte[] Reserved2 { get; set; } = new byte[12];
    public List<Vector4> Positions { get; } = [];

    public override void ReadCore( BinaryReader reader, int length ) {
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        Pitch = reader.ReadSingle();
        ReverbFac = reader.ReadSingle();
        DopplerFac = reader.ReadSingle();
        InteriorFac = reader.ReadSingle();
        Direction = reader.ReadSingle();
        SubSoundType = reader.ReadByte();
        Flag = reader.ReadByte();
        VertexCount = reader.ReadByte();
        Reserved1 = reader.ReadByte();
        RotSpeed = reader.ReadSingle();
        Reserved2 = reader.ReadBytes( 12 );
        Positions.Clear();
        for( var i = 0; i < 32; i++ ) {
            Positions.Add( ScdLayoutBinary.ReadVector4( reader ) );
        }
    }

    public override void WriteCore( BinaryWriter writer ) {
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( Pitch );
        writer.Write( ReverbFac );
        writer.Write( DopplerFac );
        writer.Write( InteriorFac );
        writer.Write( Direction );
        writer.Write( SubSoundType );
        writer.Write( Flag );
        writer.Write( VertexCount );
        writer.Write( Reserved1 );
        writer.Write( RotSpeed );
        writer.Write( ScdLayoutBinary.Resize( Reserved2, 12 ) );
        for( var i = 0; i < 32; i++ ) {
            ScdLayoutBinary.Write( writer, i < Positions.Count ? Positions[i] : Vector4.Zero );
        }
    }

    public override ScdLayoutDataModel Clone() {
        var clone = new ScdLayoutPolygonDataModel {
            MaxRange = MaxRange,
            MinRange = MinRange,
            Height = Height,
            RangeVolume = RangeVolume,
            VolumeValue = VolumeValue,
            Pitch = Pitch,
            ReverbFac = ReverbFac,
            DopplerFac = DopplerFac,
            InteriorFac = InteriorFac,
            Direction = Direction,
            SubSoundType = SubSoundType,
            Flag = Flag,
            VertexCount = VertexCount,
            Reserved1 = Reserved1,
            RotSpeed = RotSpeed,
            Reserved2 = Reserved2.ToArray(),
            TailPayload = TailPayload.ToArray()
        };
        clone.Positions.AddRange( Positions );
        return clone;
    }
}

internal sealed class ScdLayoutBoardObstructionDataModel : ScdLayoutDataModel {
    public Vector4 Position1 { get; set; }
    public Vector4 Position2 { get; set; }
    public Vector4 Position3 { get; set; }
    public Vector4 Position4 { get; set; }
    public float ObstacleFac { get; set; }
    public float HiCutFac { get; set; }
    public byte Flags { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[3];
    public short OpenTime { get; set; }
    public short CloseTime { get; set; }

    public override void ReadCore( BinaryReader reader, int length ) {
        Position1 = ScdLayoutBinary.ReadVector4( reader );
        Position2 = ScdLayoutBinary.ReadVector4( reader );
        Position3 = ScdLayoutBinary.ReadVector4( reader );
        Position4 = ScdLayoutBinary.ReadVector4( reader );
        ObstacleFac = reader.ReadSingle();
        HiCutFac = reader.ReadSingle();
        Flags = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 3 );
        OpenTime = reader.ReadInt16();
        CloseTime = reader.ReadInt16();
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, Position1 );
        ScdLayoutBinary.Write( writer, Position2 );
        ScdLayoutBinary.Write( writer, Position3 );
        ScdLayoutBinary.Write( writer, Position4 );
        writer.Write( ObstacleFac );
        writer.Write( HiCutFac );
        writer.Write( Flags );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 3 ) );
        writer.Write( OpenTime );
        writer.Write( CloseTime );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutBoardObstructionDataModel {
        Position1 = Position1,
        Position2 = Position2,
        Position3 = Position3,
        Position4 = Position4,
        ObstacleFac = ObstacleFac,
        HiCutFac = HiCutFac,
        Flags = Flags,
        Reserved1 = Reserved1.ToArray(),
        OpenTime = OpenTime,
        CloseTime = CloseTime,
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutBoxObstructionDataModel : ScdLayoutDataModel {
    public Vector4 Position1 { get; set; }
    public Vector4 Position2 { get; set; }
    public Vector4 Position3 { get; set; }
    public Vector4 Position4 { get; set; }
    public Vector2 Height { get; set; }
    public float ObstacleFac { get; set; }
    public float HiCutFac { get; set; }
    public byte Flags { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[3];
    public float FadeRange { get; set; }
    public short OpenTime { get; set; }
    public short CloseTime { get; set; }
    public byte[] Reserved2 { get; set; } = new byte[4];

    public override void ReadCore( BinaryReader reader, int length ) {
        Position1 = ScdLayoutBinary.ReadVector4( reader );
        Position2 = ScdLayoutBinary.ReadVector4( reader );
        Position3 = ScdLayoutBinary.ReadVector4( reader );
        Position4 = ScdLayoutBinary.ReadVector4( reader );
        Height = ScdLayoutBinary.ReadVector2( reader );
        ObstacleFac = reader.ReadSingle();
        HiCutFac = reader.ReadSingle();
        Flags = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 3 );
        FadeRange = reader.ReadSingle();
        OpenTime = reader.ReadInt16();
        CloseTime = reader.ReadInt16();
        Reserved2 = reader.ReadBytes( 4 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, Position1 );
        ScdLayoutBinary.Write( writer, Position2 );
        ScdLayoutBinary.Write( writer, Position3 );
        ScdLayoutBinary.Write( writer, Position4 );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( ObstacleFac );
        writer.Write( HiCutFac );
        writer.Write( Flags );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 3 ) );
        writer.Write( FadeRange );
        writer.Write( OpenTime );
        writer.Write( CloseTime );
        writer.Write( ScdLayoutBinary.Resize( Reserved2, 4 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutBoxObstructionDataModel {
        Position1 = Position1,
        Position2 = Position2,
        Position3 = Position3,
        Position4 = Position4,
        Height = Height,
        ObstacleFac = ObstacleFac,
        HiCutFac = HiCutFac,
        Flags = Flags,
        Reserved1 = Reserved1.ToArray(),
        FadeRange = FadeRange,
        OpenTime = OpenTime,
        CloseTime = CloseTime,
        Reserved2 = Reserved2.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal sealed class ScdLayoutPolylineObstructionDataModel : ScdLayoutDataModel {
    public List<Vector4> Positions { get; } = [];
    public Vector2 Height { get; set; }
    public float ObstacleFac { get; set; }
    public float HiCutFac { get; set; }
    public byte Flags { get; set; }
    public byte VertexCount { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[2];
    public float Width { get; set; }
    public float FadeRange { get; set; }
    public short OpenTime { get; set; }
    public short CloseTime { get; set; }

    public override void ReadCore( BinaryReader reader, int length ) {
        Positions.Clear();
        for( var i = 0; i < 16; i++ ) {
            Positions.Add( ScdLayoutBinary.ReadVector4( reader ) );
        }

        Height = ScdLayoutBinary.ReadVector2( reader );
        ObstacleFac = reader.ReadSingle();
        HiCutFac = reader.ReadSingle();
        Flags = reader.ReadByte();
        VertexCount = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 2 );
        Width = reader.ReadSingle();
        FadeRange = reader.ReadSingle();
        OpenTime = reader.ReadInt16();
        CloseTime = reader.ReadInt16();
    }

    public override void WriteCore( BinaryWriter writer ) {
        for( var i = 0; i < 16; i++ ) {
            ScdLayoutBinary.Write( writer, i < Positions.Count ? Positions[i] : Vector4.Zero );
        }

        ScdLayoutBinary.Write( writer, Height );
        writer.Write( ObstacleFac );
        writer.Write( HiCutFac );
        writer.Write( Flags );
        writer.Write( VertexCount );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 2 ) );
        writer.Write( Width );
        writer.Write( FadeRange );
        writer.Write( OpenTime );
        writer.Write( CloseTime );
    }

    public override ScdLayoutDataModel Clone() {
        var clone = new ScdLayoutPolylineObstructionDataModel {
            Height = Height,
            ObstacleFac = ObstacleFac,
            HiCutFac = HiCutFac,
            Flags = Flags,
            VertexCount = VertexCount,
            Reserved1 = Reserved1.ToArray(),
            Width = Width,
            FadeRange = FadeRange,
            OpenTime = OpenTime,
            CloseTime = CloseTime,
            TailPayload = TailPayload.ToArray()
        };
        clone.Positions.AddRange( Positions );
        return clone;
    }
}

internal sealed class ScdLayoutPolygonObstructionDataModel : ScdLayoutDataModel {
    public float ObstacleFac { get; set; }
    public float HiCutFac { get; set; }
    public byte Flags { get; set; }
    public byte VertexCount { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[2];
    public short OpenTime { get; set; }
    public short CloseTime { get; set; }
    public List<Vector4> Positions { get; } = [];

    public override void ReadCore( BinaryReader reader, int length ) {
        ObstacleFac = reader.ReadSingle();
        HiCutFac = reader.ReadSingle();
        Flags = reader.ReadByte();
        VertexCount = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 2 );
        OpenTime = reader.ReadInt16();
        CloseTime = reader.ReadInt16();
        Positions.Clear();
        for( var i = 0; i < 32; i++ ) {
            Positions.Add( ScdLayoutBinary.ReadVector4( reader ) );
        }
    }

    public override void WriteCore( BinaryWriter writer ) {
        writer.Write( ObstacleFac );
        writer.Write( HiCutFac );
        writer.Write( Flags );
        writer.Write( VertexCount );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 2 ) );
        writer.Write( OpenTime );
        writer.Write( CloseTime );
        for( var i = 0; i < 32; i++ ) {
            ScdLayoutBinary.Write( writer, i < Positions.Count ? Positions[i] : Vector4.Zero );
        }
    }

    public override ScdLayoutDataModel Clone() {
        var clone = new ScdLayoutPolygonObstructionDataModel {
            ObstacleFac = ObstacleFac,
            HiCutFac = HiCutFac,
            Flags = Flags,
            VertexCount = VertexCount,
            Reserved1 = Reserved1.ToArray(),
            OpenTime = OpenTime,
            CloseTime = CloseTime,
            TailPayload = TailPayload.ToArray()
        };
        clone.Positions.AddRange( Positions );
        return clone;
    }
}

internal sealed class ScdLayoutLineExtControllerDataModel : ScdLayoutDataModel {
    public Vector4 StartPosition { get; set; }
    public Vector4 EndPosition { get; set; }
    public float MaxRange { get; set; }
    public float MinRange { get; set; }
    public Vector2 Height { get; set; }
    public float RangeVolume { get; set; }
    public float VolumeValue { get; set; }
    public float LowerLimit { get; set; }
    public int FunctionNumber { get; set; }
    public byte CalcType { get; set; }
    public byte[] Reserved1 { get; set; } = new byte[19];

    public override void ReadCore( BinaryReader reader, int length ) {
        StartPosition = ScdLayoutBinary.ReadVector4( reader );
        EndPosition = ScdLayoutBinary.ReadVector4( reader );
        MaxRange = reader.ReadSingle();
        MinRange = reader.ReadSingle();
        Height = ScdLayoutBinary.ReadVector2( reader );
        RangeVolume = reader.ReadSingle();
        VolumeValue = reader.ReadSingle();
        LowerLimit = reader.ReadSingle();
        FunctionNumber = reader.ReadInt32();
        CalcType = reader.ReadByte();
        Reserved1 = reader.ReadBytes( 19 );
    }

    public override void WriteCore( BinaryWriter writer ) {
        ScdLayoutBinary.Write( writer, StartPosition );
        ScdLayoutBinary.Write( writer, EndPosition );
        writer.Write( MaxRange );
        writer.Write( MinRange );
        ScdLayoutBinary.Write( writer, Height );
        writer.Write( RangeVolume );
        writer.Write( VolumeValue );
        writer.Write( LowerLimit );
        writer.Write( FunctionNumber );
        writer.Write( CalcType );
        writer.Write( ScdLayoutBinary.Resize( Reserved1, 19 ) );
    }

    public override ScdLayoutDataModel Clone() => new ScdLayoutLineExtControllerDataModel {
        StartPosition = StartPosition,
        EndPosition = EndPosition,
        MaxRange = MaxRange,
        MinRange = MinRange,
        Height = Height,
        RangeVolume = RangeVolume,
        VolumeValue = VolumeValue,
        LowerLimit = LowerLimit,
        FunctionNumber = FunctionNumber,
        CalcType = CalcType,
        Reserved1 = Reserved1.ToArray(),
        TailPayload = TailPayload.ToArray()
    };
}

internal static class ScdLayoutDataFactory {
    public static ScdLayoutDataModel Read( byte type, byte[] payload ) {
        ScdLayoutDataModel model = type switch {
            ( byte )ScdSoundObjectType.Ambient => new ScdLayoutAmbientDataModel(),
            ( byte )ScdSoundObjectType.Direction => new ScdLayoutDirectionDataModel(),
            ( byte )ScdSoundObjectType.Point => new ScdLayoutPointDataModel(),
            ( byte )ScdSoundObjectType.PointDir => new ScdLayoutPointDirDataModel(),
            ( byte )ScdSoundObjectType.Line => new ScdLayoutLineDataModel(),
            ( byte )ScdSoundObjectType.Polyline => new ScdLayoutPolylineDataModel(),
            ( byte )ScdSoundObjectType.Surface => new ScdLayoutSurfaceDataModel(),
            ( byte )ScdSoundObjectType.BoardObstruction => new ScdLayoutBoardObstructionDataModel(),
            ( byte )ScdSoundObjectType.BoxObstruction => new ScdLayoutBoxObstructionDataModel(),
            ( byte )ScdSoundObjectType.PolylineObstruction => new ScdLayoutPolylineObstructionDataModel(),
            ( byte )ScdSoundObjectType.Polygon => new ScdLayoutPolygonDataModel(),
            ( byte )ScdSoundObjectType.LineExtController => new ScdLayoutLineExtControllerDataModel(),
            ( byte )ScdSoundObjectType.PolygonObstruction => new ScdLayoutPolygonObstructionDataModel(),
            _ => new ScdLayoutRawDataModel()
        };

        try {
            model.Read( payload );
            return model;
        }
        catch {
            var fallback = new ScdLayoutRawDataModel();
            fallback.Read( payload );
            return fallback;
        }
    }
}
