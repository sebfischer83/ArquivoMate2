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
    public class UploadDocumentByMailHandler : IRequestHandler<UploadDocumentByMailCommand, Guid>
    {
        private readonly IDocumentSession _session;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly IPathService _pathService;
        private readonly OcrSettings _ocrSettings;

        public UploadDocumentByMailHandler(IDocumentSession session, IFileMetadataService fileMetadataService, ICurrentUserService currentUserService, IPathService pathService, OcrSettings ocrSettings)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
        }

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

            // Berechnung des Hashes der Datei
            string fileHash;
            using (var hashAlgorithm = System.Security.Cryptography.SHA256.Create())
            {
                fs.Position = 0; // Zurücksetzen des Streams
                var hashBytes = hashAlgorithm.ComputeHash(fs);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var @event = new DocumentUploaded(fileId, request.UserId, fileHash, DateTime.UtcNow);

            _session.Events.StartStream<Document>(@event.AggregateId, @event);
            await _session.SaveChangesAsync(cancellationToken);

            var languages = _ocrSettings.DefaultLanguages;

            var mime = MimeTypesMap.GetMimeType(request.EmailDocument.FileName);

            var metadata = new DocumentMetadata(
                fileId,
                request.UserId,
                request.EmailDocument.FileName,
                mime,
                ext,
                request.EmailDocument.File.Length,
                DateTime.UtcNow,
                languages,
                fileHash
            );

            await _fileMetadataService.WriteMetadataAsync(metadata, cancellationToken);

            return @event.AggregateId;
        }
    }
}
