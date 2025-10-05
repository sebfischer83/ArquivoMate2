using ArquivoMate2.Shared.Models.Collections;
using MediatR;

namespace ArquivoMate2.Application.Commands.Collections;

public sealed record CreateCollectionCommand(string OwnerUserId, string Name) : IRequest<CollectionDto>;
