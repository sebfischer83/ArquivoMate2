namespace ArquivoMate2.Shared.Models.Users;

/// <summary>
///     Represents the API key information that is returned to the client when a key is generated.
/// </summary>
public class UserApiKeyDto
{
    /// <summary>
    ///     Gets or sets the generated API key for the user.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
