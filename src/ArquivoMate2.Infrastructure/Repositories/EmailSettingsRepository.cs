using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using Marten;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Repositories
{
    public class EmailSettingsRepository : IEmailSettingsRepository
    {
        private readonly IDocumentSession _session;

        public EmailSettingsRepository(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<EmailSettings?> GetEmailSettingsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _session.Query<EmailSettings>()
                .Where(x => x.UserId == userId && x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveEmailSettingsAsync(EmailSettings emailSettings, CancellationToken cancellationToken = default)
        {
            emailSettings.UpdatedAt = DateTime.UtcNow;
            
            // Deactivate any existing settings for this user
            var existingSettings = await _session.Query<EmailSettings>()
                .Where(x => x.UserId == emailSettings.UserId && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingSettings)
            {
                existing.IsActive = false;
                _session.Update(existing);
            }

            // Save the new settings
            if (emailSettings.Id == Guid.Empty)
            {
                emailSettings.Id = Guid.NewGuid();
                emailSettings.CreatedAt = DateTime.UtcNow;
                _session.Store(emailSettings);
            }
            else
            {
                _session.Update(emailSettings);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteEmailSettingsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var settings = await _session.Query<EmailSettings>()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var setting in settings)
            {
                setting.IsActive = false;
                _session.Update(setting);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> EmailSettingsExistAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _session.Query<EmailSettings>()
                .Where(x => x.UserId == userId && x.IsActive)
                .AnyAsync(cancellationToken);
        }
    }
}