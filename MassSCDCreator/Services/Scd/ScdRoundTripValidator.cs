using System.IO;

namespace MassSCDCreator.Services.Scd;

internal static class ScdRoundTripValidator {
    public static void Validate( ScdFileModel model ) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter( stream );
        model.Write( writer );
        var fileSize = checked( ( int )stream.Length );

        writer.BaseStream.Position = 0x10;
        writer.Write( fileSize );
        writer.BaseStream.Position = 0;

        using var reader = new BinaryReader( new MemoryStream( stream.ToArray(), writable: false ) );
        var header = ScdHeader.Read( reader );
        var offsets = ScdOffsetTable.Read( reader );
        var reloaded = ScdFileModel.Load( reader, header, offsets );

        if( reloaded.SoundEntries.Count != model.SoundEntries.Count ||
            reloaded.TrackEntries.Count != model.TrackEntries.Count ||
            reloaded.AudioEntries.Count != model.AudioEntries.Count ||
            reloaded.LayoutEntries.Count != model.LayoutEntries.Count ||
            reloaded.AttributeEntries.Count != model.AttributeEntries.Count ) {
            throw new ScdFormatException( "SCD round-trip validation failed: section counts changed after save/load." );
        }
    }
}
