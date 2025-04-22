using ArquivoMate2.Application.Commands;
using MediatR;

namespace ArquivoMate2.Application.Services
{
    public class DocumentProcessingService
    {
        private readonly IMediator _mediator;
        public DocumentProcessingService(IMediator mediator) => _mediator = mediator;

        public async Task ProcessAsync(Guid documentId)
        {
            // Pre-processing validations, logging, etc.
            await _mediator.Send(new ProcessDocumentCommand(documentId));
            // Post-processing actions (notifications, indexing)
        }
    }
}
