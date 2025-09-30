using System;
using System.Collections.Generic;
using ArquivoMate2.Shared.Models.Sharing;
using MediatR;

namespace ArquivoMate2.Application.Queries.Sharing;

public record GetDocumentSharesQuery(Guid DocumentId, string OwnerUserId) : IRequest<IReadOnlyCollection<DocumentShareDto>>;
