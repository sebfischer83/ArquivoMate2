using System;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;

namespace ArquivoMate2.Application.Commands.Sharing;

public record CreateDocumentShareCommand(Guid DocumentId, string OwnerUserId, ShareTarget Target, DocumentPermissions Permissions) : IRequest<DocumentShareDto>;
