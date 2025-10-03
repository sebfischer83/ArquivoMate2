using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArquivoMate2.API.Filters;
using ArquivoMate2.Domain.Document;
using Marten;
using Marten.Events;
using Marten.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Route("api/maintenance")]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
public class MaintenanceController : ControllerBase
{
    private readonly IQuerySession _querySession;

    public MaintenanceController(IQuerySession querySession)
    {
        _querySession = querySession;
    }

    /// <summary>
    /// Creates a ZIP archive containing all DocumentEncryptionKeysAdded events for backup purposes.
    /// </summary>
    [HttpGet("document-encryption-keys")] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadDocumentEncryptionKeysAsync(CancellationToken cancellationToken)
    {
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

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("document-encryption-keys.json", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
            await writer.WriteAsync(json);
            await writer.FlushAsync();
        }

        archiveStream.Position = 0;
        var fileName = $"document-encryption-keys-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        return File(archiveStream.ToArray(), "application/zip", fileName);
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
