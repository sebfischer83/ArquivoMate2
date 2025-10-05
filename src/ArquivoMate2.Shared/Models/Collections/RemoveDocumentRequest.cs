using System;

namespace ArquivoMate2.Shared.Models.Collections;

public class RemoveDocumentRequest
{
    public Guid CollectionId { get; set; }
    public Guid DocumentId { get; set; }
}
