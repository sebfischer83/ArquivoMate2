using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.Sharing;

public class ShareGroupDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> MemberUserIds { get; set; } = new();
}
