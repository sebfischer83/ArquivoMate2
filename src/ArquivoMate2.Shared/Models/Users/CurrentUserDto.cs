namespace ArquivoMate2.Shared.Models.Users;

/// <summary>
/// Represents the current authenticated user including the (non-secret) API key value if already generated.
/// </summary>
public class CurrentUserDto : UserDto
{
    /// <summary>
    /// Gets or sets the API key assigned to the user (null if not generated yet).
    /// </summary>
    public string? ApiKey { get; set; }
}
