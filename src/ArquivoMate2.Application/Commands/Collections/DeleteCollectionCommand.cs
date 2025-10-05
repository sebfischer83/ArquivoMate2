using MediatR;
using System;

namespace ArquivoMate2.Application.Commands.Collections;

public sealed record DeleteCollectionCommand(Guid CollectionId, string OwnerUserId) : IRequest<bool>;
