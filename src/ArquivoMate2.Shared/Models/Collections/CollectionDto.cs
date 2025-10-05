using System;

namespace ArquivoMate2.Shared.Models.Collections;

public class CollectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int DocumentCount { get; set; }
}
