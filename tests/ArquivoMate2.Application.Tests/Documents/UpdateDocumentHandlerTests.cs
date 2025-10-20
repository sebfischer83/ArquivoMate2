using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Shared.Models;
using Marten;
using Marten.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ArquivoMate2.Application.Tests.Documents;

public class UpdateDocumentHandlerTests
{
    [Fact]
    public async Task Handle_Should_Apply_CaseInsensitive_Field_Names_And_Use_Canonical_Property_Names()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var dto = new UpdateDocumentFieldsDto
        {
            Fields = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["keywords"] = new[] { "alpha", "beta" }
            }
        };

        var sessionMock = new Mock<IDocumentSession>();
        var eventsMock = new Mock<IEventStoreOperations>();

        DocumentUpdated? capturedEvent = null;
        eventsMock.Setup(e => e.Append(It.IsAny<Guid>(), It.IsAny<object[]>()))
            .Callback<Guid, object[]>((id, evs) => capturedEvent = evs != null && evs.Length > 0 ? evs[0] as DocumentUpdated : null);

        sessionMock.SetupGet(s => s.Events).Returns(eventsMock.Object);
        sessionMock.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<UpdateDocumentHandler>>();
        var handler = new UpdateDocumentHandler(sessionMock.Object, loggerMock.Object);

        // Act
        var result = await handler.Handle(new UpdateDocumentCommand(docId, dto), CancellationToken.None);

        // Assert
        Assert.Equal(PatchResult.Success, result);
        Assert.NotNull(capturedEvent);
        Assert.Equal(docId, capturedEvent!.AggregateId);
        Assert.True(capturedEvent.Values.ContainsKey("Keywords"), "Event should contain canonical property name 'Keywords'.");

        var value = capturedEvent.Values["Keywords"];

        if (value is string[] arr)
        {
            Assert.Equal(new[] { "alpha", "beta" }, arr);
        }
        else if (value is List<string> list)
        {
            Assert.Equal(new List<string> { "alpha", "beta" }, list);
        }
        else
        {
            // Fail the test with explicit message
            Assert.True(false, $"Unexpected value type for Keywords: {value?.GetType().FullName}");
        }

        sessionMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        eventsMock.Verify(e => e.Append(docId, It.IsAny<object[]>()), Times.Once);
    }
}
