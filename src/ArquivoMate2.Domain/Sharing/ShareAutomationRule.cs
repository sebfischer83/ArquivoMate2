using System;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Domain.Sharing;

public class ShareAutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string OwnerUserId { get; set; } = string.Empty;

    public ShareTarget Target { get; set; } = new();

    public ShareAutomationScope Scope { get; set; } = ShareAutomationScope.AllDocuments;
}
