using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenRouterChatBot : IChatBot
    {
        private readonly OpenAIChatBot _innerChatBot;
        private readonly ILogger<OpenRouterChatBot> _logger;

        public OpenRouterChatBot(ChatClient client, IDocumentVectorizationService vectorizationService, ILoggerFactory loggerFactory, ILogger<OpenRouterChatBot> logger)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (vectorizationService is null)
            {
                throw new ArgumentNullException(nameof(vectorizationService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _innerChatBot = new OpenAIChatBot(client, vectorizationService, loggerFactory.CreateLogger<OpenAIChatBot>());
            _logger = logger;
        }

        public string ModelName => _innerChatBot.ModelName;

        public Task<DocumentAnalysisResult> AnalyzeDocumentContent(string content, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Delegating document analysis to OpenAIChatBot using OpenRouter backend.");
            return _innerChatBot.AnalyzeDocumentContent(content, cancellationToken);
        }

        public Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, IDocumentQuestionTooling tooling, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Delegating question answering to OpenAIChatBot using OpenRouter backend.");
            return _innerChatBot.AnswerQuestion(context, question, tooling, cancellationToken);
        }
    }
}
