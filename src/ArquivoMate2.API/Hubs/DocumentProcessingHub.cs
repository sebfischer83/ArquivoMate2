using Microsoft.AspNetCore.SignalR;

namespace ArquivoMate2.API.Hubs
{
    public class DocumentProcessingHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }
    }
}
