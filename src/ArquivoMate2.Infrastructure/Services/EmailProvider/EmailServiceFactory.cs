using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.EmailProvider
{
    /// <summary>
    /// Factory for creating email services based on runtime database settings
    /// </summary>
    public class EmailServiceFactory : IEmailServiceFactory
    {
        private readonly IEmailSettingsRepository _settingsRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ImapEmailService> _imapLogger;
        private readonly ILogger<Pop3EmailService> _pop3Logger;

        public EmailServiceFactory(
            IEmailSettingsRepository settingsRepository,
            ICurrentUserService currentUserService,
            ILogger<ImapEmailService> imapLogger,
            ILogger<Pop3EmailService> pop3Logger)
        {
            _settingsRepository = settingsRepository;
            _currentUserService = currentUserService;
            _imapLogger = imapLogger;
            _pop3Logger = pop3Logger;
        }

        public async Task<List<IEmailService>> CreateEmailServicesAsync(CancellationToken cancellationToken = default)
        {
            var services = new List<IEmailService>();

            var all = await _settingsRepository.GetEmailSettingsAsync(cancellationToken);

            foreach (var settings in all)
            {
                if (settings.IsActive)
                {
                    IEmailService service = settings.ProviderType switch
                    {
                        EmailProviderType.IMAP => new ImapEmailService(settings, _imapLogger),
                        EmailProviderType.POP3 => new Pop3EmailService(settings, _pop3Logger),
                        _ => new NullEmailService()
                    };
                    services.Add(service);
                }
            }

            return services;
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
                EmailProviderType.IMAP => new ImapEmailService(settings, _imapLogger),
                EmailProviderType.POP3 => new Pop3EmailService(settings, _pop3Logger),
                _ => new NullEmailService()
            };
        }
    }
}