using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Logging;

public sealed class UiLoggerService : ILoggerService {
    public event Action<LogEntry>? EntryLogged;

    public void Info( string message ) => Write( LogLevel.Info, message );
    public void Success( string message ) => Write( LogLevel.Success, message );
    public void Error( string message ) => Write( LogLevel.Error, message );

    private void Write( LogLevel level, string message ) {
        EntryLogged?.Invoke( new LogEntry {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        } );
    }
}
