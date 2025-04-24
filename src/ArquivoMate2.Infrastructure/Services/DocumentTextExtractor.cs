using ArquivoMate2.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    public class DocumentTextExtractor : IDocumentTextExtractor
    {
        public Task<string> ExtractPdfTextAsync(Stream documentStream, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
