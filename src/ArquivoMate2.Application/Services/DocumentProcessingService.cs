using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Services
{
    public class DocumentProcessingService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly ISearchClient _searchClient;

        public DocumentProcessingService(IMediator mediator, ILogger<DocumentProcessingService> logger, IDocumentSession documentSession, ISearchClient searchClient)
        {
            _mediator = mediator;
            _logger = logger;
            _searchClient = searchClient;
        }

        public async Task ProcessAsync(Guid documentId, Guid importProcessId, string userId)
        {
            _logger.LogInformation("Starting document processing for Document ID: {DocumentId}, User ID: {UserId}", documentId, userId);
            var doc = await _mediator.Send(new ProcessDocumentCommand(documentId, importProcessId, userId));

            if (doc.Document == null)
            {
                _logger.LogError("Document processing returned null for Document ID: {DocumentId}, User ID: {UserId}", documentId, userId);
            }
            else
            {
                await _searchClient.AddDocument(doc.Document!);
            }
            _logger.LogInformation("End document processing for Document ID: {DocumentId}, User ID: {UserId}", documentId, userId);

            if (!string.IsNullOrWhiteSpace(doc.TempFilePath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(doc.TempFilePath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(doc.TempFilePath);

                    if (directory != null && fileNameWithoutExt != null)
                    {
                        var files = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*");
                        foreach (var file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                _logger.LogInformation("Deleted temp file: {FilePath}", file);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete temp file: {FilePath}", file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while deleting temp files for: {TempFilePath}", doc.TempFilePath);
                }
            }
        }
    }
}
