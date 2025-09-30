using System.ComponentModel.DataAnnotations;

namespace ArquivoMate2.Shared.Models.Users;

/// <summary>
///     Request payload used when a user logs in and the application needs to
///     persist or update profile information.
/// </summary>
public class UpsertUserRequest
{
    /// <summary>
    ///     Gets or sets the display name of the user as received from the identity provider.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
