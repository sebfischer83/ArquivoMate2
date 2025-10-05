using ArquivoMate2.Shared.Models.Collections;
using MediatR;
using System;

namespace ArquivoMate2.Application.Commands.Collections;

public sealed record UpdateCollectionCommand(Guid CollectionId, string OwnerUserId, string Name) : IRequest<CollectionDto?>;
