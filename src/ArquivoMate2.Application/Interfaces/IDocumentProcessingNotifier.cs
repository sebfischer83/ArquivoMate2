namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentProcessingNotifier
    {
        Task NotifyStatusChangedAsync(Guid documentId, string userId, string status, bool finished = false, bool error = false);
    }
}
