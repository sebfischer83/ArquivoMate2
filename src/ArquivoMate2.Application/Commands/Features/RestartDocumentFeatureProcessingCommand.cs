using MediatR;
using System;

namespace ArquivoMate2.Application.Commands.Features
{
    public sealed record RestartDocumentFeatureProcessingCommand(Guid DocumentId, string FeatureKey) : IRequest<bool>;
}
