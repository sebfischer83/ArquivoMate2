using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record HideAllFromImportHistoryByStatusCommand(DocumentProcessingStatus DocumentProcessingStatus, string UserId) : IRequest<bool>;
}
