using System;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Domain.Sharing;

public class DocumentShare
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public ShareTarget Target { get; set; } = new();

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    public string? GrantedBy { get; set; }
}
