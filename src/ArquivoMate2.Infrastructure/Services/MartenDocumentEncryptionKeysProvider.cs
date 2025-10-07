using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Marten.Events;
using Marten;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    public class MartenDocumentEncryptionKeysProvider : IDocumentEncryptionKeysProvider
    {
        private readonly IQuerySession _query;

        public MartenDocumentEncryptionKeysProvider(IQuerySession query)
        {
            _query = query;
        }

        public async Task<DocumentEncryptionKeysAdded?> GetLatestAsync(Guid documentId, CancellationToken ct = default)
        {
            var events = await _query.Events.FetchStreamAsync(documentId, token: ct).ConfigureAwait(false);
            var keys = events.Select(e => e.Data).OfType<DocumentEncryptionKeysAdded>().LastOrDefault();
            return keys;
        }
    }
}
