namespace ArquivoMate2.Domain.Sharing
{
    public class ExternalShare
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Artifact { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool Revoked { get; set; }
    }
}
