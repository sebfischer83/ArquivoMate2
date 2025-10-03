using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Text;
using System.Text.Json;
using ArquivoMate2.Domain.Document;
using Hangfire;
using Marten;
using Marten.Events;
using Marten.Linq;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.Maintenance;

public sealed class DocumentEncryptionKeysExportJob
{
    private readonly IQuerySession _querySession;
    private readonly IDocumentEncryptionKeysExportStore _store;
    private readonly ILogger<DocumentEncryptionKeysExportJob> _logger;

    public DocumentEncryptionKeysExportJob(
        IQuerySession querySession,
        IDocumentEncryptionKeysExportStore store,
        ILogger<DocumentEncryptionKeysExportJob> logger)
    {
        _querySession = querySession;
        _store = store;
        _logger = logger;
    }

    [Queue("maintenance")]
    public async Task ProcessAsync(Guid operationId, CancellationToken cancellationToken)
    {
        try
        {
            await _store.MarkRunningAsync(operationId, cancellationToken);

            var events = await _querySession.Events
                .QueryAllRawEvents()
                .Where(e => e.EventTypeName == nameof(DocumentEncryptionKeysAdded)
                            || e.EventTypeName == typeof(DocumentEncryptionKeysAdded).FullName)
                .OrderBy(e => e.Sequence)
                .ToListAsync(cancellationToken);

            var payload = events
                .Select(e => new
                {
                    Event = e,
                    Data = e.Data as DocumentEncryptionKeysAdded
                })
                .Where(e => e.Data is not null)
                .Select(e => new DocumentEncryptionKeysBackupItem
                {
                    StreamId = e.Event.StreamId,
                    EventId = e.Event.Id,
                    Sequence = e.Event.Sequence,
                    Timestamp = e.Event.Timestamp.UtcDateTime,
                    Event = e.Data!
                })
                .ToList();

            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(payload, serializerOptions);

            var archiveFileName = $"document-encryption-keys-backup-{DateTime.UtcNow:yyyyMMddHHmmss}-{operationId}.zip";
            var archiveFilePath = _store.GetArchiveFilePath(operationId, archiveFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(archiveFilePath)!);

            await using (var archiveStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("document-encryption-keys.json", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                await writer.WriteAsync(json);
            }

            await _store.MarkCompletedAsync(operationId, archiveFileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export document encryption keys for operation {OperationId}", operationId);
            await _store.MarkFailedAsync(operationId, ex.Message, cancellationToken);
            throw;
        }
    }

    private sealed class DocumentEncryptionKeysBackupItem
    {
        public Guid StreamId { get; init; }

        public Guid EventId { get; init; }

        public long Sequence { get; init; }

        public DateTime Timestamp { get; init; }

        public DocumentEncryptionKeysAdded Event { get; init; } = default!;
    }
}
