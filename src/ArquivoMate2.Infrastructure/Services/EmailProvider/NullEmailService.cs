using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
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
        public Task<IEnumerable<EmailMessage>> GetEmailsAsync(EmailCriteria criteria, CancellationToken cancellationToken = default)
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
    }
}