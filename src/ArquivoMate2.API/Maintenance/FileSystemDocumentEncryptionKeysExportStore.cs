using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ArquivoMate2.API.Maintenance;

public sealed class FileSystemDocumentEncryptionKeysExportStore : IDocumentEncryptionKeysExportStore
{
    private readonly string _rootPath;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileSystemDocumentEncryptionKeysExportStore(IHostEnvironment hostEnvironment)
    {
        _rootPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "maintenance", "document-encryption-keys");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<Guid> CreatePendingAsync(CancellationToken cancellationToken)
    {
        var metadata = new DocumentEncryptionKeysExportMetadata
        {
            OperationId = Guid.NewGuid(),
            State = MaintenanceExportState.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        await WriteMetadataAsync(metadata, cancellationToken);
        return metadata.OperationId;
    }

    public async Task<DocumentEncryptionKeysExportMetadata?> GetAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var path = GetMetadataPath(operationId);
        if (!File.Exists(path))
        {
            return null;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<DocumentEncryptionKeysExportMetadata>(stream, _serializerOptions, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task MarkRunningAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await UpdateMetadataAsync(operationId, metadata =>
        {
            metadata.State = MaintenanceExportState.Running;
        }, cancellationToken);
    }

    public async Task MarkCompletedAsync(Guid operationId, string archiveFileName, CancellationToken cancellationToken)
    {
        await UpdateMetadataAsync(operationId, metadata =>
        {
            metadata.State = MaintenanceExportState.Completed;
            metadata.ArchiveFileName = archiveFileName;
            metadata.CompletedUtc = DateTime.UtcNow;
            metadata.ErrorMessage = null;
        }, cancellationToken);
    }

    public async Task MarkFailedAsync(Guid operationId, string errorMessage, CancellationToken cancellationToken)
    {
        await UpdateMetadataAsync(operationId, metadata =>
        {
            metadata.State = MaintenanceExportState.Failed;
            metadata.CompletedUtc = DateTime.UtcNow;
            metadata.ErrorMessage = errorMessage;
        }, cancellationToken);
    }

    public string GetArchiveFilePath(Guid operationId, string archiveFileName)
    {
        var safeFileName = string.IsNullOrWhiteSpace(archiveFileName)
            ? $"{operationId}.zip"
            : archiveFileName;
        return Path.Combine(_rootPath, safeFileName);
    }

    public async Task<DocumentEncryptionKeysExportDownload?> TryGetDownloadAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var metadata = await GetAsync(operationId, cancellationToken);
        if (metadata is null || metadata.State != MaintenanceExportState.Completed || string.IsNullOrWhiteSpace(metadata.ArchiveFileName))
        {
            return null;
        }

        var filePath = GetArchiveFilePath(operationId, metadata.ArchiveFileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return new DocumentEncryptionKeysExportDownload(filePath, metadata.ArchiveFileName);
    }

    public async Task<int> DeleteOlderThanAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.Subtract(maxAge);
        var metadataFiles = Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly);
        var deleted = 0;

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            foreach (var metadataFile in metadataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DocumentEncryptionKeysExportMetadata? metadata;
                await using (var stream = File.OpenRead(metadataFile))
                {
                    metadata = await JsonSerializer.DeserializeAsync<DocumentEncryptionKeysExportMetadata>(stream, _serializerOptions, cancellationToken);
                }

                if (metadata is null)
                {
                    continue;
                }

                if (metadata.CreatedUtc >= threshold)
                {
                    continue;
                }

                var archivePath = metadata.ArchiveFileName is null
                    ? null
                    : GetArchiveFilePath(metadata.OperationId, metadata.ArchiveFileName);

                File.Delete(metadataFile);
                if (archivePath is not null && File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                deleted++;
            }
        }
        finally
        {
            _mutex.Release();
        }

        return deleted;
    }

    private string GetMetadataPath(Guid operationId)
        => Path.Combine(_rootPath, $"{operationId}.json");

    private async Task WriteMetadataAsync(DocumentEncryptionKeysExportMetadata metadata, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.Create(GetMetadataPath(metadata.OperationId));
            await JsonSerializer.SerializeAsync(stream, metadata, _serializerOptions, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task UpdateMetadataAsync(Guid operationId, Action<DocumentEncryptionKeysExportMetadata> apply, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var path = GetMetadataPath(operationId);
            DocumentEncryptionKeysExportMetadata metadata;
            if (File.Exists(path))
            {
                await using (var readStream = File.OpenRead(path))
                {
                    metadata = await JsonSerializer.DeserializeAsync<DocumentEncryptionKeysExportMetadata>(readStream, _serializerOptions, cancellationToken)
                               ?? new DocumentEncryptionKeysExportMetadata
                               {
                                   OperationId = operationId,
                                   CreatedUtc = DateTime.UtcNow
                               };
                }
            }
            else
            {
                metadata = new DocumentEncryptionKeysExportMetadata
                {
                    OperationId = operationId,
                    CreatedUtc = DateTime.UtcNow
                };
            }

            apply(metadata);

            await using var writeStream = File.Create(path);
            await JsonSerializer.SerializeAsync(writeStream, metadata, _serializerOptions, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
