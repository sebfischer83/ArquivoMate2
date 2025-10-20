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
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Meilisearch;
using System.Collections.Generic;
using System.Reflection;
using Npgsql;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Route("api/maintenance")]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
public class MaintenanceController : ControllerBase
{
    private readonly IQuerySession _querySession;
    private readonly ILogger<MaintenanceController> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly MeilisearchClient _meilisearchClient;
    private readonly IConfiguration _configuration;

    public MaintenanceController(IQuerySession querySession, ILogger<MaintenanceController> logger, IConnectionMultiplexer redis, MeilisearchClient meilisearchClient, IConfiguration configuration)
    {
        _querySession = querySession;
        _logger = logger;
        _redis = redis;
        _meilisearchClient = meilisearchClient;
        _configuration = configuration;
    }

    private sealed class TableSizeInfo
    {
        public string TableName { get; init; } = string.Empty;
        public string TotalSize { get; init; } = string.Empty;
        public long TotalSizeBytes { get; init; }
        public string TableSize { get; init; } = string.Empty;
        public string IndexSize { get; init; } = string.Empty;
        public long ApproxRows { get; init; }
    }

    private sealed record DatabaseStatsDto
    {
        public string PgVersion { get; init; } = string.Empty;
        public List<TableSizeInfo> TopTables { get; init; } = new();
        public List<TableSizeInfo> MartenTables { get; init; } = new();
        public string MartenVersion { get; init; } = string.Empty;
    }

    /// <summary>
    /// Creates a ZIP archive containing all DocumentEncryptionKeysAdded events for backup purposes.
    /// Adds a metadata wrapper and a SHA256 hash file for integrity verification.
    /// </summary>
    /// <param name="fromSequence">Optional lower sequence bound (inclusive).</param>
    /// <param name="toSequence">Optional upper sequence bound (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
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

    /// <summary>
    /// Returns a count of keys grouped by prefix (text before the first ':') across all configured Redis servers.
    /// Intended for maintenance / monitoring use. This may be an expensive operation on large keyspaces.
    /// </summary>
    [HttpGet("cache/key-counts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCacheKeyCounts(CancellationToken cancellationToken)
    {
        var counts = await GetCacheKeyCountsInternal(cancellationToken);
        return Ok(counts);
    }

    // Internal helper used by GetCacheKeyCounts and infra-stats endpoint
    private async Task<Dictionary<string, long>> GetCacheKeyCountsInternal(CancellationToken cancellationToken)
    {
        var prefixes = new[] { "thumb", "meta", "preview", "archive", "file", "enc", "s3delivery", "bunnyDelivery" };
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var endpoints = _redis.GetEndPoints();
        foreach (var endpoint in endpoints)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var server = _redis.GetServer(endpoint);

            foreach (var prefix in prefixes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                long prefixCount = 0;
                var patterns = new[] { $"{prefix}:*", $"redis:{prefix}:*" };

                foreach (var pattern in patterns)
                {
                    try
                    {
                        await foreach (var _ in server.KeysAsync(pattern: pattern, pageSize: 1000).WithCancellation(cancellationToken))
                        {
                            prefixCount++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error scanning keys on endpoint {Endpoint} for pattern {Pattern}", endpoint, pattern);
                    }
                }

                if (prefixCount > 0)
                {
                    if (!counts.TryGetValue(prefix, out var cur)) counts[prefix] = 0;
                    counts[prefix] = counts[prefix] + prefixCount;
                }
            }
        }

        return counts;
    }

    /// <summary>
    /// Returns combined infra stats: Redis INFO, key counts per prefix and Meilisearch health + index stats.
    /// </summary>
    [HttpGet("infra-stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInfraStats(CancellationToken cancellationToken)
    {
        // Redis INFO (structured)
        object redisInfoStructured = new { };
        try
        {
            var db = _redis.GetDatabase();
            var infoResult = await db.ExecuteAsync("INFO");
            var infoText = infoResult?.ToString() ?? string.Empty;
            redisInfoStructured = ArquivoMate2.API.Utilities.RedisInfoParser.Parse(infoText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Redis INFO");
            redisInfoStructured = new { error = ex.Message };
        }

        // Key counts per prefix (reuse existing logic)
        var keyCountsResult = await GetCacheKeyCountsInternal(cancellationToken);

        // Meilisearch health + index stats
        object? meiliHealth = null;
        object? meiliIndexStats = null;
        object? meiliVersion = null;
        try
        {
            meiliHealth = await _meilisearchClient.HealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Meilisearch health");
            meiliHealth = new { error = ex.Message };
        }

        try
        {
            // Use the Meilisearch client to get aggregate stats for the instance
            var stats = await _meilisearchClient.GetStatsAsync();
            meiliIndexStats = stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Meilisearch stats");
            meiliIndexStats = new { error = ex.Message };
        }

        // Retrieve Meilisearch version using client API
        try
        {
            meiliVersion = await _meilisearchClient.GetVersionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to retrieve Meilisearch version");
            meiliVersion = new { error = ex.Message };
        }

        // Database stats (Postgres + Marten)
        DatabaseStatsDto? dbStats = null;
        try
        {
            dbStats = await GetDatabaseStatsInternal(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve database stats for infra-stats");
            dbStats = new DatabaseStatsDto { PgVersion = string.Empty, TopTables = new List<TableSizeInfo>(), MartenTables = new List<TableSizeInfo>(), MartenVersion = string.Empty };
        }

        return Ok(new { RedisInfo = redisInfoStructured, KeyCounts = keyCountsResult, MeiliHealth = meiliHealth, MeiliIndexStats = meiliIndexStats, MeiliVersion = meiliVersion, Database = dbStats });
    }

    private async Task<DatabaseStatsDto> GetDatabaseStatsInternal(CancellationToken cancellationToken)
    {
        var connStr = _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Database connection string not configured.");

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(cancellationToken);

        var result = new DatabaseStatsDto();

        // Postgres version
        await using (var cmd = new NpgsqlCommand("SELECT version();", conn))
        {
            cmd.CommandTimeout = 10;
            var ver = await cmd.ExecuteScalarAsync(cancellationToken);
            result = result with { PgVersion = ver?.ToString() ?? string.Empty };
        }

        // Top tables
        var topTables = new List<TableSizeInfo>();
        var topTablesSql = @"
SELECT nspname || '.' || c.relname AS table_name,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
       pg_total_relation_size(c.oid) AS total_size_bytes,
       pg_size_pretty(pg_relation_size(c.oid)) AS table_size,
       pg_size_pretty(pg_total_relation_size(c.oid) - pg_relation_size(c.oid)) AS index_size,
       coalesce(s.n_live_tup, 0) AS approx_rows
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
WHERE c.relkind = 'r'
ORDER BY pg_total_relation_size(c.oid) DESC
LIMIT 20;";

        await using (var cmd = new NpgsqlCommand(topTablesSql, conn))
        {
            cmd.CommandTimeout = 30;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                topTables.Add(new TableSizeInfo
                {
                    TableName = reader.GetString(0),
                    TotalSize = reader.GetString(1),
                    TotalSizeBytes = reader.GetInt64(2),
                    TableSize = reader.GetString(3),
                    IndexSize = reader.GetString(4),
                    ApproxRows = reader.GetInt64(5)
                });
            }
        }

        // Marten tables
        var martenTables = new List<TableSizeInfo>();
        var martenSql = @"
SELECT nspname || '.' || c.relname AS table_name,
       pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
       pg_total_relation_size(c.oid) AS total_size_bytes,
       pg_size_pretty(pg_relation_size(c.oid)) AS table_size,
       pg_size_pretty(pg_total_relation_size(c.oid) - pg_relation_size(c.oid)) AS index_size,
       coalesce(s.n_live_tup,0) AS approx_rows
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
WHERE c.relkind = 'r'
  AND (c.relname LIKE 'mt_%' OR c.relname = 'mt_events' OR c.relname = 'mt_streams')
ORDER BY pg_total_relation_size(c.oid) DESC;";

        await using (var cmd = new NpgsqlCommand(martenSql, conn))
        {
            cmd.CommandTimeout = 30;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                martenTables.Add(new TableSizeInfo
                {
                    TableName = reader.GetString(0),
                    TotalSize = reader.GetString(1),
                    TotalSizeBytes = reader.GetInt64(2),
                    TableSize = reader.GetString(3),
                    IndexSize = reader.GetString(4),
                    ApproxRows = reader.GetInt64(5)
                });
            }
        }

        // Marten assembly version
        string martenVersion = string.Empty;
        try
        {
            var asm = typeof(IDocumentStore).Assembly;
            martenVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? asm.GetName(). Version?.ToString() ?? string.Empty;
        }
        catch { }

        // Create final DTO
        var final = new DatabaseStatsDto
        {
            PgVersion = result.PgVersion,
            TopTables = topTables,
            MartenTables = martenTables,
            MartenVersion = martenVersion
        };

        return final;
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
