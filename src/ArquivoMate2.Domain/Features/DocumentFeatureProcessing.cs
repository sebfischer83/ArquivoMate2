namespace ArquivoMate2.Domain.Features
{
    public enum FeatureProcessingState
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public class DocumentFeatureProcessing
    {
        public Guid Id { get; set; }              // DocumentId
        public string FeatureKey { get; set; } = string.Empty;
        public FeatureProcessingState State { get; set; } = FeatureProcessingState.Pending;
        public int AttemptCount { get; set; }
        public bool ChatBotAvailable { get; set; }
        public bool ChatBotUsed { get; set; }
        public int ChatBotCallCount { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? FailedAtUtc { get; set; }
        public string? LastError { get; set; }
    }
}
