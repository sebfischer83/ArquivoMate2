using ArquivoMate2.API.Hubs;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Notifications
{
    public class SignalRDocumentProcessingNotifier : IDocumentProcessingNotifier
    {
        private readonly IHubContext<DocumentProcessingHub> _hubContext;
        public SignalRDocumentProcessingNotifier(IHubContext<DocumentProcessingHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyStatusChangedAsync(string userId, DocumentProcessingNotification processingNotification)
        {
            return _hubContext.Clients.Group(userId).SendAsync(
                "DocumentProcessingNotification",
                   processingNotification
            );
        }
    }
}
