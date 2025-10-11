using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Maintenance;

public interface IDocumentEncryptionKeysExportStore
{
    Task<Guid> CreatePendingAsync(CancellationToken cancellationToken);

    Task<DocumentEncryptionKeysExportMetadata?> GetAsync(Guid operationId, CancellationToken cancellationToken);

    Task MarkRunningAsync(Guid operationId, CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid operationId, string archiveFileName, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid operationId, string errorMessage, CancellationToken cancellationToken);

    string GetArchiveFilePath(Guid operationId, string archiveFileName);

    Task<DocumentEncryptionKeysExportDownload?> TryGetDownloadAsync(Guid operationId, CancellationToken cancellationToken);

    Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken);
}
