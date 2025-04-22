using ArquivoMate2.Application.Commands;
using ArquivoMate2.Domain.Document;
using Marten;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers
{
    public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, Guid>
    {
        private readonly IDocumentSession _session;
        public UploadDocumentHandler(IDocumentSession session) => _session = session;
        public async Task<Guid> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
        {
            var @event = new DocumentUploaded(Guid.NewGuid(), request.FilePath, DateTime.UtcNow);
            _session.Events.StartStream<Document>(@event.AggregateId, @event);
            await _session.SaveChangesAsync(cancellationToken);
            return @event.AggregateId;
        }
    }
}
