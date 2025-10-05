using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ArquivoMate2.API.Filters;
using ArquivoMate2.Domain.Document;
using Marten;
using Marten.Events;
using Marten.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Route("api/maintenance")]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
public class MaintenanceController : ControllerBase
{
    private readonly IQuerySession _querySession;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IQuerySession querySession, ILogger<MaintenanceController> logger)
    {
        _querySession = querySession;
        _logger = logger;
    }

    /// <summary>
    /// Creates a ZIP archive containing all DocumentEncryptionKeysAdded events for backup purposes.
    /// Adds a metadata wrapper and a SHA256 hash file for integrity verification.
    /// </summary>
    /// <param name="fromSequence">Optional lower sequence bound (inclusive).</param>
    /// <param name="toSequence">Optional upper sequence bound (inclusive).</param>
    [HttpGet("document-encryption-keys")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/zip")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> DownloadDocumentEncryptionKeysAsync([FromQuery] long? fromSequence, [FromQuery] long? toSequence, CancellationToken cancellationToken)
    {
        var baseQuery = _querySession.Events
            .QueryAllRawEvents()
            .Where(e => e.EventTypeName == nameof(DocumentEncryptionKeysAdded) || e.EventTypeName == typeof(DocumentEncryptionKeysAdded).FullName);

        if (fromSequence.HasValue)
            baseQuery = baseQuery.Where(e => e.Sequence >= fromSequence.Value);
        if (toSequence.HasValue)
            baseQuery = baseQuery.Where(e => e.Sequence <= toSequence.Value);

        var ordered = baseQuery.OrderBy(e => e.Sequence);

        // Materialize only IDs & raw events once (still may be large; Marten lacks fine-grained streaming here)
        var events = await ordered.ToListAsync(cancellationToken);

        var total = events.Count;
        var exportedAt = DateTime.UtcNow;
        _logger.LogInformation("Starting encryption key backup. Count={Count}, From={From}, To={To}", total, fromSequence, toSequence);

        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // JSON entry
            var jsonEntry = archive.CreateEntry("document-encryption-keys.json", CompressionLevel.Optimal);
            using var entryStream = jsonEntry.Open();
            using var sha256 = SHA256.Create();
            using var hashingStream = new CryptoStream(entryStream, sha256, CryptoStreamMode.Write);
            var writerOptions = new JsonWriterOptions { Indented = true, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            using (var jsonWriter = new Utf8JsonWriter(hashingStream, writerOptions))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteString("formatVersion", "1");
                jsonWriter.WriteString("exportedAtUtc", exportedAt.ToString("O"));
                if (fromSequence.HasValue) jsonWriter.WriteNumber("fromSequence", fromSequence.Value);
                if (toSequence.HasValue) jsonWriter.WriteNumber("toSequence", toSequence.Value);
                jsonWriter.WriteNumber("itemCount", total);
                jsonWriter.WritePropertyName("items");
                jsonWriter.WriteStartArray();

                foreach (var e in events)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (e.Data is not DocumentEncryptionKeysAdded data) continue;
                    jsonWriter.WriteStartObject();
                    jsonWriter.WriteString("streamId", e.StreamId.ToString());
                    jsonWriter.WriteString("eventId", e.Id.ToString());
                    jsonWriter.WriteNumber("sequence", e.Sequence);
                    jsonWriter.WriteString("timestampUtc", e.Timestamp.UtcDateTime.ToString("O"));
                    jsonWriter.WritePropertyName("event");
                    JsonSerializer.Serialize(jsonWriter, data, data.GetType());
                    jsonWriter.WriteEndObject();
                }

                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }
            // finalize hash
            hashingStream.FlushFinalBlock();
            var hash = sha256.Hash ?? Array.Empty<byte>();

            // Hash entry (hex + original file name)
            var hashEntry = archive.CreateEntry("document-encryption-keys.sha256", CompressionLevel.Optimal);
            using (var hashStream = hashEntry.Open())
            using (var sw = new StreamWriter(hashStream, new UTF8Encoding(false)))
            {
                var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                await sw.WriteLineAsync($"{hex}  document-encryption-keys.json");
                await sw.FlushAsync();
            }
        }

        archiveStream.Position = 0;
        var fileName = $"document-encryption-keys-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        _logger.LogInformation("Finished encryption key backup. Bytes={Bytes}", archiveStream.Length);

        // Stream directly without ToArray to avoid extra allocation
        return File(archiveStream, "application/zip", fileName);
    }

    private sealed class DocumentEncryptionKeysBackupItem // retained for potential future use
    {
        public Guid StreamId { get; init; }
        public Guid EventId { get; init; }
        public long Sequence { get; init; }
        public DateTime Timestamp { get; init; }
        public DocumentEncryptionKeysAdded Event { get; init; } = default!;
    }
}
