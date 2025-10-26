using MediatR;
using ArquivoMate2.Shared.Models;
using System;

namespace ArquivoMate2.Application.Queries.Features
{
    public sealed record GetDocumentFeatureProcessingQuery(Guid DocumentId, string FeatureKey) : IRequest<DocumentFeatureProcessingDto?>;
}
