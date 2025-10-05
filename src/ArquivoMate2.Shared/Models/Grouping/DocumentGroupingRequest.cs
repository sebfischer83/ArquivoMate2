using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models.Grouping;

public class DocumentGroupingRequest
{
    public List<string> Groups { get; set; } = new(); // e.g. ["Collection","Year","Month"]
    public List<DocumentGroupingPathSegment> Path { get; set; } = new(); // already selected prefix
}
