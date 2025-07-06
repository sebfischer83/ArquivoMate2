using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.EmailProvider
{
    /// <summary>
    /// Null implementation of IEmailService that provides no actual email functionality.
    /// Used as default when no email settings are configured.
    /// </summary>
    public class NullEmailService : IEmailService
    {
        public EmailProviderType ProviderType =>  EmailProviderType.Null;

        public string UserId => string.Empty;

        public Task<IEnumerable<EmailMessage>> GetEmailsAsync(ArquivoMate2.Shared.Models.EmailCriteria criteria, CancellationToken cancellationToken = default)
        {
            // Return empty list - no emails available
            return Task.FromResult(Enumerable.Empty<EmailMessage>());
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            // Always return false - no connection possible
            return Task.FromResult(false);
        }

        public Task<int> GetEmailCountAsync(CancellationToken cancellationToken = default)
        {
            // Always return 0 - no emails available
            return Task.FromResult(0);
        }

        /// <summary>
        /// Null implementation does not support moving emails or setting custom flags.
        /// This method throws NotSupportedException.
        /// </summary>
        public Task MoveEmailWithFlagAsync(string sourceFolderName, string destinationFolderName, uint emailUid, string customFlag, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("No email service configured. Configure IMAP settings to use email operations.");
        }
    }
}