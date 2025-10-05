using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.Collections;

public class AssignDocumentsRequest
{
    public Guid CollectionId { get; set; }
    public List<Guid> DocumentIds { get; set; } = new();
}
