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
    public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Guid>
    {
        private readonly IDocumentSession _session;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPathService _pathService;
        private readonly OcrSettings _ocrSettings;
        private readonly IAutoShareService _autoShareService;
        private readonly IEncryptionService _encryptionService;

        public UploadDocumentHandler(IDocumentSession session, IFileMetadataService fileMetadataService, ICurrentUserService currentUserService, IPathService pathService, OcrSettings ocrSettings, IAutoShareService autoShareService, IEncryptionService encryptionService)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _currentUserService = currentUserService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
            _autoShareService = autoShareService;
            _encryptionService = encryptionService;
        }

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

            // Berechnung des Hashes der Datei
            string fileHash;
            using (var hashAlgorithm = System.Security.Cryptography.SHA256.Create())
            {
                fs.Position = 0; // Zurücksetzen des Streams
                var hashBytes = hashAlgorithm.ComputeHash(fs);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var uploaded = new DocumentUploaded(fileId, _currentUserService.UserId, fileHash, DateTime.UtcNow);
            _session.Events.StartStream<Document>(uploaded.AggregateId, uploaded);

            if (_encryptionService.IsEnabled)
            {
                _session.Events.Append(fileId, new DocumentEncryptionEnabled(fileId, DateTime.UtcNow));
            }

            // Default Titel
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
