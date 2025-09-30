using System;
using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record DeleteDocumentShareCommand(Guid DocumentId, string OwnerUserId, Guid ShareId) : IRequest<bool>;
