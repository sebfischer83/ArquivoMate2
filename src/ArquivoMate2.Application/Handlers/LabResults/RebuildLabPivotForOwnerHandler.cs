using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.LabResults;
using MediatR;
using Marten;
using ArquivoMate2.Application.Features.Processors.LabResults.Services;
using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class RebuildLabPivotForOwnerHandler : IRequestHandler<RebuildLabPivotForOwnerCommand, Unit>
    {
        private readonly IDocumentStore _store;
        private readonly ILabPivotUpdater _pivotUpdater;
        private readonly IParameterNormalizer _normalizer;

        public RebuildLabPivotForOwnerHandler(IDocumentStore store, ILabPivotUpdater pivotUpdater, IParameterNormalizer normalizer)
        {
            _store = store;
            _pivotUpdater = pivotUpdater;
            _normalizer = normalizer;
        }

        public async Task<Unit> Handle(RebuildLabPivotForOwnerCommand request, CancellationToken cancellationToken)
        {
            await _pivotUpdater.RebuildForOwnerAsync(_store, request.OwnerId, _normalizer, cancellationToken);
            return Unit.Value;
        }
    }
}
