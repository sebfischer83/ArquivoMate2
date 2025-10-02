using System;

namespace ArquivoMate2.Shared.Models.Sharing;

public class DocumentShareDto
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public ShareTarget Target { get; set; } = new();

    public DateTime SharedAt { get; set; }

    public string? GrantedBy { get; set; }

    public DocumentPermissions Permissions { get; set; }

    [Obsolete("Use Permissions")]
    public bool CanEdit => Permissions.HasFlag(DocumentPermissions.Edit);
}
