using MediatR;
using System;

namespace ArquivoMate2.Application.Commands.Collections;

public sealed record RemoveDocumentFromCollectionCommand(Guid CollectionId, Guid DocumentId, string OwnerUserId) : IRequest<bool>;
