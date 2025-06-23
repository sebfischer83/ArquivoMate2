using ArquivoMate2.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArquivoMate2.API.Hubs
{
    [Authorize]
    public class DocumentProcessingHub : Hub
    {
        private readonly ICurrentUserService _currentUserService;

        public DocumentProcessingHub(ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        public override Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            var userName = Context.User?.Identity?.Name;
            var connectionId = _currentUserService.UserId;


            return base.OnConnectedAsync();
        }
    }
}
