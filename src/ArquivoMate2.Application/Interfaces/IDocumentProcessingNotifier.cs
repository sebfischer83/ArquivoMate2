using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentProcessingNotifier
    {
        Task NotifyStatusChangedAsync(string userId, DocumentProcessingNotification processingNotification);
    }
}
