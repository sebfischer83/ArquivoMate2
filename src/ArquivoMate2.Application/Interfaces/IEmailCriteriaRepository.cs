using ArquivoMate2.Domain.Email;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Repository interface for managing user email criteria (single criteria per user)
    /// </summary>
    public interface IEmailCriteriaRepository
    {
        /// <summary>
        /// Gets the email criteria for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Email criteria or null if not found</returns>
        Task<EmailCriteria?> GetEmailCriteriaAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves email criteria (creates new or updates existing for the user)
        /// </summary>
        /// <param name="emailCriteria">Email criteria to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Saved email criteria</returns>
        Task<EmailCriteria> SaveEmailCriteriaAsync(EmailCriteria emailCriteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes email criteria for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteEmailCriteriaAsync(string userId, CancellationToken cancellationToken = default);
    }
}