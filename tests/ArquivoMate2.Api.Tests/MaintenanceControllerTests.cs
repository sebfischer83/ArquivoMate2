using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using StackExchange.Redis;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Marten.Linq;
using Marten.Events;
using Marten;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System;

namespace ArquivoMate2.Api.Tests
{
    public class MaintenanceControllerTests
    {
        [Fact]
        public async Task GetCacheKeyCounts_Returns_Empty_When_No_Redis_Endpoints()
        {
            // Arrange
            var querySession = new Mock<IQuerySession>();
            var logger = new Mock<ILogger<ArquivoMate2.API.Controllers.MaintenanceController>>();

            var mux = new Mock<IConnectionMultiplexer>();
            // call with explicit matcher to avoid optional-argument expression
            mux.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<EndPoint>());

            var meili = new Mock<MeilisearchClient>("http://localhost:7700", "key");
            var config = new Mock<IConfiguration>();

            var controller = new ArquivoMate2.API.Controllers.MaintenanceController(querySession.Object, logger.Object, mux.Object, meili.Object, config.Object);

            // Act
            var result = await controller.GetCacheKeyCounts(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            var dict = ok!.Value as Dictionary<string, long>;
            dict.Should().NotBeNull();
            dict!.Count.Should().Be(0);
        }

        [Fact]
        public async Task GetInfraStats_Returns_OkObjectResult()
        {
            // Arrange
            var querySession = new Mock<IQuerySession>();
            var logger = new Mock<ILogger<ArquivoMate2.API.Controllers.MaintenanceController>>();

            var mux = new Mock<IConnectionMultiplexer>();
            var db = new Mock<IDatabase>();
            // Make ExecuteAsync throw so controller handles it and includes error info
            db.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object[]>())).ThrowsAsync(new Exception("no redis"));
            // Use explicit matcher for GetDatabase optional args
            mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
            mux.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<EndPoint>());

            var meili = new Mock<MeilisearchClient>("http://localhost:7700", "key");
            // Note: MeilisearchClient methods are non-virtual and cannot be mocked with Moq.
            // We rely on the controller's try/catch to handle any exceptions from the real client methods.

            var inMemoryConfig = new Dictionary<string, string?>();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

            var controller = new ArquivoMate2.API.Controllers.MaintenanceController(querySession.Object, logger.Object, mux.Object, meili.Object, configuration);

            // Act
            var result = await controller.GetInfraStats(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            var payload = ok!.Value as dynamic;
            ((object)payload).Should().NotBeNull();
            Assert.True(payload.RedisInfo != null);
            Assert.True(payload.KeyCounts != null);
            Assert.True(payload.MeiliHealth != null);
            Assert.True(payload.Database != null);
        }
    }
}
