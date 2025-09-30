namespace ArquivoMate2.Shared.Models.Users;

/// <summary>
///     Represents the user data that is returned to API clients after login synchronisation.
/// </summary>
public class UserDto
{
    /// <summary>
    ///     Gets or sets the unique identifier of the user inside the application.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the display name of the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the timestamp of the first login that created the user profile (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the timestamp of the most recent login activity (UTC).
    /// </summary>
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}
