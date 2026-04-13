using System.IO;

namespace MassSCDCreator.Services.Scd;

internal sealed class ScdAttributeEntryModel {
    public byte Version { get; set; }
    public byte Reserved { get; set; }
    public short AttributeId { get; set; }
    public short SearchAttributeId { get; set; }
    public byte ConditionFirst { get; set; }
    public byte ArgumentCount { get; set; }
    public int SoundLabelLow { get; set; }
    public int SoundLabelHigh { get; set; }
    public ScdAttributeResultCommandModel ResultFirst { get; set; } = new();
    public ScdAttributeExtendDataModel Extend1 { get; set; } = new();
    public ScdAttributeExtendDataModel Extend2 { get; set; } = new();
    public ScdAttributeExtendDataModel Extend3 { get; set; } = new();
    public ScdAttributeExtendDataModel Extend4 { get; set; } = new();
    public byte[] TailPayload { get; set; } = [];

    public static ScdAttributeEntryModel Read( byte[] rawEntry ) {
        using var stream = new MemoryStream( rawEntry, writable: false );
        using var reader = new BinaryReader( stream );

        var entry = new ScdAttributeEntryModel {
            Version = reader.ReadByte(),
            Reserved = reader.ReadByte(),
            AttributeId = reader.ReadInt16(),
            SearchAttributeId = reader.ReadInt16(),
            ConditionFirst = reader.ReadByte(),
            ArgumentCount = reader.ReadByte(),
            SoundLabelLow = reader.ReadInt32(),
            SoundLabelHigh = reader.ReadInt32()
        };

        entry.ResultFirst = ScdAttributeResultCommandModel.Read( reader );
        entry.Extend1 = ScdAttributeExtendDataModel.Read( reader );
        entry.Extend2 = ScdAttributeExtendDataModel.Read( reader );
        entry.Extend3 = ScdAttributeExtendDataModel.Read( reader );
        entry.Extend4 = ScdAttributeExtendDataModel.Read( reader );
        entry.TailPayload = reader.ReadBytes( ( int )( stream.Length - stream.Position ) );
        return entry;
    }

    public ScdAttributeEntryModel Clone() {
        return new ScdAttributeEntryModel {
            Version = Version,
            Reserved = Reserved,
            AttributeId = AttributeId,
            SearchAttributeId = SearchAttributeId,
            ConditionFirst = ConditionFirst,
            ArgumentCount = ArgumentCount,
            SoundLabelLow = SoundLabelLow,
            SoundLabelHigh = SoundLabelHigh,
            ResultFirst = ResultFirst.Clone(),
            Extend1 = Extend1.Clone(),
            Extend2 = Extend2.Clone(),
            Extend3 = Extend3.Clone(),
            Extend4 = Extend4.Clone(),
            TailPayload = TailPayload.ToArray()
        };
    }

    public void Write( BinaryWriter writer ) {
        writer.Write( Version );
        writer.Write( Reserved );
        writer.Write( AttributeId );
        writer.Write( SearchAttributeId );
        writer.Write( ConditionFirst );
        writer.Write( ArgumentCount );
        writer.Write( SoundLabelLow );
        writer.Write( SoundLabelHigh );
        ResultFirst.Write( writer );
        Extend1.Write( writer );
        Extend2.Write( writer );
        Extend3.Write( writer );
        Extend4.Write( writer );
        writer.Write( TailPayload );
    }
}

internal sealed class ScdAttributeResultCommandModel {
    public byte SelfCommand { get; set; }
    public byte TargetCommand { get; set; }
    public ushort Reserved1 { get; set; }
    public int SelfArgument { get; set; }
    public int TargetArgument { get; set; }

    public static ScdAttributeResultCommandModel Read( BinaryReader reader ) => new() {
        SelfCommand = reader.ReadByte(),
        TargetCommand = reader.ReadByte(),
        Reserved1 = reader.ReadUInt16(),
        SelfArgument = reader.ReadInt32(),
        TargetArgument = reader.ReadInt32()
    };

    public ScdAttributeResultCommandModel Clone() => new() {
        SelfCommand = SelfCommand,
        TargetCommand = TargetCommand,
        Reserved1 = Reserved1,
        SelfArgument = SelfArgument,
        TargetArgument = TargetArgument
    };

    public void Write( BinaryWriter writer ) {
        writer.Write( SelfCommand );
        writer.Write( TargetCommand );
        writer.Write( Reserved1 );
        writer.Write( SelfArgument );
        writer.Write( TargetArgument );
    }
}

internal sealed class ScdAttributeExtendDataModel {
    public byte FirstCondition { get; set; }
    public byte SecondCondition { get; set; }
    public byte JoinType { get; set; }
    public byte NumberOfConditions { get; set; }
    public int SelfArgument { get; set; }
    public int TargetArgumentRaw { get; set; }
    public ScdAttributeResultCommandModel Result { get; set; } = new();

    public static ScdAttributeExtendDataModel Read( BinaryReader reader ) => new() {
        FirstCondition = reader.ReadByte(),
        SecondCondition = reader.ReadByte(),
        JoinType = reader.ReadByte(),
        NumberOfConditions = reader.ReadByte(),
        SelfArgument = reader.ReadInt32(),
        TargetArgumentRaw = reader.ReadInt32(),
        Result = ScdAttributeResultCommandModel.Read( reader )
    };

    public ScdAttributeExtendDataModel Clone() => new() {
        FirstCondition = FirstCondition,
        SecondCondition = SecondCondition,
        JoinType = JoinType,
        NumberOfConditions = NumberOfConditions,
        SelfArgument = SelfArgument,
        TargetArgumentRaw = TargetArgumentRaw,
        Result = Result.Clone()
    };

    public void Write( BinaryWriter writer ) {
        writer.Write( FirstCondition );
        writer.Write( SecondCondition );
        writer.Write( JoinType );
        writer.Write( NumberOfConditions );
        writer.Write( SelfArgument );
        writer.Write( TargetArgumentRaw );
        Result.Write( writer );
    }
}
