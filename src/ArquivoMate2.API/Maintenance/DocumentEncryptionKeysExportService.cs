using System;
using System.Threading;
using Hangfire;

namespace ArquivoMate2.API.Maintenance;

public sealed class DocumentEncryptionKeysExportService : IDocumentEncryptionKeysExportService
{
    private readonly IDocumentEncryptionKeysExportStore _store;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public DocumentEncryptionKeysExportService(
        IDocumentEncryptionKeysExportStore store,
        IBackgroundJobClient backgroundJobClient)
    {
        _store = store;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<Guid> StartExportAsync(CancellationToken cancellationToken)
    {
        var operationId = await _store.CreatePendingAsync(cancellationToken);
        _backgroundJobClient.Enqueue<DocumentEncryptionKeysExportJob>(job => job.ProcessAsync(operationId, CancellationToken.None));
        return operationId;
    }

    public Task<DocumentEncryptionKeysExportMetadata?> GetExportStatusAsync(Guid operationId, CancellationToken cancellationToken)
        => _store.GetAsync(operationId, cancellationToken);

    public Task<DocumentEncryptionKeysExportDownload?> GetDownloadAsync(Guid operationId, CancellationToken cancellationToken)
        => _store.TryGetDownloadAsync(operationId, cancellationToken);
}
