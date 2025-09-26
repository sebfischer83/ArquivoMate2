using ArquivoMate2.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IChatBot
    {
        string ModelName { get; }

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken);
    }
}
