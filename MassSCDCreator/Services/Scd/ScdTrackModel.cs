using System.Buffers.Binary;
using System.IO;

namespace MassSCDCreator.Services.Scd;

internal enum ScdTrackCommand : short {
    End = 0,
    Volume = 1,
    Pitch = 2,
    Interval = 3,
    Modulation = 4,
    ReleaseRate = 5,
    Panning = 6,
    KeyOn = 7,
    RandomVolume = 8,
    RandomPitch = 9,
    RandomPan = 10,
    KeyOff = 12,
    LoopStart = 13,
    LoopEnd = 14,
    ExternalAudio = 15,
    EndForLoop = 16,
    AddInterval = 17,
    Expression = 18,
    Velocity = 19,
    MidiVolume = 20,
    MidiAddVolume = 21,
    MidiPan = 22,
    MidiAddPan = 23,
    ModulationType = 24,
    ModulationDepth = 25,
    ModulationAddDepth = 26,
    ModulationSpeed = 27,
    ModulationAddSpeed = 28,
    ModulationOff = 29,
    PitchBend = 30,
    Transpose = 31,
    AddTranspose = 32,
    FrPanning = 33,
    RandomWait = 34,
    Adsr = 35,
    CutOff = 36,
    Jump = 37,
    PlayContinueLoop = 38,
    Sweep = 39,
    MidiKeyOnOld = 40,
    SlurOn = 41,
    SlurOff = 42,
    AutoAdsrEnvelope = 43,
    MidiExternalAudio = 44,
    Marker = 45,
    InitParams = 46,
    Version = 47,
    ReverbOn = 48,
    ReverbOff = 49,
    MidiKeyOn = 50,
    PortamentoOn = 51,
    PortamentoOff = 52,
    MidiEnd = 53,
    ClearKeyInfo = 54,
    ModulationDepthFade = 55,
    ModulationSpeedFade = 56,
    AnalysisFlag = 57,
    Config = 58,
    Filter = 59,
    PlayInnerSound = 60,
    VolumeZeroOne = 61,
    ZeroOneJump = 62,
    ChannelVolumeZeroOne = 63,
    Unknown64 = 64
}

internal abstract class ScdTrackPayloadModel {
    public abstract byte[] ToBytes();
}

internal sealed class ScdTrackRawPayloadModel : ScdTrackPayloadModel {
    public required byte[] Bytes { get; init; }
    public override byte[] ToBytes() => Bytes.ToArray();
}

internal sealed class ScdTrackIntPayloadModel : ScdTrackPayloadModel {
    public int Value { get; set; }
    public override byte[] ToBytes() => BitConverter.GetBytes( Value );
}

internal sealed class ScdTrackShortPayloadModel : ScdTrackPayloadModel {
    public short Value { get; set; }
    public override byte[] ToBytes() => BitConverter.GetBytes( Value );
}

internal sealed class ScdTrackInt2PayloadModel : ScdTrackPayloadModel {
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Value1 );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), Value2 );
        return bytes;
    }
}

internal sealed class ScdTrackFloatPayloadModel : ScdTrackPayloadModel {
    public float Value { get; set; }
    public override byte[] ToBytes() => BitConverter.GetBytes( Value );
}

internal sealed class ScdTrackExternalAudioPayloadModel : ScdTrackPayloadModel {
    public short BankNumber { get; set; }
    public short Index { get; set; }
    public List<short> RandomIndices { get; } = [];

    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( BankNumber );
        writer.Write( Index );
        foreach( var value in RandomIndices ) {
            writer.Write( value );
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackFloat2PayloadModel : ScdTrackPayloadModel {
    public float Value1 { get; set; }
    public float Value2 { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BitConverter.GetBytes( Value1 ).CopyTo( bytes, 0 );
        BitConverter.GetBytes( Value2 ).CopyTo( bytes, 4 );
        return bytes;
    }
}

internal sealed class ScdTrackParamPayloadModel : ScdTrackPayloadModel {
    public float Value { get; set; }
    public int Time { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BitConverter.GetBytes( Value ).CopyTo( bytes, 0 );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), Time );
        return bytes;
    }
}

internal sealed class ScdTrackRandomPayloadModel : ScdTrackPayloadModel {
    public float Upper { get; set; }
    public float Lower { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BitConverter.GetBytes( Upper ).CopyTo( bytes, 0 );
        BitConverter.GetBytes( Lower ).CopyTo( bytes, 4 );
        return bytes;
    }
}

internal sealed class ScdTrackJumpPayloadModel : ScdTrackPayloadModel {
    public int Condition { get; set; }
    public int Offset { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Condition );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), Offset );
        return bytes;
    }
}

internal sealed class ScdTrackModulationPayloadModel : ScdTrackPayloadModel {
    public byte Carrier { get; set; }
    public byte Modulator { get; set; }
    public byte Curve { get; set; }
    public byte Reserved { get; set; }
    public float Depth { get; set; }
    public int Rate { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        bytes[0] = Carrier;
        bytes[1] = Modulator;
        bytes[2] = Curve;
        bytes[3] = Reserved;
        BitConverter.GetBytes( Depth ).CopyTo( bytes, 4 );
        var rateBytes = BitConverter.GetBytes( Rate );
        Array.Resize( ref bytes, 12 );
        rateBytes.CopyTo( bytes, 8 );
        return bytes;
    }
}

internal sealed class ScdTrackModulationTypePayloadModel : ScdTrackPayloadModel {
    public int Carrier { get; set; }
    public int Modulator { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Carrier );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), Modulator );
        return bytes;
    }
}

internal sealed class ScdTrackModulationDepthPayloadModel : ScdTrackPayloadModel {
    public int Carrier { get; set; }
    public float Depth { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Carrier );
        BitConverter.GetBytes( Depth ).CopyTo( bytes, 4 );
        return bytes;
    }
}

internal sealed class ScdTrackModulationSpeedPayloadModel : ScdTrackPayloadModel {
    public int Carrier { get; set; }
    public int Speed { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Carrier );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), Speed );
        return bytes;
    }
}

internal sealed class ScdTrackModulationOffPayloadModel : ScdTrackPayloadModel {
    public int Carrier { get; set; }
    public override byte[] ToBytes() => BitConverter.GetBytes( Carrier );
}

internal sealed class ScdTrackModulationDepthFadePayloadModel : ScdTrackPayloadModel {
    public int Carrier { get; set; }
    public float Depth { get; set; }
    public int FadeTime { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Carrier );
        BitConverter.GetBytes( Depth ).CopyTo( bytes, 4 );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 8, 4 ), FadeTime );
        return bytes;
    }
}

internal sealed class ScdTrackFilterPayloadModel : ScdTrackPayloadModel {
    public int Type { get; set; }
    public float Frequency { get; set; }
    public float InvQ { get; set; }
    public float Gain { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), Type );
        BitConverter.GetBytes( Frequency ).CopyTo( bytes, 4 );
        BitConverter.GetBytes( InvQ ).CopyTo( bytes, 8 );
        BitConverter.GetBytes( Gain ).CopyTo( bytes, 12 );
        return bytes;
    }
}

internal sealed class ScdTrackPortamentoPayloadModel : ScdTrackPayloadModel {
    public int PortamentoTime { get; set; }
    public float Pitch { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), PortamentoTime );
        BitConverter.GetBytes( Pitch ).CopyTo( bytes, 4 );
        return bytes;
    }
}

internal sealed class ScdTrackPlayInnerSoundPayloadModel : ScdTrackPayloadModel {
    public short BankNumber { get; set; }
    public short SoundIndex { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian( bytes.AsSpan( 0, 2 ), BankNumber );
        BinaryPrimitives.WriteInt16LittleEndian( bytes.AsSpan( 2, 2 ), SoundIndex );
        return bytes;
    }
}

internal sealed class ScdTrackAnalysisFlagPayloadModel : ScdTrackPayloadModel {
    public List<short> Data { get; } = [];
    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( ( short )Data.Count );
        foreach( var value in Data ) {
            writer.Write( value );
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackAutoAdsrEnvelopePayloadModel : ScdTrackPayloadModel {
    public int AttackTime { get; set; }
    public int DecayTime { get; set; }
    public int SustainLevel { get; set; }
    public int ReleaseTime { get; set; }
    public override byte[] ToBytes() {
        var bytes = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 0, 4 ), AttackTime );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 4, 4 ), DecayTime );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 8, 4 ), SustainLevel );
        BinaryPrimitives.WriteInt32LittleEndian( bytes.AsSpan( 12, 4 ), ReleaseTime );
        return bytes;
    }
}

internal sealed class ScdTrackConfigPayloadModel : ScdTrackPayloadModel {
    public short Type { get; set; }
    public ushort Count { get; set; }
    public ushort? DataSingle { get; set; }
    public List<ushort> DataList { get; } = [];
    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( Type );
        writer.Write( Count );
        if( Type == 1 ) {
            writer.Write( DataSingle.GetValueOrDefault() );
        }
        else if( Type > 1 ) {
            foreach( var item in DataList ) {
                writer.Write( item );
            }
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackUnknown64ItemModel {
    public short BankNumber { get; set; }
    public short Index { get; set; }
    public int Unknown1 { get; set; }
    public float Unknown2 { get; set; }
}

internal sealed class ScdTrackUnknown64PayloadModel : ScdTrackPayloadModel {
    public byte Version { get; set; }
    public short Unknown1 { get; set; }
    public List<ScdTrackUnknown64ItemModel> Items { get; } = [];
    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( Version );
        writer.Write( ( byte )Items.Count );
        writer.Write( Unknown1 );
        foreach( var item in Items ) {
            writer.Write( item.BankNumber );
            writer.Write( item.Index );
            writer.Write( item.Unknown1 );
            writer.Write( item.Unknown2 );
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackZeroOnePointModel {
    public short ZeroOne { get; set; }
    public short Value { get; set; }
}

internal sealed class ScdTrackVolumeZeroOnePayloadModel : ScdTrackPayloadModel {
    public byte Version { get; set; }
    public byte Reserved1 { get; set; }
    public short HeaderSize { get; set; }
    public List<ScdTrackZeroOnePointModel> Points { get; } = [];
    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( Version );
        writer.Write( Reserved1 );
        writer.Write( HeaderSize );
        writer.Write( ( short )Points.Count );
        foreach( var point in Points ) {
            writer.Write( point.ZeroOne );
            writer.Write( point.Value );
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackChannelVolumeZeroOnePayloadModel : ScdTrackPayloadModel {
    public byte Version { get; set; }
    public byte Reserved1 { get; set; }
    public short HeaderSize { get; set; }
    public List<ScdTrackVolumeZeroOnePayloadModel> Channels { get; } = [];
    public override byte[] ToBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        writer.Write( Version );
        writer.Write( Reserved1 );
        writer.Write( HeaderSize );
        writer.Write( ( short )Channels.Count );
        foreach( var channel in Channels ) {
            writer.Write( channel.ToBytes() );
        }
        return stream.ToArray();
    }
}

internal sealed class ScdTrackItemModel {
    public required short CommandId { get; init; }
    public required byte[] Payload { get; set; }
    public required ScdTrackPayloadModel PayloadModel { get; init; }

    public ScdTrackCommand? KnownCommand =>
        Enum.IsDefined( typeof( ScdTrackCommand ), CommandId ) ? ( ScdTrackCommand )CommandId : null;

    public ScdTrackItemModel Clone() => new() {
        CommandId = CommandId,
        Payload = Payload.ToArray(),
        PayloadModel = CreatePayloadModel( CommandId, Payload )
    };

    public void Write( BinaryWriter writer ) {
        Payload = PayloadModel.ToBytes();
        writer.Write( CommandId );
        writer.Write( Payload );
    }

    public void SetInt32( int value ) {
        if( PayloadModel is ScdTrackIntPayloadModel intPayload ) {
            intPayload.Value = value;
            Payload = intPayload.ToBytes();
            return;
        }

        if( Payload.Length < 4 ) {
            throw new ScdFormatException( $"Track command {CommandId} does not contain a 4-byte value." );
        }

        BinaryPrimitives.WriteInt32LittleEndian( Payload.AsSpan( 0, 4 ), value );
    }

    public static ScdTrackPayloadModel CreatePayloadModel( short commandId, byte[] payload ) {
        return commandId switch {
            3 or 5 or 62 when payload.Length >= 4 => new ScdTrackIntPayloadModel { Value = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ) },
            13 when payload.Length >= 8 => new ScdTrackInt2PayloadModel {
                Value1 = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Value2 = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            47 when payload.Length >= 2 => new ScdTrackShortPayloadModel { Value = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 0, 2 ) ) },
            48 when payload.Length >= 4 => new ScdTrackFloatPayloadModel { Value = BitConverter.ToSingle( payload, 0 ) },
            1 or 2 or 6 or 33 or 35 when payload.Length >= 8 => new ScdTrackParamPayloadModel {
                Value = BitConverter.ToSingle( payload, 0 ),
                Time = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            17 or 19 or 21 or 23 or 32 when payload.Length >= 4 => new ScdTrackFloatPayloadModel {
                Value = BitConverter.ToSingle( payload, 0 )
            },
            18 or 20 or 22 or 30 or 31 or 50 when payload.Length >= 8 => new ScdTrackFloat2PayloadModel {
                Value1 = BitConverter.ToSingle( payload, 0 ),
                Value2 = BitConverter.ToSingle( payload, 4 )
            },
            8 or 9 or 10 when payload.Length >= 8 => new ScdTrackRandomPayloadModel {
                Upper = BitConverter.ToSingle( payload, 0 ),
                Lower = BitConverter.ToSingle( payload, 4 )
            },
            15 or 44 when payload.Length >= 4 => ReadExternalAudioPayload( payload ),
            37 when payload.Length >= 8 => new ScdTrackJumpPayloadModel {
                Condition = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Offset = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            4 when payload.Length >= 12 => new ScdTrackModulationPayloadModel {
                Carrier = payload[0],
                Modulator = payload[1],
                Curve = payload[2],
                Reserved = payload[3],
                Depth = BitConverter.ToSingle( payload, 4 ),
                Rate = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 8, 4 ) )
            },
            24 when payload.Length >= 8 => new ScdTrackModulationTypePayloadModel {
                Carrier = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Modulator = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            25 or 26 when payload.Length >= 8 => new ScdTrackModulationDepthPayloadModel {
                Carrier = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Depth = BitConverter.ToSingle( payload, 4 )
            },
            27 or 28 when payload.Length >= 8 => new ScdTrackModulationSpeedPayloadModel {
                Carrier = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Speed = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            29 when payload.Length >= 4 => new ScdTrackModulationOffPayloadModel {
                Carrier = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) )
            },
            39 when payload.Length >= 8 => new ScdTrackParamPayloadModel {
                Value = BitConverter.ToSingle( payload, 0 ),
                Time = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) )
            },
            51 when payload.Length >= 8 => new ScdTrackPortamentoPayloadModel {
                PortamentoTime = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Pitch = BitConverter.ToSingle( payload, 4 )
            },
            55 or 56 when payload.Length >= 12 => new ScdTrackModulationDepthFadePayloadModel {
                Carrier = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Depth = BitConverter.ToSingle( payload, 4 ),
                FadeTime = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 8, 4 ) )
            },
            59 when payload.Length >= 16 => new ScdTrackFilterPayloadModel {
                Type = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                Frequency = BitConverter.ToSingle( payload, 4 ),
                InvQ = BitConverter.ToSingle( payload, 8 ),
                Gain = BitConverter.ToSingle( payload, 12 )
            },
            60 when payload.Length >= 4 => new ScdTrackPlayInnerSoundPayloadModel {
                BankNumber = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 0, 2 ) ),
                SoundIndex = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2, 2 ) )
            },
            43 when payload.Length >= 16 => new ScdTrackAutoAdsrEnvelopePayloadModel {
                AttackTime = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 0, 4 ) ),
                DecayTime = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 4, 4 ) ),
                SustainLevel = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 8, 4 ) ),
                ReleaseTime = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( 12, 4 ) )
            },
            57 when payload.Length >= 2 => ReadAnalysisFlagPayload( payload),
            58 when payload.Length >= 4 => ReadConfigPayload( payload ),
            61 when payload.Length >= 6 => ReadVolumeZeroOnePayload( payload ),
            63 when payload.Length >= 6 => ReadChannelVolumeZeroOnePayload( payload ),
            64 when payload.Length >= 4 => ReadUnknown64Payload( payload ),
            _ => new ScdTrackRawPayloadModel { Bytes = payload.ToArray() }
        };
    }

    private static ScdTrackAnalysisFlagPayloadModel ReadAnalysisFlagPayload( byte[] payload ) {
        var model = new ScdTrackAnalysisFlagPayloadModel();
        var count = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 0, 2 ) );
        for( var index = 0; index < count && 2 + ( index * 2 ) + 2 <= payload.Length; index++ ) {
            model.Data.Add( BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2 + ( index * 2 ), 2 ) ) );
        }
        return model;
    }

    private static ScdTrackExternalAudioPayloadModel ReadExternalAudioPayload( byte[] payload ) {
        var model = new ScdTrackExternalAudioPayloadModel {
            BankNumber = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 0, 2 ) ),
            Index = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2, 2 ) )
        };

        if( model.Index < 0 ) {
            var count = -model.Index;
            for( var index = 0; index < count && 4 + ( index * 2 ) + 2 <= payload.Length; index++ ) {
                model.RandomIndices.Add( BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 4 + ( index * 2 ), 2 ) ) );
            }
        }

        return model;
    }

    private static ScdTrackConfigPayloadModel ReadConfigPayload( byte[] payload ) {
        var model = new ScdTrackConfigPayloadModel {
            Type = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 0, 2 ) ),
            Count = BinaryPrimitives.ReadUInt16LittleEndian( payload.AsSpan( 2, 2 ) )
        };

        if( model.Type == 1 && payload.Length >= 6 ) {
            model.DataSingle = BinaryPrimitives.ReadUInt16LittleEndian( payload.AsSpan( 4, 2 ) );
        }
        else if( model.Type > 1 ) {
            for( var index = 0; index < model.Count && 4 + ( index * 2 ) + 2 <= payload.Length; index++ ) {
                model.DataList.Add( BinaryPrimitives.ReadUInt16LittleEndian( payload.AsSpan( 4 + ( index * 2 ), 2 ) ) );
            }
        }

        return model;
    }

    private static ScdTrackVolumeZeroOnePayloadModel ReadVolumeZeroOnePayload( byte[] payload ) {
        var model = new ScdTrackVolumeZeroOnePayloadModel {
            Version = payload[0],
            Reserved1 = payload[1],
            HeaderSize = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2, 2 ) )
        };
        var count = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 4, 2 ) );
        var offset = 6;
        for( var index = 0; index < count && offset + 4 <= payload.Length; index++ ) {
            model.Points.Add( new ScdTrackZeroOnePointModel {
                ZeroOne = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset, 2 ) ),
                Value = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset + 2, 2 ) )
            } );
            offset += 4;
        }
        return model;
    }

    private static ScdTrackChannelVolumeZeroOnePayloadModel ReadChannelVolumeZeroOnePayload( byte[] payload ) {
        var model = new ScdTrackChannelVolumeZeroOnePayloadModel {
            Version = payload[0],
            Reserved1 = payload[1],
            HeaderSize = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2, 2 ) )
        };
        var count = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 4, 2 ) );
        var offset = 6;
        for( var channel = 0; channel < count && offset + 6 <= payload.Length; channel++ ) {
            var channelHeaderSize = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset + 2, 2 ) );
            var pointCount = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset + 4, 2 ) );
            var channelBytesLength = 6 + ( pointCount * 4 );
            if( offset + channelBytesLength > payload.Length ) {
                break;
            }

            var channelPayload = payload.AsSpan( offset, channelBytesLength ).ToArray();
            model.Channels.Add( ReadVolumeZeroOnePayload( channelPayload ) );
            offset += channelBytesLength;
        }
        return model;
    }

    private static ScdTrackUnknown64PayloadModel ReadUnknown64Payload( byte[] payload ) {
        var model = new ScdTrackUnknown64PayloadModel {
            Version = payload[0],
            Unknown1 = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( 2, 2 ) )
        };
        var count = payload[1];
        var offset = 4;
        for( var index = 0; index < count && offset + 12 <= payload.Length; index++ ) {
            model.Items.Add( new ScdTrackUnknown64ItemModel {
                BankNumber = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset, 2 ) ),
                Index = BinaryPrimitives.ReadInt16LittleEndian( payload.AsSpan( offset + 2, 2 ) ),
                Unknown1 = BinaryPrimitives.ReadInt32LittleEndian( payload.AsSpan( offset + 4, 4 ) ),
                Unknown2 = BitConverter.ToSingle( payload, offset + 8 )
            } );
            offset += 12;
        }
        return model;
    }
}

internal sealed class ScdTrackEntryModel {
    public required byte[] RawBytes { get; set; }
    public List<ScdTrackItemModel> Items { get; } = [];
    public bool ParsedFully { get; private set; }

    public static ScdTrackEntryModel Read( byte[] rawBytes ) {
        var entry = new ScdTrackEntryModel {
            RawBytes = rawBytes
        };

        using var stream = new MemoryStream( rawBytes, writable: false );
        using var reader = new BinaryReader( stream );

        try {
            while( stream.Position < stream.Length ) {
                var commandId = reader.ReadInt16();
                if( !TryReadPayload( reader, commandId, out var payload ) ) {
                    entry.Items.Clear();
                    entry.ParsedFully = false;
                    return entry;
                }

                entry.Items.Add( new ScdTrackItemModel {
                    CommandId = commandId,
                    Payload = payload,
                    PayloadModel = ScdTrackItemModel.CreatePayloadModel( commandId, payload )
                } );

                if( commandId is 0 or 14 or 53 ) {
                    entry.ParsedFully = stream.Position == stream.Length;
                    return entry;
                }
            }
        }
        catch {
            entry.Items.Clear();
            entry.ParsedFully = false;
            return entry;
        }

        entry.ParsedFully = false;
        return entry;
    }

    public ScdTrackEntryModel Clone() {
        var clone = new ScdTrackEntryModel {
            RawBytes = RawBytes.ToArray()
        };
        clone.Items.AddRange( Items.Select( item => item.Clone() ) );
        clone.ParsedFully = ParsedFully;
        return clone;
    }

    public void UpdatePlaybackTimeline( int playLengthSamples, bool enableLoop ) {
        if( playLengthSamples < 0 ) {
            playLengthSamples = 0;
        }

        if( Items.Count > 0 && TryUpdateParsedTimeline( playLengthSamples, enableLoop ) ) {
            RebuildRawBytes();
            return;
        }

        if( enableLoop ) {
            PatchRawLoopTimeline( playLengthSamples );
            return;
        }

        throw new ScdFormatException( "Could not convert the track to a non-looping timeline because the track data could not be parsed." );
    }

    private bool TryUpdateParsedTimeline( int playLengthSamples, bool enableLoop ) {
        var loopSequenceIndex = FindLoopSequenceIndex();
        if( loopSequenceIndex >= 0 ) {
            if( enableLoop ) {
                Items[loopSequenceIndex + 1].SetInt32( 0 );
                Items[loopSequenceIndex + 3].SetInt32( playLengthSamples );
            }
            else {
                Items.RemoveRange( loopSequenceIndex + 1, 4 );
                Items.Insert( loopSequenceIndex + 1, CreateIntervalItem( playLengthSamples ) );
                Items.Insert( loopSequenceIndex + 2, CreateNoPayloadItem( ScdTrackCommand.KeyOff ) );
                Items.Insert( loopSequenceIndex + 3, CreateNoPayloadItem( ScdTrackCommand.End ) );
            }

            return true;
        }

        var oneShotSequenceIndex = FindOneShotSequenceIndex();
        if( oneShotSequenceIndex < 0 ) {
            return false;
        }

        if( enableLoop ) {
            Items.RemoveRange( oneShotSequenceIndex + 1, 3 );
            Items.Insert( oneShotSequenceIndex + 1, CreateIntervalItem( 0 ) );
            Items.Insert( oneShotSequenceIndex + 2, CreateLoopStartItem() );
            Items.Insert( oneShotSequenceIndex + 3, CreateIntervalItem( playLengthSamples ) );
            Items.Insert( oneShotSequenceIndex + 4, CreateNoPayloadItem( ScdTrackCommand.LoopEnd ) );
        }
        else {
            Items[oneShotSequenceIndex + 1].SetInt32( playLengthSamples );
        }

        return true;
    }

    private int FindLoopSequenceIndex() {
        for( var index = 0; index <= Items.Count - 5; index++ ) {
            if( Items[index].CommandId == ( short )ScdTrackCommand.KeyOn &&
                Items[index + 1].CommandId == ( short )ScdTrackCommand.Interval &&
                Items[index + 2].CommandId == ( short )ScdTrackCommand.LoopStart &&
                Items[index + 3].CommandId == ( short )ScdTrackCommand.Interval &&
                Items[index + 4].CommandId == ( short )ScdTrackCommand.LoopEnd ) {
                return index;
            }
        }

        return -1;
    }

    private int FindOneShotSequenceIndex() {
        for( var index = 0; index <= Items.Count - 4; index++ ) {
            if( Items[index].CommandId == ( short )ScdTrackCommand.KeyOn &&
                Items[index + 1].CommandId == ( short )ScdTrackCommand.Interval &&
                Items[index + 2].CommandId == ( short )ScdTrackCommand.KeyOff &&
                Items[index + 3].CommandId == ( short )ScdTrackCommand.End ) {
                return index;
            }
        }

        return -1;
    }

    private void RebuildRawBytes() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        foreach( var item in Items ) {
            item.Write( writer );
        }

        RawBytes = stream.ToArray();
    }

    private void PatchRawLoopTimeline( int playLengthSamples ) {
        var patchedCount = 0;
        var end = RawBytes.Length - 30;
        for( var index = 0; index <= end; index++ ) {
            if( RawBytes[index] == 0x07 && RawBytes[index + 1] == 0x00 &&
                RawBytes[index + 2] == 0x03 && RawBytes[index + 3] == 0x00 &&
                RawBytes[index + 8] == 0x0D && RawBytes[index + 9] == 0x00 &&
                RawBytes[index + 18] == 0x03 && RawBytes[index + 19] == 0x00 &&
                RawBytes[index + 24] == 0x0E && RawBytes[index + 25] == 0x00 ) {
                BinaryPrimitives.WriteInt32LittleEndian( RawBytes.AsSpan( index + 4, 4 ), 0 );
                BinaryPrimitives.WriteInt32LittleEndian( RawBytes.AsSpan( index + 20, 4 ), playLengthSamples );
                patchedCount++;
                index += 25;
            }
        }

        if( patchedCount == 0 ) {
            throw new ScdFormatException( "Could not find the KeyOn -> Interval -> LoopStart -> Interval -> LoopEnd track sequence in the template track data." );
        }
    }

    private static ScdTrackItemModel CreateIntervalItem( int value ) => new() {
        CommandId = ( short )ScdTrackCommand.Interval,
        Payload = BitConverter.GetBytes( value ),
        PayloadModel = new ScdTrackIntPayloadModel { Value = value }
    };

    private static ScdTrackItemModel CreateLoopStartItem() => new() {
        CommandId = ( short )ScdTrackCommand.LoopStart,
        Payload = new byte[8],
        PayloadModel = new ScdTrackInt2PayloadModel()
    };

    private static ScdTrackItemModel CreateNoPayloadItem( ScdTrackCommand command ) => new() {
        CommandId = ( short )command,
        Payload = [],
        PayloadModel = new ScdTrackRawPayloadModel { Bytes = [] }
    };

    public void Write( BinaryWriter writer ) {
        writer.Write( RawBytes );
    }

    private static bool TryReadPayload( BinaryReader reader, short commandId, out byte[] payload ) {
        payload = commandId switch {
            47 => reader.ReadBytes( 2 ),
            5 or 17 or 19 or 21 or 23 or 29 or 32 or 48 or 60 or 62 => reader.ReadBytes( 4 ),
            1 or 2 or 6 or 18 or 20 or 22 or 24 or 25 or 26 or 27 or 28 or 30 or 31 or 33 or 34 or 35 or 50 or 51 => reader.ReadBytes( 8 ),
            8 or 9 or 10 => reader.ReadBytes( 8 ),
            15 or 44 => ReadExternalAudioPayloadBytes( reader ),
            39 => reader.ReadBytes( 8 ),
            4 => reader.ReadBytes( 12 ),
            37 => reader.ReadBytes( 8 ),
            43 => reader.ReadBytes( 16 ),
            55 or 56 => reader.ReadBytes( 12 ),
            59 => reader.ReadBytes( 16 ),
            61 or 63 => ReadZeroOnePayload( reader ),
            57 => ReadAnalysisFlagPayloadBytes( reader ),
            58 => ReadConfigPayloadBytes( reader ),
            64 => ReadUnknown64PayloadBytes( reader ),
            7 or 12 or 14 or 0 or 16 or 38 or 40 or 41 or 42 or 45 or 46 or 49 or 52 or 53 or 54 => [],
            3 => reader.ReadBytes( 4 ),
            13 => reader.ReadBytes( 8 ),
            36 => reader.ReadBytes( 0 ),
            _ => null!
        };

        return payload is not null;
    }

    private static byte[] ReadZeroOnePayload( BinaryReader reader ) {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter( ms );

        var version = reader.ReadByte();
        var reserved = reader.ReadByte();
        var headerSize = reader.ReadInt16();
        var count = reader.ReadInt16();

        writer.Write( version );
        writer.Write( reserved );
        writer.Write( headerSize );
        writer.Write( count );

        for( var channel = 0; channel < count; channel++ ) {
            var channelVersion = reader.ReadByte();
            var channelReserved = reader.ReadByte();
            var channelHeaderSize = reader.ReadInt16();
            var pointCount = reader.ReadInt16();

            writer.Write( channelVersion );
            writer.Write( channelReserved );
            writer.Write( channelHeaderSize );
            writer.Write( pointCount );

            for( var point = 0; point < pointCount; point++ ) {
                writer.Write( reader.ReadInt16() );
                writer.Write( reader.ReadInt16() );
            }
        }

        return ms.ToArray();
    }

    private static byte[] ReadAnalysisFlagPayloadBytes( BinaryReader reader ) {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter( ms );
        var count = reader.ReadInt16();
        writer.Write( count );
        for( var index = 0; index < count; index++ ) {
            writer.Write( reader.ReadInt16() );
        }
        return ms.ToArray();
    }

    private static byte[] ReadExternalAudioPayloadBytes( BinaryReader reader ) {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter( ms );

        var bankNumber = reader.ReadInt16();
        var index = reader.ReadInt16();
        writer.Write( bankNumber );
        writer.Write( index );

        if( index < 0 ) {
            for( var i = 0; i < -index; i++ ) {
                writer.Write( reader.ReadInt16() );
            }
        }

        return ms.ToArray();
    }

    private static byte[] ReadConfigPayloadBytes( BinaryReader reader ) {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter( ms );
        var type = reader.ReadInt16();
        var count = reader.ReadUInt16();
        writer.Write( type );
        writer.Write( count );
        if( type == 1 ) {
            writer.Write( reader.ReadUInt16() );
        }
        else if( type > 1 ) {
            for( var index = 0; index < count; index++ ) {
                writer.Write( reader.ReadUInt16() );
            }
        }
        return ms.ToArray();
    }

    private static byte[] ReadUnknown64PayloadBytes( BinaryReader reader ) {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter( ms );
        var version = reader.ReadByte();
        var count = reader.ReadByte();
        var unknown1 = reader.ReadInt16();
        writer.Write( version );
        writer.Write( count );
        writer.Write( unknown1 );
        for( var index = 0; index < count; index++ ) {
            writer.Write( reader.ReadInt16() );
            writer.Write( reader.ReadInt16() );
            writer.Write( reader.ReadInt32() );
            writer.Write( reader.ReadSingle() );
        }
        return ms.ToArray();
    }
}
