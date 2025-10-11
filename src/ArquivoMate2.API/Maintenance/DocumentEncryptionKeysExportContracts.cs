using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Maintenance;

public sealed record DocumentEncryptionKeysExportCreatedResponse(Guid OperationId);

public sealed record DocumentEncryptionKeysExportStatusResponse(
    Guid OperationId,
    MaintenanceExportState State,
    DateTime CreatedUtc,
    DateTime? CompletedUtc,
    string? ErrorMessage,
    string? DownloadUrl,
    string? FileName);

public sealed record DocumentEncryptionKeysExportDownload(string FilePath, string DownloadFileName);

public interface IDocumentEncryptionKeysExportService
{
    Task<Guid> StartExportAsync(CancellationToken cancellationToken);

    Task<DocumentEncryptionKeysExportMetadata?> GetExportStatusAsync(Guid operationId, CancellationToken cancellationToken);

    Task<DocumentEncryptionKeysExportDownload?> GetDownloadAsync(Guid operationId, CancellationToken cancellationToken);
}
