using System;
using System.Collections.Generic;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Domain.ReadModels;

public class DocumentAccessView
{
    // Use DocumentId as Id for Marten identity
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public Dictionary<string, DocumentPermissions> DirectUserPermissions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, DocumentPermissions> GroupPermissions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, DocumentPermissions> EffectiveUserPermissions { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> EffectiveUserIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> EffectiveEditUserIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> EffectiveDeleteUserIds { get; set; } = new(StringComparer.Ordinal);
    public int DirectUserShareCount { get; set; }
    public int GroupShareCount { get; set; }
}
