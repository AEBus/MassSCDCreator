using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Processing;

public interface IFileBatchProcessor {
    Task<BatchProcessResult> ProcessAsync( ProcessRequest request, IProgress<ProcessingProgress> progress, CancellationToken cancellationToken );
}
