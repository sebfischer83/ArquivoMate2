using ArquivoMate2.Application.Models;
using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IEmailService
    {
        EmailProviderType ProviderType { get; }

        string UserId { get; }  

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

        /// <summary>
        /// Moves an email from the source folder to the destination folder and sets a custom flag on it.
        /// Only supported by IMAP. Other providers will throw NotSupportedException.
        /// </summary>
        /// <param name="sourceFolderName">The name of the source folder.</param>
        /// <param name="destinationFolderName">The name of the destination folder.</param>
        /// <param name="emailUid">The UID of the email to move (as uint).</param>
        /// <param name="customFlag">The custom flag to set (e.g., "Processed").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="NotSupportedException">Thrown when the provider doesn't support this operation</exception>
        Task MoveEmailWithFlagAsync(string sourceFolderName, string destinationFolderName, uint emailUid, string customFlag, CancellationToken cancellationToken = default);
    }
}