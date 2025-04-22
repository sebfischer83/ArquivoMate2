using ArquivoMate2.Application.Commands;
using ArquivoMate2.Domain.Document;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ArquivoMate2.Application.Handlers
{
    public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<ProcessDocumentHandler> _logger;
        public ProcessDocumentHandler(IDocumentSession session, ILogger<ProcessDocumentHandler> logger)
            => (_session, _logger) = (session, logger);

        async Task IRequestHandler<ProcessDocumentCommand>.Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting processing for document {DocumentId}", request.DocumentId);
            var sw = Stopwatch.StartNew();
            try
            {
                var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
                if (doc is null)
                {
                    _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");
                }

                doc.MarkAsProcessed();
                _session.Events.Append(request.DocumentId, new DocumentProcessed(request.DocumentId, DateTime.UtcNow));
                await _session.SaveChangesAsync(cancellationToken);

                sw.Stop();
                _logger.LogInformation("Processed document {DocumentId} in {ElapsedMs}ms", request.DocumentId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
                throw;
            }
        }
    }
}
