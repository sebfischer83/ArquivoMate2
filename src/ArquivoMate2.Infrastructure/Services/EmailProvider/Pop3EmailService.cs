using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Domain.Email;
using MailKit.Net.Pop3;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.EmailProvider
{
    public class Pop3EmailService : IEmailService, IDisposable
    {
        private readonly Pop3Client _client;
        private readonly EmailSettings _settings;
        private readonly ILogger<Pop3EmailService> _logger;
        private bool _disposed = false;

        public EmailProviderType ProviderType => EmailProviderType.POP3;

        public string UserId => _settings.UserId ?? string.Empty;

        public Pop3EmailService(EmailSettings settings, ILogger<Pop3EmailService> logger)
        {
            _client = new Pop3Client();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;
        }

        public async Task<IEnumerable<EmailMessage>> GetEmailsAsync(ArquivoMate2.Shared.Models.EmailCriteria criteria, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var messages = new List<EmailMessage>();
            var count = _client.Count;

            // Apply pagination
            var startIndex = Math.Max(0, count - criteria.Skip - criteria.MaxResults);
            var endIndex = Math.Max(0, count - criteria.Skip);

            for (int i = startIndex; i < endIndex; i++)
            {
                try
                {
                    var message = await _client.GetMessageAsync(i, cancellationToken);
                    var emailMessage = ConvertToEmailMessage(message, i);

                    // Apply client-side filtering since POP3 doesn't support server-side search
                    if (MatchesCriteria(emailMessage, criteria))
                    {
                        messages.Add(emailMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while retrieving message at index {Index}", i);
                    continue;
                }
            }

            // Apply sorting
            messages = ApplySorting(messages, criteria).ToList();

            return messages;
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var testClient = new Pop3Client();
                await testClient.ConnectAsync(_settings.Server, _settings.Port, _settings.UseSsl, cancellationToken);
                await testClient.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
                await testClient.DisconnectAsync(true, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while testing connection");
                return false;
            }
        }

        public async Task<int> GetEmailCountAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);
            return _client.Count;
        }

        /// <summary>
        /// POP3 does not support moving emails or setting custom flags.
        /// This method throws NotSupportedException.
        /// </summary>
        public Task MoveEmailWithFlagAsync(string sourceFolderName, string destinationFolderName, uint emailUid, string customFlag, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("POP3 protocol does not support moving emails between folders or setting custom flags. Use IMAP instead.");
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_client.IsConnected && _client.IsAuthenticated)
                return;

            if (_client.IsConnected)
                await _client.DisconnectAsync(false, cancellationToken);

            try
            {
                await _client.ConnectAsync(_settings.Server, _settings.Port, _settings.UseSsl, cancellationToken);
                await _client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while connecting to POP3 server");
                throw;
            }
        }

        private bool MatchesCriteria(EmailMessage message, ArquivoMate2.Shared.Models.EmailCriteria criteria)
        {
            if (!string.IsNullOrEmpty(criteria.SubjectContains) && 
                !message.Subject.Contains(criteria.SubjectContains, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(criteria.FromContains) && 
                !message.From.Contains(criteria.FromContains, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(criteria.ToContains) && 
                !message.To.Any(to => to.Contains(criteria.ToContains, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Use the effective DateFrom (either explicit DateFrom or calculated from MaxDaysBack)
            var effectiveDateFrom = criteria.GetEffectiveDateFrom();
            if (effectiveDateFrom.HasValue && message.Date < effectiveDateFrom.Value)
                return false;

            if (criteria.DateTo.HasValue && message.Date > criteria.DateTo.Value)
                return false;

            if (criteria.IsRead.HasValue && message.IsRead != criteria.IsRead.Value)
                return false;

            if (criteria.HasAttachments.HasValue && message.HasAttachments != criteria.HasAttachments.Value)
                return false;

            // Flag-based filtering (POP3 has limited flag support)
            if (criteria.ExcludeFlags?.Count > 0)
            {
                foreach (var excludeFlag in criteria.ExcludeFlags)
                {
                    if (message.Flags.Contains(excludeFlag))
                        return false;
                }
            }

            if (criteria.IncludeFlags?.Count > 0)
            {
                foreach (var includeFlag in criteria.IncludeFlags)
                {
                    if (!message.Flags.Contains(includeFlag))
                        return false;
                }
            }

            return true;
        }

        private IEnumerable<EmailMessage> ApplySorting(IEnumerable<EmailMessage> messages, ArquivoMate2.Shared.Models.EmailCriteria criteria)
        {
            var sorted = criteria.SortBy switch
            {
                EmailSortBy.Date => messages.OrderBy(m => m.Date),
                EmailSortBy.Subject => messages.OrderBy(m => m.Subject),
                EmailSortBy.From => messages.OrderBy(m => m.From),
                EmailSortBy.Size => messages.OrderBy(m => m.Size),
                _ => messages.OrderBy(m => m.Date)
            };

            return criteria.SortDescending ? sorted.Reverse() : sorted;
        }

        private EmailMessage ConvertToEmailMessage(MimeMessage message, int index)
        {
            var emailMessage = new EmailMessage
            {
                MessageId = message.MessageId ?? index.ToString(),
                Subject = message.Subject ?? string.Empty,
                From = message.From.ToString(),
                To = message.To.Select(x => x.ToString()).ToList(),
                Cc = message.Cc.Select(x => x.ToString()).ToList(),
                Date = message.Date.DateTime,
                Body = message.TextBody ?? string.Empty,
                BodyHtml = message.HtmlBody ?? string.Empty,
                FolderName = "INBOX", // POP3 only has one mailbox
                Size = (int)(message.Headers.Count * 50), // Rough estimate
                Flags = new List<string>() // POP3 doesn't support custom flags
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