using ArquivoMate2.Application.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Retrieves emails based on the specified criteria
        /// </summary>
        /// <param name="criteria">Filtering criteria for emails</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of emails matching the criteria</returns>
        Task<IEnumerable<EmailMessage>> GetEmailsAsync(EmailCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tests the email connection with current settings
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connection successful</returns>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the total count of emails in the mailbox
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Total email count</returns>
        Task<int> GetEmailCountAsync(CancellationToken cancellationToken = default);
    }
}