using System;
using System.Collections.Generic;

namespace ArquivoMate2.Infrastructure.Persistance;

public class DocumentAccessView
{
    // Use DocumentId as Id for Marten identity
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public HashSet<string> DirectUserIds { get; set; } = new();
    public HashSet<string> GroupIds { get; set; } = new();
    public HashSet<string> EffectiveUserIds { get; set; } = new();
    public int DirectUserShareCount { get; set; }
    public int GroupShareCount { get; set; }
}
