using System;

namespace ArquivoMate2.Domain.Collections;

/// <summary>
/// N:N membership between a document and a collection owned by a user.
/// </summary>
public class DocumentCollectionMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectionId { get; set; }
    public Guid DocumentId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty; // redundant for fast querying by owner
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
