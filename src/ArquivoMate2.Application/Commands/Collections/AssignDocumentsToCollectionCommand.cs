using MediatR;
using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Commands.Collections;

public sealed record AssignDocumentsToCollectionCommand(Guid CollectionId, string OwnerUserId, IReadOnlyCollection<Guid> DocumentIds) : IRequest<int>;
