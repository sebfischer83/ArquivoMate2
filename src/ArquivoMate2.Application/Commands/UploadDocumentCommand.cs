using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record UploadDocumentCommand(UploadDocumentRequest request) : IRequest<Guid>;
}
