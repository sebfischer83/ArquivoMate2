using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.DocumentTypes;
using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.DocumentTypes
{
    public class DeleteDocumentTypeHandler : IRequestHandler<DeleteDocumentTypeCommand, bool>
    {
        private readonly IDocumentSession _session;

        public DeleteDocumentTypeHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<bool> Handle(DeleteDocumentTypeCommand request, CancellationToken cancellationToken)
        {
            var definition = await _session.Query<DocumentTypeDefinition>()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (definition == null) return false;
            if (definition.IsLocked) throw new InvalidOperationException("Seeded document types cannot be deleted.");

            var mappings = await _session.Query<UserDocumentType>()
                .Where(x => x.DocumentTypeId == request.Id)
                .ToListAsync(cancellationToken);

            foreach (var mapping in mappings)
            {
                _session.Delete(mapping);
            }

            _session.Delete(definition);
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
