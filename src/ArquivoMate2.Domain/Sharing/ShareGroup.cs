using System;
using System.Collections.Generic;

namespace ArquivoMate2.Domain.Sharing;

public class ShareGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public List<string> MemberUserIds { get; set; } = new();
}
