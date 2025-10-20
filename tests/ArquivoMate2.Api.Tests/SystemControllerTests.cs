using ArquivoMate2.Application.Queries.Features;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ArquivoMate2.Api.Tests
{
    public class SystemControllerTests
    {
        [Fact]
        public async Task GetFeatures_Returns_ApiResponse_With_Features()
        {
            // Arrange
            var expected = new FeaturesDto
            {
                ChatBotConfigured = true,
                ChatBotAvailable = true,
                ChatBotProvider = "OpenAI",
                ChatBotModel = "gpt-4",
                EmbeddingsEnabled = true,
                EmbeddingsClientAvailable = true,
                EmbeddingsModel = "text-embedding-3-small",
                VectorStoreConfigured = false,
                VectorizationAvailable = false
            };

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetFeaturesQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            var controller = new ArquivoMate2.API.Controllers.SystemController(mediator.Object);

            // Act
            var actionResult = await controller.GetFeatures();

            // Assert
            var ok = actionResult as OkObjectResult;
            ok.Should().NotBeNull();
            var api = ok!.Value as ApiResponse<FeaturesDto>;
            api.Should().NotBeNull();
            api!.Data.Should().BeEquivalentTo(expected);
            api.Success.Should().BeTrue();

            // Verify mediator was called once
            mediator.Verify(m => m.Send(It.IsAny<GetFeaturesQuery>(), It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
