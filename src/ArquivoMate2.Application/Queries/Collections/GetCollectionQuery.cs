using ArquivoMate2.Shared.Models.Collections;
using MediatR;
using System;

namespace ArquivoMate2.Application.Queries.Collections;

public sealed record GetCollectionQuery(Guid CollectionId, string OwnerUserId) : IRequest<CollectionDto?>;
