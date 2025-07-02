using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.EmailProvider
{
    /// <summary>
    /// Factory for creating email services based on runtime database settings
    /// </summary>
    public class EmailServiceFactory
    {
        private readonly IEmailSettingsRepository _settingsRepository;
        private readonly ICurrentUserService _currentUserService;

        public EmailServiceFactory(
            IEmailSettingsRepository settingsRepository,
            ICurrentUserService currentUserService)
        {
            _settingsRepository = settingsRepository;
            _currentUserService = currentUserService;
        }

        public async Task<IEmailService> CreateEmailServiceAsync(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            
            if (string.IsNullOrEmpty(userId))
            {
                return new NullEmailService();
            }

            var settings = await _settingsRepository.GetEmailSettingsAsync(userId, cancellationToken);
            
            if (settings == null || !settings.IsActive)
            {
                return new NullEmailService();
            }

            return settings.ProviderType switch
            {
                EmailProviderType.IMAP => new ImapEmailService(settings),
                EmailProviderType.POP3 => new Pop3EmailService(settings),
                _ => new NullEmailService()
            };
        }
    }
}