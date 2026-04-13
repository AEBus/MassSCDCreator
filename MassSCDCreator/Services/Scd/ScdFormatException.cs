namespace MassSCDCreator.Services.Scd;

public sealed class ScdFormatException : Exception {
    public ScdFormatException( string message ) : base( message ) {
    }
}
