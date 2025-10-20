using ArquivoMate2.Application.Queries.Users;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.Users;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ArquivoMate2.Api.Tests
{
    public class UsersControllerTests
    {
        [Fact]
        public async Task GetMe_Returns_ApiResponse_With_CurrentUser_When_Found()
        {
            // Arrange
            var userId = "user-1";
            var expected = new CurrentUserDto { Id = userId, Name = "Alice" };

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            var currentUserService = new Mock<ArquivoMate2.Application.Interfaces.ICurrentUserService>();
            currentUserService.Setup(c => c.UserId).Returns(userId);

            var controller = new ArquivoMate2.API.Controllers.UsersController(mediator.Object, currentUserService.Object);

            // Act
            var actionResult = await controller.GetMe(CancellationToken.None);

            // Assert
            if (actionResult.Result is OkObjectResult okResult)
            {
                var api = okResult.Value as ApiResponse<CurrentUserDto>;
                api.Should().NotBeNull();
                api!.Data.Should().BeEquivalentTo(expected);
                api.Success.Should().BeTrue();
            }
            else
            {
                // Some controllers may return the typed value directly
                actionResult.Value.Should().NotBeNull();
                actionResult.Value!.Data.Should().BeEquivalentTo(expected);
            }

            mediator.Verify(m => m.Send(It.Is<GetCurrentUserQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMe_Returns_NotFound_When_User_Not_Found()
        {
            // Arrange
            var userId = "user-2";

            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CurrentUserDto?)null);

            var currentUserService = new Mock<ArquivoMate2.Application.Interfaces.ICurrentUserService>();
            currentUserService.Setup(c => c.UserId).Returns(userId);

            var controller = new ArquivoMate2.API.Controllers.UsersController(mediator.Object, currentUserService.Object);

            // Act
            var actionResult = await controller.GetMe(CancellationToken.None);

            // Assert
            actionResult.Result.Should().BeOfType<NotFoundResult>();

            mediator.Verify(m => m.Send(It.Is<GetCurrentUserQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
