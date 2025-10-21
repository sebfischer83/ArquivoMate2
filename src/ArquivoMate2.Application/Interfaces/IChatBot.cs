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

        /// <summary>
        /// Ask a question by providing the document content directly as prompt text.
        /// Optionally a JSON schema can be provided to request structured output from the model.
        /// Implementations should honour the schema when possible and return a DocumentAnswerResult.
        /// </summary>
        Task<DocumentAnswerResult> AnswerQuestionWithPrompt(
            string question,
            string documentContent,
            string? structuredJsonSchema,
            CancellationToken cancellationToken);
    }
}
