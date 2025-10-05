namespace ArquivoMate2.Shared.Models.Grouping;

public class DocumentGroupingPathSegment
{
    public string Dimension { get; set; } = string.Empty; // e.g. Collection, Year, Month, Type, Language
    public string Key { get; set; } = string.Empty;       // internal key value
}
