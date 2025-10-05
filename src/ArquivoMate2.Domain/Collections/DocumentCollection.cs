using System;

namespace ArquivoMate2.Domain.Collections;

/// <summary>
/// User owned logical collection used to organise documents (no ACL semantics).
/// </summary>
public class DocumentCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Upper invariant name used for uniqueness per owner.
    /// </summary>
    public string NormalizedName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
