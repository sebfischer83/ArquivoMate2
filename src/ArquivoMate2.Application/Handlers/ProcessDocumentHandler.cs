using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using Marten;
using MediatR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ArquivoMate2.Application.Handlers
{
    public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<ProcessDocumentHandler> _logger;
        private readonly IDocumentTextExtractor _documentTextExtractor;
        private readonly IFileMetadataService fileMetadataService;
        private readonly IPathService pathService;
        private readonly IStorageProvider _storage;

        public ProcessDocumentHandler(IDocumentSession session, ILogger<ProcessDocumentHandler> logger, IDocumentTextExtractor documentTextExtractor, IFileMetadataService fileMetadataService, IPathService pathService,
            IStorageProvider storage)
            => (_session, _logger, _documentTextExtractor, this.fileMetadataService, this.pathService, _storage) = (session, logger, documentTextExtractor, fileMetadataService, pathService, storage);

        async Task IRequestHandler<ProcessDocumentCommand>.Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
                if (doc is null)
                {
                    _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");
                }

                // read the file
                var metadata = await fileMetadataService.ReadMetadataAsync(request.DocumentId, request.UserId);

                if (metadata is null)
                {
                    _logger.LogWarning("Metadata for document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Metadata for document {request.DocumentId} not found");
                }

                var path = pathService.GetDocumentUploadPath(request.UserId);
                path = Path.Combine(path, $"{request.DocumentId}{metadata.Extension}");

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

                // get the content
                var content = await ExtractTextAsync(stream, metadata, cancellationToken);

                _session.Events.Append(request.DocumentId, new DocumentContentExtracted(request.DocumentId, content,  DateTime.UtcNow));

                await _storage.SaveFile(request.UserId, request.DocumentId, Path.GetFileName(path), File.ReadAllBytes(path));

                doc.MarkAsProcessed();
                _session.Events.Append(request.DocumentId, new DocumentProcessed(request.DocumentId, DateTime.UtcNow));
                await _session.SaveChangesAsync(cancellationToken);

                sw.Stop();
                _logger.LogInformation("Processed document {DocumentId} in {ElapsedMs}ms", request.DocumentId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
                throw;
            }
        }

        private async Task<string> ExtractTextAsync(FileStream stream, DocumentMetadata metadata, CancellationToken cancellationToken)
        {
            // prüfen nach Dateiendung
            switch (metadata.Extension.ToLowerInvariant())
            {
                case ".pdf":
                    // PDF Text extrahieren
                    return await _documentTextExtractor.ExtractPdfTextAsync(stream, metadata, false, cancellationToken);
                default:
                    _logger.LogError("Unsupported file type: {Extension}", metadata.Extension);
                    return string.Empty;
                   
            }
        }
    }
}
