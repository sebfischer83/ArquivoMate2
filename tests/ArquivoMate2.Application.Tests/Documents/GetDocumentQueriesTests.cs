using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Handlers.Documents;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Queries.Documents;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using Marten;
using Marten.Events;
using JasperFx.Events;
using Moq;
using ArquivoMate2.Application.Tests.Support;

namespace ArquivoMate2.Application.Tests.Documents;

public class GetDocumentQueriesTests
{
    private static readonly MapperConfiguration MapperConfiguration = new(cfg =>
    {
        cfg.CreateMap<DocumentView, DocumentListItemDto>();
        cfg.CreateMap<DocumentView, DocumentDto>();
    });

    private static readonly IMapper Mapper = MapperConfiguration.CreateMapper();

    [Fact]
    public async Task GetDocumentListQueryHandler_FiltersAndSorts()
    {
        var userId = "user-1";
        var accessibleDocId = Guid.NewGuid();
        var excludedDocId = Guid.NewGuid();
        var ownDocId = Guid.NewGuid();

        var documents = new List<DocumentView>
        {
            new()
            {
                Id = ownDocId,
                UserId = userId,
                Processed = true,
                Deleted = false,
                Date = new DateTime(2024, 3, 2),
                UploadedAt = new DateTime(2024, 3, 2),
                Title = "Own",
                ThumbnailPath = "thumb1",
                Encrypted = false
            },
            new()
            {
                Id = accessibleDocId,
                UserId = "other",
                Processed = true,
                Deleted = false,
                Date = new DateTime(2024, 2, 1),
                UploadedAt = new DateTime(2024, 2, 1),
                Title = "Shared",
                ThumbnailPath = "thumb2",
                Encrypted = true
            },
            new()
            {
                Id = excludedDocId,
                UserId = "other",
                Processed = true,
                Deleted = false,
                Date = new DateTime(2024, 1, 1),
                UploadedAt = new DateTime(2024, 1, 1),
                Title = "NoAccess",
                ThumbnailPath = "thumb3",
                Encrypted = false
            }
        };

        var accessViews = new List<DocumentAccessView>
        {
            new()
            {
                Id = accessibleDocId,
                OwnerUserId = "owner",
                EffectiveUserIds = new HashSet<string> { userId }
            }
        };

        var querySessionMock = new Mock<IQuerySession>();
        querySessionMock.Setup(q => q.Query<DocumentView>()).Returns(MartenQueryableMockFactory.Create(documents));
        querySessionMock.Setup(q => q.Query<DocumentAccessView>()).Returns(MartenQueryableMockFactory.Create(accessViews));
        querySessionMock.Setup(q => q.Query<DocumentCollectionMembership>()).Returns(MartenQueryableMockFactory.Create(Array.Empty<DocumentCollectionMembership>()));

        var searchClientMock = new Mock<ISearchClient>();

        var handler = new GetDocumentListQueryHandler(querySessionMock.Object, searchClientMock.Object, Mapper);

        var request = new DocumentListRequestDto { Page = 1, PageSize = 10 };
        var result = await handler.Handle(new GetDocumentListQuery(userId, request), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Documents.Count);
        Assert.DoesNotContain(result.Documents, d => d.Id == excludedDocId);
        Assert.Equal(ownDocId, result.Documents.First().Id);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetDocumentListQueryHandler_UsesSearchOrdering()
    {
        var userId = "user-2";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var documents = new List<DocumentView>
        {
            new()
            {
                Id = firstId,
                UserId = userId,
                Processed = true,
                Deleted = false,
                Date = new DateTime(2024, 3, 3),
                UploadedAt = new DateTime(2024, 3, 3),
                Title = "First",
                ThumbnailPath = "t1"
            },
            new()
            {
                Id = secondId,
                UserId = userId,
                Processed = true,
                Deleted = false,
                Date = new DateTime(2024, 3, 1),
                UploadedAt = new DateTime(2024, 3, 1),
                Title = "Second",
                ThumbnailPath = "t2"
            }
        };

        var querySessionMock = new Mock<IQuerySession>();
        querySessionMock.Setup(q => q.Query<DocumentView>()).Returns(MartenQueryableMockFactory.Create(documents));
        querySessionMock.Setup(q => q.Query<DocumentAccessView>()).Returns(MartenQueryableMockFactory.Create(Array.Empty<DocumentAccessView>()));
        querySessionMock.Setup(q => q.Query<DocumentCollectionMembership>()).Returns(MartenQueryableMockFactory.Create(Array.Empty<DocumentCollectionMembership>()));

        var searchClientMock = new Mock<ISearchClient>();
        searchClientMock.Setup(s => s.SearchDocumentIdsAsync(userId, "invoice", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ids: (IReadOnlyList<Guid>)new List<Guid> { secondId, firstId }, Total: 2L));

        var handler = new GetDocumentListQueryHandler(querySessionMock.Object, searchClientMock.Object, Mapper);

        var request = new DocumentListRequestDto { Page = 1, PageSize = 10, Search = "invoice" };
        var result = await handler.Handle(new GetDocumentListQuery(userId, request), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { secondId, firstId }, result.Documents.Select(d => d.Id));
    }

    [Fact]
    public async Task GetDocumentStatsQueryHandler_ComputesAggregates()
    {
        var userId = "stats-user";
        var sharedDocId = Guid.NewGuid();

        var documents = new List<DocumentView>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Deleted = false,
                Processed = true,
                Accepted = true,
                ContentLength = 100
            },
            new()
            {
                Id = sharedDocId,
                UserId = "owner",
                Deleted = false,
                Processed = true,
                Accepted = false,
                ContentLength = 50
            }
        };

        var accessViews = new List<DocumentAccessView>
        {
            new()
            {
                Id = sharedDocId,
                OwnerUserId = "owner",
                EffectiveUserIds = new HashSet<string> { userId }
            }
        };

        var querySessionMock = new Mock<IQuerySession>();
        querySessionMock.Setup(q => q.Query<DocumentView>()).Returns(MartenQueryableMockFactory.Create(documents));
        querySessionMock.Setup(q => q.Query<DocumentAccessView>()).Returns(MartenQueryableMockFactory.Create(accessViews));

        var facets = new Dictionary<string, int> { ["2024"] = 2 };

        var searchClientMock = new Mock<ISearchClient>();
        searchClientMock.Setup(s => s.GetFacetsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(facets);

        var handler = new GetDocumentStatsQueryHandler(querySessionMock.Object, searchClientMock.Object);

        var result = await handler.Handle(new GetDocumentStatsQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Documents);
        Assert.Equal(1, result.NotAccepted);
        Assert.Equal(150, result.Characters);
        Assert.Equal(facets, result.Facets);
    }

    [Fact]
    public async Task GetDocumentDetailQueryHandler_ReturnsDocumentWithHistory()
    {
        var userId = "detail-user";
        var documentId = Guid.NewGuid();

        var documents = new List<DocumentView>
        {
            new()
            {
                Id = documentId,
                UserId = userId,
                Deleted = false,
                Processed = true,
                Content = "content",
                ContentLength = 10,
                Encrypted = true,
                ThumbnailPath = "thumb"
            }
        };

        var querySessionMock = new Mock<IQuerySession>();
        querySessionMock.Setup(q => q.Query<DocumentView>()).Returns(MartenQueryableMockFactory.Create(documents));

        var eventStoreMock = new Mock<IQueryEventStore>();
        var eventData = new TestDocumentEvent
        {
            OccurredOn = new DateTime(2024, 3, 5, 10, 0, 0, DateTimeKind.Utc),
            UserId = "author"
        };

        var eventMock = new Mock<IEvent>();
        eventMock.SetupGet(e => e.EventTypeName).Returns("DocumentUpdated");
        eventMock.SetupGet(e => e.Timestamp).Returns(new DateTimeOffset(new DateTime(2024, 3, 5, 10, 0, 0, DateTimeKind.Utc)));
        eventMock.SetupGet(e => e.Data).Returns(eventData);

        eventStoreMock.Setup(es => es.FetchStreamAsync(documentId, It.IsAny<long>(), It.IsAny<DateTimeOffset?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<IEvent>)new List<IEvent> { eventMock.Object });

        querySessionMock.SetupGet(q => q.Events).Returns(eventStoreMock.Object);

        var accessServiceMock = new Mock<IDocumentAccessService>();
        accessServiceMock.Setup(s => s.HasAccessToDocumentAsync(documentId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new GetDocumentDetailQueryHandler(querySessionMock.Object, accessServiceMock.Object, Mapper);

        var result = await handler.Handle(new GetDocumentDetailQuery(userId, documentId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(documentId, result!.Document.Id);
        Assert.Single(result.Document.History);
        Assert.Equal("author", result.Document.History[0].UserId);
    }

    private sealed class TestDocumentEvent
    {
        public DateTime OccurredOn { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}
