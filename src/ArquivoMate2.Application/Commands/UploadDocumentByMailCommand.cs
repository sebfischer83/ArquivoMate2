using ArquivoMate2.Application.Models;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record UploadDocumentByMailCommand(string UserId, EmailDocument EmailDocument) : IRequest<Guid>;
}
