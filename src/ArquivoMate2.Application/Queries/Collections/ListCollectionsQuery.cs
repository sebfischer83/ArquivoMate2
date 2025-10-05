using ArquivoMate2.Shared.Models.Collections;
using MediatR;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Queries.Collections;

public sealed record ListCollectionsQuery(string OwnerUserId) : IRequest<IReadOnlyCollection<CollectionDto>>;
