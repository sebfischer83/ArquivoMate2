using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentTextExtractor
    {
        Task<string> ExtractPdfTextAsync(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, bool forceOcr, CancellationToken cancellationToken = default);
    }
}
