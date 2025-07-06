using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using Marten;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for managing user email criteria using Marten (single criteria per user)
    /// </summary>
    public class EmailCriteriaRepository : IEmailCriteriaRepository
    {
        private readonly IDocumentSession _session;

        public EmailCriteriaRepository(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<EmailCriteria?> GetEmailCriteriaAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _session.Query<EmailCriteria>()
                .Where(x => x.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<EmailCriteria> SaveEmailCriteriaAsync(EmailCriteria emailCriteria, CancellationToken cancellationToken = default)
        {
            emailCriteria.UpdatedAt = DateTime.UtcNow;

            // Check if user already has criteria
            var existingCriteria = await GetEmailCriteriaAsync(emailCriteria.UserId, cancellationToken);
            
            if (existingCriteria != null)
            {
                // Update existing criteria
                existingCriteria.Name = emailCriteria.Name;
                existingCriteria.Description = emailCriteria.Description;
                existingCriteria.SubjectContains = emailCriteria.SubjectContains;
                existingCriteria.FromContains = emailCriteria.FromContains;
                existingCriteria.ToContains = emailCriteria.ToContains;
                existingCriteria.DateFrom = emailCriteria.DateFrom;
                existingCriteria.DateTo = emailCriteria.DateTo;
                existingCriteria.IsRead = emailCriteria.IsRead;
                existingCriteria.HasAttachments = emailCriteria.HasAttachments;
                existingCriteria.FolderName = emailCriteria.FolderName;
                existingCriteria.MaxResults = emailCriteria.MaxResults;
                existingCriteria.Skip = emailCriteria.Skip;
                existingCriteria.SortBy = emailCriteria.SortBy;
                existingCriteria.SortDescending = emailCriteria.SortDescending;
                existingCriteria.ExcludeFlags = emailCriteria.ExcludeFlags;
                existingCriteria.IncludeFlags = emailCriteria.IncludeFlags;
                existingCriteria.UpdatedAt = DateTime.UtcNow;
                
                _session.Update(existingCriteria);
                await _session.SaveChangesAsync(cancellationToken);
                return existingCriteria;
            }
            else
            {
                // Create new criteria
                if (emailCriteria.Id == Guid.Empty)
                {
                    emailCriteria.Id = Guid.NewGuid();
                    emailCriteria.CreatedAt = DateTime.UtcNow;
                }

                _session.Store(emailCriteria);
                await _session.SaveChangesAsync(cancellationToken);
                return emailCriteria;
            }
        }

        public async Task<bool> DeleteEmailCriteriaAsync(string userId, CancellationToken cancellationToken = default)
        {
            var criteria = await GetEmailCriteriaAsync(userId, cancellationToken);
            if (criteria == null)
                return false;

            _session.Delete(criteria);
            await _session.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}