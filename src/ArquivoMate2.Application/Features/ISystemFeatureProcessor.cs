using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.Application.Features
{
    public class SystemFeatureProcessingContext
    {
        public Guid DocumentId { get; init; }
        public string FeatureKey { get; init; } = string.Empty;
        public string? DocumentType { get; init; }
        public string UserId { get; init; } = string.Empty;
        public IChatBot? ChatBot { get; init; }
        public bool ChatBotAvailable => ChatBot != null;
        public string? DocumentContent { get; init; }
        public DateTime TriggeredAtUtc { get; init; } = DateTime.UtcNow;
    }

    public interface ISystemFeatureProcessor
    {
        string FeatureKey { get; }
        Task ProcessAsync(SystemFeatureProcessingContext context, CancellationToken ct);
    }
}
