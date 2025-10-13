using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.ValueObjects;
using HeyRed.Mime;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers
{
    /// <summary>
    /// Handles document uploads by persisting files, emitting domain events, and storing metadata.
    /// </summary>
    public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Guid>
    {
        private readonly IDocumentSession _session;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPathService _pathService;
        private readonly OcrSettings _ocrSettings;
        private readonly IAutoShareService _autoShareService;
        private readonly ICustomEncryptionService _encryptionService;

        /// <summary>
        /// Initializes a new <see cref="UploadDocumentHandler"/> with the dependencies required for file persistence and event handling.
        /// </summary>
        /// <param name="session">Document session used to append events.</param>
        /// <param name="fileMetadataService">Service used to persist metadata to storage.</param>
        /// <param name="currentUserService">Provides the current user's identity.</param>
        /// <param name="pathService">Resolves the physical storage path for uploads.</param>
        /// <param name="ocrSettings">OCR settings used for metadata defaults.</param>
        /// <param name="autoShareService">Service that applies automatic sharing rules.</param>
        /// <param name="encryptionService">Service that indicates whether encryption is enabled.</param>
        public UploadDocumentHandler(IDocumentSession session, IFileMetadataService fileMetadataService, ICurrentUserService currentUserService, IPathService pathService, OcrSettings ocrSettings, IAutoShareService autoShareService, ICustomEncryptionService encryptionService)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _currentUserService = currentUserService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
            _autoShareService = autoShareService;
            _encryptionService = encryptionService;
        }

        /// <summary>
        /// Persists the uploaded file, emits the domain events required to initialize the document, and stores metadata for later processing.
        /// </summary>
        /// <param name="request">Command carrying the uploaded file.</param>
        /// <param name="cancellationToken">Cancellation token propagated from the caller.</param>
        /// <returns>The identifier of the created document.</returns>
        public async Task<Guid> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
        {
            var userFolder = _pathService.GetDocumentUploadPath(_currentUserService.UserId);
            Directory.CreateDirectory(userFolder);

            var fileId = Guid.NewGuid();
            var ext = Path.GetExtension(request.request.File.FileName);
            var fileName = fileId + ext;
            var filePath = Path.Combine(userFolder, fileName);

            await using var fs = new FileStream(filePath, FileMode.Create);
            await request.request.File.CopyToAsync(fs, cancellationToken);

            // Calculate the file hash for deduplication and integrity checks
            string fileHash;
            using (var hashAlgorithm = System.Security.Cryptography.SHA256.Create())
            {
                fs.Position = 0; // Reset the stream before hashing
                var hashBytes = hashAlgorithm.ComputeHash(fs);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var uploaded = new DocumentUploaded(fileId, _currentUserService.UserId, fileHash, DateTime.UtcNow);
            _session.Events.StartStream<Document>(uploaded.AggregateId, uploaded);

            if (_encryptionService.IsEnabled)
            {
                _session.Events.Append(fileId, new DocumentEncryptionEnabled(fileId, DateTime.UtcNow));
            }

            // Initialize a default title from the file name
            var defaultTitle = TitleNormalizer.FromFileName(request.request.File.FileName);
            _session.Events.Append(fileId, new DocumentTitleInitialized(fileId, defaultTitle, DateTime.UtcNow));

            await _session.SaveChangesAsync(cancellationToken);

            await _autoShareService.ApplyRulesAsync(fileId, _currentUserService.UserId, cancellationToken);

            var metadata = new DocumentMetadata(
                fileId,
                _currentUserService.UserId,
                request.request.File.FileName,
                request.request.File.ContentType!,
                ext,
                request.request.File.Length,
                DateTime.UtcNow,
                _ocrSettings.DefaultLanguages, // always default, detection later
                fileHash
            );
            await _fileMetadataService.WriteMetadataAsync(metadata, cancellationToken);

            return uploaded.AggregateId;
        }
    }
}
