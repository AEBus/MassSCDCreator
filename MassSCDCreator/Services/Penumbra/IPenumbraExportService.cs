using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Penumbra;

public interface IPenumbraExportService {
    Task<string> ExportPlaylistAsync( IReadOnlyList<string> scdPaths, PenumbraExportOptions options, CancellationToken cancellationToken );
}
