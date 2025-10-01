using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using HeyRed.Mime;
using Marten;
using MediatR;
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

        /// <summary>
        /// Initializes a new <see cref="UploadDocumentByMailHandler"/> instance.
        /// </summary>
        /// <param name="session">Document session used to append events.</param>
        /// <param name="fileMetadataService">Service that persists metadata alongside the file.</param>
        /// <param name="currentUserService">Unused dependency kept for backwards compatibility (resolved by container).</param>
        /// <param name="pathService">Service that resolves the storage path for the uploaded file.</param>
        /// <param name="ocrSettings">OCR configuration for metadata defaults.</param>
        public UploadDocumentByMailHandler(IDocumentSession session, IFileMetadataService fileMetadataService, ICurrentUserService currentUserService, IPathService pathService, OcrSettings ocrSettings)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
        }

        /// <summary>
        /// Persists an email attachment, emits domain events, and stores metadata for further processing.
        /// </summary>
        /// <param name="request">Command containing the email attachment details.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>The identifier of the created document.</returns>
        public async Task<Guid> Handle(UploadDocumentByMailCommand request, CancellationToken cancellationToken)
        {
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

            var uploaded = new DocumentUploaded(fileId, request.UserId, fileHash, DateTime.UtcNow);
            _session.Events.StartStream<Document>(uploaded.AggregateId, uploaded);

            var defaultTitle = TitleNormalizer.FromFileName(request.EmailDocument.FileName);
            _session.Events.Append(fileId, new DocumentTitleInitialized(fileId, defaultTitle, DateTime.UtcNow));

            await _session.SaveChangesAsync(cancellationToken);

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

            await _fileMetadataService.WriteMetadataAsync(metadata, cancellationToken);

            return uploaded.AggregateId;
        }
    }
}
