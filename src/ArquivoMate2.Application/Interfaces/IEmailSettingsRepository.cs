using ArquivoMate2.Domain.Email;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IEmailSettingsRepository
    {
        /// <summary>
        /// Gets the current email settings for the specified user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Email settings or null if not found</returns>
        Task<EmailSettings?> GetEmailSettingsAsync(string userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<EmailSettings>> GetEmailSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates email settings for the specified user
        /// </summary>
        /// <param name="emailSettings">Email settings to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveEmailSettingsAsync(EmailSettings emailSettings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes email settings for the specified user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteEmailSettingsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if email settings exist for the specified user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if settings exist</returns>
        Task<bool> EmailSettingsExistAsync(string userId, CancellationToken cancellationToken = default);
    }
}