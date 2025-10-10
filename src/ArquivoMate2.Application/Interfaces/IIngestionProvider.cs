using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces;

/// <summary>
/// Provides access to ingestion sources that deliver files into the system.
/// Implementations are responsible for managing the lifecycle of the files
/// (pickup, processed, failed) for a specific ingestion backend such as the
/// filesystem.
/// </summary>
public interface IIngestionProvider
{
    /// <summary>
    /// Optional email address used when creating an EmailDocument for ingestion sources.
    /// If null, the source does not provide a sender address.
    /// </summary>
    string? IngestionEmailAddress { get; }

    /// <summary>
    /// Retrieves and reserves files that are pending ingestion. Implementations
    /// should ensure that files returned from this call are not picked up by
    /// concurrent workers (e.g. by moving them into a processing area).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <returns>A read-only collection of descriptors that describe the reserved files.</returns>
    Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks a descriptor as successfully processed and moves it to the processed area.
    /// </summary>
    Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a descriptor as failed and moves it to the failure area. The optional
    /// reason can be persisted by the provider for debugging purposes.
    /// </summary>
    Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the binary content of the specified ingestion descriptor.
    /// </summary>
    /// <param name="descriptor">Descriptor that should be read.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <returns>The raw binary content of the descriptor.</returns>
    Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken);

    /// <summary>
    /// Stores an uploaded file into the ingestion source for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user that owns the file.</param>
    /// <param name="fileName">Original file name supplied by the caller.</param>
    /// <param name="content">File content stream.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <returns>The absolute path of the stored file.</returns>
    Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken);
}

/// <summary>
/// Describes a file that has been reserved for ingestion.
/// </summary>
/// <param name="UserId">Owner of the file.</param>
/// <param name="FileName">The original file name (without any provider specific suffixes).</param>
/// <param name="FullPath">The provider specific path or identifier used to access the file.</param>
public record IngestionFileDescriptor(string UserId, string FileName, string FullPath);
