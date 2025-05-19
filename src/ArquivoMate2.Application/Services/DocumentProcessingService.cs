using ArquivoMate2.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Services
{
    public class DocumentProcessingService
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(IMediator mediator, Microsoft.Extensions.Logging.ILogger<DocumentProcessingService> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid documentId, string userId)
        {
            _logger.LogInformation("Starting document processing for Document ID: {DocumentId}, User ID: {UserId}", documentId, userId);
            var doc = await _mediator.Send(new ProcessDocumentCommand(documentId, userId));
            // Post-processing actions (notifications, indexing)

            if (doc.Document == null)
            {
                // Optional: Fehlerbehandlung oder Logging
            }

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
