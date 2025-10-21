using ArquivoMate2.Application.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IChatBot
    {
        string ModelName { get; }

        Task<DocumentAnalysisResult> AnalyzeDocumentContent(
            string content,
            IReadOnlyList<DocumentTypeOption> availableTypes,
            CancellationToken cancellationToken);

        Task<DocumentAnswerResult> AnswerQuestion(
            DocumentQuestionContext context,
            string question,
            IDocumentQuestionTooling tooling,
            CancellationToken cancellationToken);
    }
}
