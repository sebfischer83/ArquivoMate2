using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.Sharing;

public class UpdateShareGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public List<string> MemberUserIds { get; set; } = new();
}
