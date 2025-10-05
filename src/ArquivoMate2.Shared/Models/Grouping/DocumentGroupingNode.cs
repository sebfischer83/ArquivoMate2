namespace ArquivoMate2.Shared.Models.Grouping;

public class DocumentGroupingNode
{
    public string Dimension { get; set; } = string.Empty; // Which dimension this node represents
    public string Key { get; set; } = string.Empty;       // Internal key (id, year, month code, etc.)
    public string Label { get; set; } = string.Empty;     // Human readable label
    public int Count { get; set; }                        // Number of documents in this bucket
    public bool HasChildren { get; set; }                 // Whether more group levels follow
}
