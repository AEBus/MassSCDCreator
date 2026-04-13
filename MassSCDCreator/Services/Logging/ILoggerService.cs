using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Logging;

public interface ILoggerService {
    event Action<LogEntry>? EntryLogged;

    void Info( string message );
    void Success( string message );
    void Error( string message );
}
