using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentProcessor
    {
        Task<string> ExtractPdfTextAsync(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, bool forceOcr, CancellationToken cancellationToken = default);

        Task<string> ExtractImageTextAsync(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, CancellationToken cancellationToken = default);

        Task GeneratePreviewPdf(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, Stream output, CancellationToken cancellationToken = default);

        Task GenerateArchivePdf(Stream documentStream, Domain.ValueObjects.DocumentMetadata documentMetadata, Stream output, CancellationToken cancellationToken = default);
    }
}
