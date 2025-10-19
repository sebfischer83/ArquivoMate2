using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using HeyRed.Mime;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers
{
    /// <summary>
    /// Handles the ingestion of email attachments into the document system.
    /// </summary>
    public class UploadDocumentByMailHandler : IRequestHandler<UploadDocumentByMailCommand, Guid>
    {
        private readonly IDocumentSession _session;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly IPathService _pathService;
        private readonly OcrSettings _ocrSettings;
        private readonly ILogger<UploadDocumentByMailHandler> _logger;
        private readonly IAutoShareService _autoShareService;
        private readonly IEncryptionService _encryptionService;

        /// <summary>
        /// Initializes a new <see cref="UploadDocumentByMailHandler"/> instance.
        /// </summary>
        /// <param name="session">Document session used to append events.</param>
        /// <param name="fileMetadataService">Service that persists metadata alongside the file.</param>
        /// <param name="currentUserService">Unused dependency kept for backwards compatibility (resolved by container).</param>
        /// <param name="pathService">Service that resolves the storage path for the uploaded file.</param>
        /// <param name="ocrSettings">OCR configuration for metadata defaults.</param>
        /// <param name="logger">Logger for tracing and debugging.</param>
        /// <param name="autoShareService">Service that applies automatic sharing rules.</param>
        /// <param name="encryptionService">Service that indicates whether encryption is enabled.</param>
        public UploadDocumentByMailHandler(
            IDocumentSession session, 
            IFileMetadataService fileMetadataService, 
            ICurrentUserService currentUserService, 
            IPathService pathService, 
            OcrSettings ocrSettings, 
            ILogger<UploadDocumentByMailHandler> logger,
            IAutoShareService autoShareService,
            IEncryptionService encryptionService)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
            _logger = logger;
            _autoShareService = autoShareService;
            _encryptionService = encryptionService;
        }

        /// <summary>
        /// Persists an email attachment, emits domain events, and stores metadata for further processing.
        /// </summary>
        /// <param name="request">Command containing the email attachment details.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>The identifier of the created document.</returns>
        public async Task<Guid> Handle(UploadDocumentByMailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting upload by mail for user {UserId}, file {FileName}", request.UserId, request.EmailDocument.FileName);

            var userFolder = _pathService.GetDocumentUploadPath(request.UserId);
            Directory.CreateDirectory(userFolder);

            var fileId = Guid.NewGuid();
            var ext = Path.GetExtension(request.EmailDocument.FileName);
            var fileName = fileId + ext;
            var filePath = Path.Combine(userFolder, fileName);

            await using var fs = new FileStream(filePath, FileMode.Create);
            await fs.WriteAsync(request.EmailDocument.File, 0, request.EmailDocument.File.Length, cancellationToken);

            // Calculate the file hash for deduplication and integrity checks
            string fileHash;
            using (var hashAlgorithm = System.Security.Cryptography.SHA256.Create())
            {
                fs.Position = 0; // Reset the stream before hashing
                var hashBytes = hashAlgorithm.ComputeHash(fs);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            _logger.LogInformation("Created file {FilePath} with hash {FileHash}", filePath, fileHash);

            var uploaded = new DocumentUploaded(fileId, request.UserId, fileHash, DateTime.UtcNow);
            _session.Events.StartStream<Document>(uploaded.AggregateId, uploaded);

            // Add encryption event if encryption is enabled (matching UploadDocumentHandler)
            if (_encryptionService.IsEnabled)
            {
                _session.Events.Append(fileId, new DocumentEncryptionEnabled(fileId, DateTime.UtcNow));
                _logger.LogInformation("Added DocumentEncryptionEnabled event for document {DocumentId}", fileId);
            }

            // Initialize a default title from the file name (matching UploadDocumentHandler)
            var defaultTitle = TitleNormalizer.FromFileName(request.EmailDocument.FileName);
            _session.Events.Append(fileId, new DocumentTitleInitialized(fileId, defaultTitle, DateTime.UtcNow));

            _logger.LogInformation("Attempting SaveChangesAsync for document {DocumentId}", fileId);

            try
            {
                await _session.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("SaveChangesAsync succeeded for document {DocumentId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed saving document events for user {UserId} file {FileName}", request.UserId, request.EmailDocument.FileName);
                throw;
            }

            _logger.LogInformation("Created document {DocumentId} for user {UserId} from ingestion. File: {FilePath}", uploaded.AggregateId, request.UserId, filePath);

            // Apply automatic sharing rules (matching UploadDocumentHandler)
            try
            {
                await _autoShareService.ApplyRulesAsync(fileId, request.UserId, cancellationToken);
                _logger.LogInformation("Applied auto-share rules for document {DocumentId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed applying auto-share rules for document {DocumentId}", fileId);
                // Continue - auto-share failures should not prevent processing
            }

            var mime = MimeTypesMap.GetMimeType(request.EmailDocument.FileName);

            var metadata = new DocumentMetadata(
                fileId,
                request.UserId,
                request.EmailDocument.FileName,
                mime,
                ext,
                request.EmailDocument.File.Length,
                DateTime.UtcNow,
                _ocrSettings.DefaultLanguages,
                fileHash
            );

            try
            {
                await _fileMetadataService.WriteMetadataAsync(metadata, cancellationToken);
                _logger.LogInformation("Wrote metadata for document {DocumentId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed writing metadata for document {DocumentId} user {UserId}", fileId, request.UserId);
                // continue - metadata failures should not prevent processing, but logged for diagnostics
            }

            _logger.LogInformation("Completed upload by mail for document {DocumentId}", uploaded.AggregateId);
            return uploaded.AggregateId;
        }
    }
}
