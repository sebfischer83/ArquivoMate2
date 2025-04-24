using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using JasperFx.CodeGeneration.Frames;
using Marten;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers
{
    public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Guid>
    {
        private readonly IDocumentSession _session;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPathService _pathService;
        private readonly OcrSettings _ocrSettings;

        public UploadDocumentHandler(IDocumentSession session, IFileMetadataService fileMetadataService, ICurrentUserService currentUserService, IPathService pathService, OcrSettings ocrSettings)
        {
            _session = session;
            _fileMetadataService = fileMetadataService;
            _currentUserService = currentUserService;
            _pathService = pathService;
            _ocrSettings = ocrSettings;
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

            var @event = new DocumentUploaded(fileId, _currentUserService.UserId, filePath, DateTime.UtcNow);
            _session.Events.StartStream<Document>(@event.AggregateId, @event);
            await _session.SaveChangesAsync(cancellationToken);

            var metadata = new DocumentMetadata(
                fileId,
                _currentUserService.UserId,
                request.request.File.FileName,
                request.request.File.ContentType!,
                ext,
                request.request.File.Length,
                DateTime.UtcNow,
                (string.IsNullOrWhiteSpace(request.request.Language) ? _ocrSettings.DefaultLanguage : request.request.Language)
            );
            await _fileMetadataService.WriteMetadataAsync(metadata, cancellationToken);

            return @event.AggregateId;
        }
    }
}
