using System;

namespace ArquivoMate2.Shared.Models
{
    public sealed class DocumentFeatureProcessingDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string FeatureKey { get; set; } = string.Empty;
        public FeatureProcessingState State { get; set; } // enum so client can understand allowed values
        public int AttemptCount { get; set; }
        public bool ChatBotAvailable { get; set; }
        public bool ChatBotUsed { get; set; }
        public int ChatBotCallCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? FailedAtUtc { get; set; }
        public string? LastError { get; set; }
    }
}
