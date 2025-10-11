using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Infrastructure.Services.Llm;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace ArquivoMate2.Infrastructure.Tests
{
    public class OpenAIChatBotPipelineTests
    {
        [Fact]
        public async Task AnswerQuestion_ExecutesQueryDocumentsToolCall()
        {
            var context = new DocumentQuestionContext
            {
                DocumentId = Guid.NewGuid(),
                Title = "Invoice 4711",
                Summary = "Monthly invoice",
                Keywords = new[] { "invoice", "2024" },
                Language = "de",
                Content = new string('A', 1500)
            };

            var queryResult = new DocumentQueryResult
            {
                Documents = new[]
                {
                    new DocumentQueryDocument
                    {
                        DocumentId = Guid.NewGuid(),
                        Title = "Invoice 4711",
                        Summary = "Summary",
                        Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                        Score = 0.82,
                        FileSizeBytes = 4096
                    }
                },
                TotalCount = 3
            };

            var responses = new[]
            {
                TestOpenAIChatBot.Response.WithToolCalls(
                    new TestOpenAIChatBot.ResponseToolCall(
                        "call-1",
                        "query_documents",
                        "{\"limit\":7,\"projection\":\"both\",\"filters\":{\"year\":2024}}")),
                TestOpenAIChatBot.Response.WithMessage(
                    $"{{\"answer\":\"Here is the data\",\"citations\":[],\"documents\":[{{\"id\":\"{queryResult.Documents[0].DocumentId}\",\"title\":\"Invoice 4711\",\"summary\":\"Summary\",\"date\":\"2024-03-01T00:00:00Z\",\"score\":0.82,\"file_size_bytes\":4096}}],\"document_count\":3}}")
            };

            var tooling = new Mock<IDocumentQuestionTooling>(MockBehavior.Strict);
            tooling.Setup(t => t.QueryDocumentsAsync(
                    It.Is<DocumentQuery>(q =>
                        q.Limit == 7 &&
                        q.Projection == DocumentQueryProjection.Both &&
                        q.Filters.Year == 2024),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryResult);

            var bot = new TestOpenAIChatBot(responses);

            var result = await bot.AnswerQuestion(context, "Welche Rechnungen habe ich 2024?", tooling.Object, CancellationToken.None);

            Assert.Equal("Here is the data", result.Answer);
            Assert.Equal("gpt-test", result.Model);
            Assert.Single(result.Documents);
            Assert.Equal(queryResult.Documents[0].DocumentId, result.Documents[0].DocumentId);
            Assert.Equal(3, result.DocumentCount);

            tooling.VerifyAll();

            Assert.Equal(2, bot.CallCount);
            Assert.Contains(bot.Calls[1], message => message is ToolChatMessage);
        }

        [Fact]
        public async Task AnswerQuestion_ContinuesWhenDocumentQueryFails()
        {
            var context = new DocumentQuestionContext
            {
                DocumentId = Guid.NewGuid(),
                Content = "Test content for failure scenario"
            };

            var responses = new[]
            {
                TestOpenAIChatBot.Response.WithToolCalls(
                    new TestOpenAIChatBot.ResponseToolCall("call-1", "query_documents", "{\"limit\":5}")),
                TestOpenAIChatBot.Response.WithMessage("{\"answer\":\"Fallback response\",\"citations\":[]}")
            };

            var tooling = new Mock<IDocumentQuestionTooling>();
            tooling.Setup(t => t.QueryDocumentsAsync(It.IsAny<DocumentQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Search failed"));

            var bot = new TestOpenAIChatBot(responses);

            var result = await bot.AnswerQuestion(context, "Funktioniert die Suche?", tooling.Object, CancellationToken.None);

            Assert.Equal("Fallback response", result.Answer);
            Assert.Equal("gpt-test", result.Model);
            Assert.Equal(2, bot.CallCount);
            Assert.Contains(bot.Calls[1], message => message is ToolChatMessage);
        }

        [Fact]
        public async Task AnswerQuestion_StopsAfterMaxToolIterations()
        {
            var context = new DocumentQuestionContext
            {
                DocumentId = Guid.NewGuid(),
                Content = new string('B', 2400)
            };

            var responses = Enumerable.Repeat(
                TestOpenAIChatBot.Response.WithToolCalls(
                    new TestOpenAIChatBot.ResponseToolCall("call-1", "load_document_chunk", "{\"chunk_id\":\"chunk_1\"}")),
                9).ToArray();
            var tooling = Mock.Of<IDocumentQuestionTooling>();

            var bot = new TestOpenAIChatBot(responses);

            var result = await bot.AnswerQuestion(context, "Bitte lade alle Chunks", tooling, CancellationToken.None);

            Assert.Equal(string.Empty, result.Answer);
            Assert.Equal("gpt-test", result.Model);
            Assert.Equal(9, bot.CallCount);
        }
 
        private sealed class TestOpenAIChatBot : OpenAIChatBot
        {
            private readonly Queue<Response> _responses;
            private readonly List<IReadOnlyList<ChatMessage>> _calls = new();

            public TestOpenAIChatBot(IEnumerable<Response> responses)
            {
                _responses = new Queue<Response>(responses);
            }

            public override string ModelName => "gpt-test";

            public int CallCount => _calls.Count;

            public IReadOnlyList<IReadOnlyList<ChatMessage>> Calls => _calls;

            protected override Task<ChatCompletionResult> CompleteChatAsync(
                IReadOnlyList<ChatMessage> messages,
                ChatCompletionOptions options,
                CancellationToken cancellationToken)
            {
                _calls.Add(messages.ToList());

                if (_responses.Count == 0)
                {
                    return Task.FromResult(new ChatCompletionResult(Array.Empty<FunctionToolCall>(), null));
                }

                var next = _responses.Dequeue();
                var toolCalls = next.ToolCalls
                    .Select(tc => new FunctionToolCall(tc.Id, tc.Name, tc.Arguments))
                    .ToArray();

                return Task.FromResult(new ChatCompletionResult(toolCalls, next.RawMessageText));
            }

            public static Response WithToolCalls(params ResponseToolCall[] toolCalls)
                => new(toolCalls, null);

            public static Response WithMessage(string? rawMessageText)
                => new(Array.Empty<ResponseToolCall>(), rawMessageText);

            internal sealed record Response(IReadOnlyList<ResponseToolCall> ToolCalls, string? RawMessageText);

            internal sealed record ResponseToolCall(string Id, string Name, string? Arguments);
        }
    }
}
