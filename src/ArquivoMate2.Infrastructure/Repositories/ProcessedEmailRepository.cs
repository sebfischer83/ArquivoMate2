using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for managing processed email records using Marten
    /// </summary>
    public class ProcessedEmailRepository : IProcessedEmailRepository
    {
        private readonly IDocumentSession _session;

        public ProcessedEmailRepository(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<bool> IsEmailProcessedAsync(uint emailUid, string userId, CancellationToken cancellationToken = default)
        {
            return await _session.Query<ProcessedEmail>()
                .Where(x => x.EmailUid == emailUid && x.UserId == userId)
                .AnyAsync(cancellationToken);
        }

        public async Task SaveProcessedEmailAsync(ProcessedEmail processedEmail, CancellationToken cancellationToken = default)
        {
            if (processedEmail.Id == Guid.Empty)
            {
                processedEmail.Id = Guid.NewGuid();
            }

            processedEmail.ProcessedAt = DateTime.UtcNow;
            _session.Store(processedEmail);
            await _session.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<ProcessedEmail>> GetProcessedEmailsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default)
        {
            return await _session.Query<ProcessedEmail>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ProcessedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProcessedEmail?> GetProcessedEmailAsync(uint emailUid, string userId, CancellationToken cancellationToken = default)
        {
            return await _session.Query<ProcessedEmail>()
                .Where(x => x.EmailUid == emailUid && x.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<HashSet<uint>> GetProcessedEmailUidsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var processedUids = await _session.Query<ProcessedEmail>()
                .Where(x => x.UserId == userId)
                .Select(x => x.EmailUid)
                .ToListAsync(cancellationToken);

            return new HashSet<uint>(processedUids);
        }
    }
}