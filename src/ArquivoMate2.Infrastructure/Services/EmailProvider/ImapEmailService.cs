using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Email;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.EmailProvider
{
    public class ImapEmailService : IEmailService, IDisposable
    {
        private readonly ImapClient _client;
        private readonly EmailSettings _settings;
        private bool _disposed = false;

        public ImapEmailService(EmailSettings settings)
        {
            _client = new ImapClient();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<IEnumerable<EmailMessage>> GetEmailsAsync(EmailCriteria criteria, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var folder = await _client.GetFolderAsync(criteria.FolderName ?? _settings.DefaultFolder, cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            // Build search query
            var searchQuery = BuildSearchQuery(criteria);
            var uids = await folder.SearchAsync(searchQuery, cancellationToken);

            // Apply sorting and pagination
            var sortedUids = ApplySorting(uids, criteria);
            var pagedUids = sortedUids.Skip(criteria.Skip).Take(criteria.MaxResults);

            var messages = new List<EmailMessage>();

            foreach (var uid in pagedUids)
            {
                try
                {
                    var message = await folder.GetMessageAsync(uid, cancellationToken);
                    var emailMessage = ConvertToEmailMessage(message, uid, folder.Name);
                    messages.Add(emailMessage);
                }
                catch (Exception)
                {
                    // Log error and continue with next message
                    continue;
                }
            }

            return messages;
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var testClient = new ImapClient();
                await testClient.ConnectAsync(_settings.Server, _settings.Port, _settings.UseSsl, cancellationToken);
                await testClient.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
                await testClient.DisconnectAsync(true, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> GetEmailCountAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var folder = await _client.GetFolderAsync(_settings.DefaultFolder, cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            return folder.Count;
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client.IsConnected && _client.IsAuthenticated)
                return;

            if (_client.IsConnected)
                await _client.DisconnectAsync(false, cancellationToken);

            await _client.ConnectAsync(_settings.Server, _settings.Port, _settings.UseSsl, cancellationToken);
            await _client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        }

        private SearchQuery BuildSearchQuery(EmailCriteria criteria)
        {
            var queries = new List<SearchQuery>();

            if (!string.IsNullOrEmpty(criteria.SubjectContains))
                queries.Add(SearchQuery.SubjectContains(criteria.SubjectContains));

            if (!string.IsNullOrEmpty(criteria.FromContains))
                queries.Add(SearchQuery.FromContains(criteria.FromContains));

            if (!string.IsNullOrEmpty(criteria.ToContains))
                queries.Add(SearchQuery.ToContains(criteria.ToContains));

            if (criteria.DateFrom.HasValue)
                queries.Add(SearchQuery.DeliveredAfter(criteria.DateFrom.Value));

            if (criteria.DateTo.HasValue)
                queries.Add(SearchQuery.DeliveredBefore(criteria.DateTo.Value));

            if (criteria.IsRead.HasValue)
            {
                if (criteria.IsRead.Value)
                    queries.Add(SearchQuery.Seen);
                else
                    queries.Add(SearchQuery.NotSeen);
            }

            return queries.Count == 0 ? SearchQuery.All : queries.Aggregate(SearchQuery.And);
        }

        private IEnumerable<UniqueId> ApplySorting(IList<UniqueId> uids, EmailCriteria criteria)
        {
            // For IMAP, sorting is typically done server-side, but for simplicity we'll sort client-side
            // In production, you'd want to use IMAP SORT extension if available
            return criteria.SortDescending ? uids.Reverse() : uids;
        }

        private EmailMessage ConvertToEmailMessage(MimeMessage message, UniqueId uid, string folderName)
        {
            var emailMessage = new EmailMessage
            {
                MessageId = message.MessageId ?? uid.ToString(),
                Subject = message.Subject ?? string.Empty,
                From = message.From.ToString(),
                To = message.To.Select(x => x.ToString()).ToList(),
                Cc = message.Cc.Select(x => x.ToString()).ToList(),
                Date = message.Date.DateTime,
                Body = message.TextBody ?? string.Empty,
                BodyHtml = message.HtmlBody ?? string.Empty,
                FolderName = folderName,
                Size = (int)(message.Headers.Count * 50) // Rough estimate
            };

            // Handle attachments
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart mimePart)
                {
                    emailMessage.Attachments.Add(new EmailAttachment
                    {
                        FileName = mimePart.FileName ?? "unknown",
                        ContentType = mimePart.ContentType.MimeType,
                        Size = (int)(mimePart.Content?.Stream?.Length ?? 0)
                    });
                }
            }

            emailMessage.HasAttachments = emailMessage.Attachments.Any();

            return emailMessage;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
}