using ArquivoMate2.Domain.Email;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Repository interface for managing processed email records
    /// </summary>
    public interface IProcessedEmailRepository
    {
        /// <summary>
        /// Checks if an email UID has already been processed for a specific user
        /// </summary>
        /// <param name="emailUid">The email UID to check</param>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the email has been processed</returns>
        Task<bool> IsEmailProcessedAsync(uint emailUid, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a processed email record
        /// </summary>
        /// <param name="processedEmail">The processed email record to store</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveProcessedEmailAsync(ProcessedEmail processedEmail, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets processed email records for a specific user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of processed email records</returns>
        Task<IEnumerable<ProcessedEmail>> GetProcessedEmailsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific processed email record by UID and user ID
        /// </summary>
        /// <param name="emailUid">The email UID</param>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The processed email record or null if not found</returns>
        Task<ProcessedEmail?> GetProcessedEmailAsync(uint emailUid, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all processed email UIDs for a specific user (for filtering purposes)
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>HashSet of processed email UIDs</returns>
        Task<HashSet<uint>> GetProcessedEmailUidsAsync(string userId, CancellationToken cancellationToken = default);
    }
}