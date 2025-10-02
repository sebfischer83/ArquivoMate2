using System;

namespace ArquivoMate2.Shared.Models.Sharing;

public class ShareAutomationRuleDto
{
    public string Id { get; set; } = string.Empty;

    public ShareTarget Target { get; set; } = new();

    public ShareAutomationScope Scope { get; set; }

    public DocumentPermissions Permissions { get; set; }

    [Obsolete("Use Permissions")]
    public bool CanEdit => Permissions.HasFlag(DocumentPermissions.Edit);
}
