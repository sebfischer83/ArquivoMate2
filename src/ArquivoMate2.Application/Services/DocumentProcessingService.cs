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
            await _mediator.Send(new ProcessDocumentCommand(documentId, userId));
            // Post-processing actions (notifications, indexing)

            // Delete the temp uploaded file after processing
        }
    }
}
