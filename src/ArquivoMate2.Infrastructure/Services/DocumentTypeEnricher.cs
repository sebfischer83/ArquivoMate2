using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models;
using Marten;

namespace ArquivoMate2.Infrastructure.Services
{
    public class DocumentTypeEnricher : IDocumentTypeEnricher
    {
        private readonly IQuerySession _query;

        public DocumentTypeEnricher(IQuerySession query)
        {
            _query = query;
        }

        public async Task EnrichAsync(DocumentDto dto, string? documentTypeName, IQuerySession querySession, CancellationToken cancellationToken = default)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            try
            {
                var docTypeName = documentTypeName;
                if (!string.IsNullOrWhiteSpace(docTypeName))
                {
                    var definition = await querySession.Query<ArquivoMate2.Domain.DocumentTypes.DocumentTypeDefinition>()
                        .FirstOrDefaultAsync(x => x.Name.Equals(docTypeName, StringComparison.OrdinalIgnoreCase), cancellationToken);

                    dto.DocumentTypeSystemFeatures = definition?.SystemFeatures ?? new System.Collections.Generic.List<string>();
                    dto.DocumentTypeUserFunctions = definition?.UserDefinedFunctions ?? new System.Collections.Generic.List<string>();
                }
            }
            catch
            {
                // ignore lookup failures
            }
        }
    }
}
