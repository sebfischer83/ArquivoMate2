using System;
using MediatR;

namespace ArquivoMate2.Application.Commands.DocumentTypes
{
    public record DeleteDocumentTypeCommand(Guid Id, string UserId) : IRequest<bool>;
}
