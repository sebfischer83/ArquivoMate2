using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using ArquivoMate2.API.Hubs;

namespace ArquivoMate2.API.Notifications
{
    public class SignalRDocumentProcessingNotifier : IDocumentProcessingNotifier
    {
        private readonly IHubContext<DocumentProcessingHub> _hubContext;
        public SignalRDocumentProcessingNotifier(IHubContext<DocumentProcessingHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyStatusChangedAsync(Guid documentId, string userId, string status, bool finished = false, bool error = false)
        {
            return _hubContext.Clients.User(userId).SendAsync(
                "ProcessingUpdate",
                new {
                    DocumentId = documentId,
                    Status = status,
                    Finished = finished,
                    Error = error
                }
            );
        }
    }
}
