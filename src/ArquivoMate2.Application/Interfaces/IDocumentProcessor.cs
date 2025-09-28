using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentProcessor
    {
        Task<string> ExtractPdfTextAsync(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, bool forceOcr, CancellationToken cancellationToken = default);
        
        Task<string> ExtractImageTextAsync(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, CancellationToken cancellationToken = default);
        
        Task<byte[]> GeneratePreviewPdf(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, CancellationToken cancellationToken = default);

        Task<byte[]> GenerateArchivePdf(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, CancellationToken cancellationToken = default);
    }
}
