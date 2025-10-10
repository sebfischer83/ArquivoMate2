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
    /// Retrieves and reserves files that are pending ingestion. Implementations
    /// should ensure that files returned from this call are not picked up by
    /// concurrent workers (e.g. by moving them into a processing area).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <summary>
/// Retrieves and reserves files that are pending ingestion so they are not concurrently processed by other workers.
/// </summary>
/// <returns>A read-only collection of IngestionFileDescriptor describing the reserved files.</returns>
    Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks a descriptor as successfully processed and moves it to the processed area.
    /// <summary>
/// Marks the specified reserved ingestion file as successfully processed and moves it to the provider's processed area.
/// </summary>
/// <param name="descriptor">Descriptor of the reserved ingestion file to mark as processed.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
    Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a descriptor as failed and moves it to the failure area. The optional
    /// reason can be persisted by the provider for debugging purposes.
    /// <summary>
/// Marks the specified ingestion file as failed and moves it to the provider's failure area.
/// </summary>
/// <param name="descriptor">Descriptor of the reserved ingestion file to mark as failed.</param>
/// <param name="reason">Optional short explanation for the failure that the provider may persist for debugging.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
    Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the binary content of the specified ingestion descriptor.
    /// </summary>
    /// <param name="descriptor">Descriptor that should be read.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <summary>
/// Reads the binary content of the specified ingestion file descriptor.
/// </summary>
/// <param name="descriptor">Descriptor identifying the reserved ingestion file to read.</param>
/// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
/// <returns>A byte array containing the file's binary content.</returns>
    Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken);

    /// <summary>
    /// Stores an uploaded file into the ingestion source for the specified user.
    /// </summary>
    /// <param name="userId">The identifier of the user that owns the file.</param>
    /// <param name="fileName">Original file name supplied by the caller.</param>
    /// <param name="content">File content stream.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
    /// <summary>
/// Stores an uploaded file in the ingestion source for the specified user and reserves it for ingestion.
/// </summary>
/// <param name="userId">Identifier of the user who owns the uploaded file.</param>
/// <param name="fileName">Original name of the uploaded file.</param>
/// <param name="content">Stream containing the file data to store.</param>
/// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
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