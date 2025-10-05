namespace ArquivoMate2.Shared.Models.Party;

public class UpdatePartyRequest
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; } = string.Empty;
    public string? Street { get; set; } = string.Empty;
    public string? HouseNumber { get; set; } = string.Empty;
    public string? PostalCode { get; set; } = string.Empty;
    public string? City { get; set; } = string.Empty;
}
