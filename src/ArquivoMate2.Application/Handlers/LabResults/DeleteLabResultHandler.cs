using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.LabResults;
using MediatR;
using Marten;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class DeleteLabResultHandler : IRequestHandler<DeleteLabResultCommand, bool>
    {
        private readonly IDocumentSession _session;

        public DeleteLabResultHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<bool> Handle(DeleteLabResultCommand request, CancellationToken cancellationToken)
        {
            var existing = await _session.LoadAsync<LabResult>(request.Id, cancellationToken);
            if (existing == null) return false;

            _session.Delete(existing);
            await _session.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
