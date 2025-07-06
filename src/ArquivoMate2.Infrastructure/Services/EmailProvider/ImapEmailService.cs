using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Domain.Email;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<ImapEmailService> _logger;
        private bool _disposed = false;

        public EmailProviderType ProviderType => EmailProviderType.IMAP;

        public string UserId => _settings.UserId ?? string.Empty;

        public ImapEmailService(EmailSettings settings, ILogger<ImapEmailService> logger)
        {
            _client = new ImapClient();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;
        }

        public async Task<IEnumerable<EmailMessage>> GetEmailsAsync(ArquivoMate2.Shared.Models.EmailCriteria criteria, CancellationToken cancellationToken = default)
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
                    
                    // Fetch flags for this specific message
                    var messageInfoList = await folder.FetchAsync(new[] { uid }, MessageSummaryItems.Flags, cancellationToken);
                    var flags = messageInfoList.FirstOrDefault()?.Flags ?? MessageFlags.None;
                    
                    var emailMessage = ConvertToEmailMessage(message, uid, folder.Name, flags);
                    messages.Add(emailMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving email with UID {Uid}", uid);
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

        /// <summary>
        /// Moves an email from the source folder to the destination folder and sets a custom IMAP flag on it.
        /// </summary>
        /// <param name="sourceFolderName">The name of the source folder.</param>
        /// <param name="destinationFolderName">The name of the destination folder.</param>
        /// <param name="emailUid">The UID of the email to move (as uint).</param>
        /// <param name="customFlag">The custom IMAP flag to set (e.g., "Processed").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task MoveEmailWithFlagAsync(string sourceFolderName, string destinationFolderName, uint emailUid, string customFlag, CancellationToken cancellationToken = default)
        {
            var uid = new UniqueId(emailUid);
            await MoveEmailWithFlagAsync(sourceFolderName, destinationFolderName, uid, customFlag, cancellationToken);
        }

        /// <summary>
        /// Internal method that handles the actual moving with UniqueId
        /// </summary>
        private async Task MoveEmailWithFlagAsync(string sourceFolderName, string destinationFolderName, UniqueId uid, string customFlag, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var sourceFolder = await _client.GetFolderAsync(sourceFolderName, cancellationToken);
            await sourceFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            // Move the message to the destination folder
            var destinationFolder = await _client.GetFolderAsync(destinationFolderName, cancellationToken);
            await destinationFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            UniqueId? newUid = null;
            try
            {
                var moveResult = await sourceFolder.MoveToAsync(uid, destinationFolder, cancellationToken);
                newUid = moveResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving email UID {Uid} from {Source} to {Destination}", uid, sourceFolderName, destinationFolderName);
                throw;
            }

            if (newUid.HasValue)
            {
                try
                {
                    await destinationFolder.AddFlagsAsync(newUid.Value, MessageFlags.None, new HashSet<string> { customFlag }, true, cancellationToken);
                    _logger.LogInformation("Successfully moved email UID {OriginalUid} to {Destination} with new UID {NewUid} and set custom flag '{Flag}'", uid, destinationFolderName, newUid.Value, customFlag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting custom flag '{Flag}' on email UID {Uid} in folder {Folder}", customFlag, newUid.Value, destinationFolderName);
                    throw;
                }
            }
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

        private SearchQuery BuildSearchQuery(ArquivoMate2.Shared.Models.EmailCriteria criteria)
        {
            var queries = new List<SearchQuery>();

            if (!string.IsNullOrEmpty(criteria.SubjectContains))
                queries.Add(SearchQuery.SubjectContains(criteria.SubjectContains));

            if (!string.IsNullOrEmpty(criteria.FromContains))
                queries.Add(SearchQuery.FromContains(criteria.FromContains));

            if (!string.IsNullOrEmpty(criteria.ToContains))
                queries.Add(SearchQuery.ToContains(criteria.ToContains));

            // Use the effective DateFrom (either explicit DateFrom or calculated from MaxDaysBack)
            var effectiveDateFrom = criteria.GetEffectiveDateFrom();
            if (effectiveDateFrom.HasValue)
                queries.Add(SearchQuery.DeliveredAfter(effectiveDateFrom.Value));

            if (criteria.DateTo.HasValue)
                queries.Add(SearchQuery.DeliveredBefore(criteria.DateTo.Value));

            if (criteria.IsRead.HasValue)
            {
                if (criteria.IsRead.Value)
                    queries.Add(SearchQuery.Seen);
                else
                    queries.Add(SearchQuery.NotSeen);
            }

            // Add flag-based filtering for IMAP
            // Note: Custom flags are searched using HasKeyword/NotKeyword
            if (criteria.ExcludeFlags?.Count > 0)
            {
                foreach (var flag in criteria.ExcludeFlags)
                {
                    // Handle standard flags differently from custom flags
                    if (flag.StartsWith("\\"))
                    {
                        queries.Add(GetNotFlagQuery(flag));
                    }
                    else
                    {
                        queries.Add(SearchQuery.NotKeyword(flag));
                    }
                }
            }

            if (criteria.IncludeFlags?.Count > 0)
            {
                foreach (var flag in criteria.IncludeFlags)
                {
                    // Handle standard flags differently from custom flags
                    if (flag.StartsWith("\\"))
                    {
                        queries.Add(GetFlagQuery(flag));
                    }
                    else
                    {
                        queries.Add(SearchQuery.HasKeyword(flag));
                    }
                }
            }

            return queries.Count == 0 ? SearchQuery.All : queries.Aggregate(SearchQuery.And);
        }

        private SearchQuery GetFlagQuery(string standardFlag)
        {
            return standardFlag switch
            {
                "\\Seen" => SearchQuery.Seen,
                "\\Answered" => SearchQuery.Answered,
                "\\Flagged" => SearchQuery.Flagged,
                "\\Deleted" => SearchQuery.Deleted,
                "\\Draft" => SearchQuery.Draft,
                "\\Recent" => SearchQuery.Recent,
                _ => SearchQuery.All // Fallback for unknown standard flags
            };
        }

        private SearchQuery GetNotFlagQuery(string standardFlag)
        {
            return standardFlag switch
            {
                "\\Seen" => SearchQuery.NotSeen,
                "\\Answered" => SearchQuery.NotAnswered,
                "\\Flagged" => SearchQuery.NotFlagged,
                "\\Deleted" => SearchQuery.NotDeleted,
                "\\Draft" => SearchQuery.NotDraft,
                "\\Recent" => SearchQuery.NotRecent,
                _ => SearchQuery.All // Fallback for unknown standard flags
            };
        }

        private IEnumerable<UniqueId> ApplySorting(IList<UniqueId> uids, ArquivoMate2.Shared.Models.EmailCriteria criteria)
        {
            return criteria.SortDescending ? uids.Reverse() : uids;
        }

        private EmailMessage ConvertToEmailMessage(MimeMessage message, UniqueId uid, string folderName, MessageFlags flags = MessageFlags.None)
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
                Size = (int)(message.Headers.Count * 50), // Rough estimate
                IsRead = flags.HasFlag(MessageFlags.Seen),
                Uid = uid.Id, // Add the UID to the email message
                Flags = ExtractStandardFlags(flags)
            };

            // Handle attachments
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart mimePart)
                {
                    using var stream = new MemoryStream();
                    mimePart.Content.DecodeTo(stream);

                    emailMessage.Attachments.Add(new EmailAttachment
                    {
                        FileName = mimePart.FileName ?? "unknown",
                        ContentType = mimePart.ContentType.MimeType,
                        Size = (int)(mimePart.Content?.Stream?.Length ?? 0),
                        Content = stream.ToArray()
                    });
                }
            }

            emailMessage.HasAttachments = emailMessage.Attachments.Any();

            return emailMessage;
        }

        private List<string> ExtractStandardFlags(MessageFlags flags)
        {
            var flagList = new List<string>();

            // Standard IMAP flags
            if (flags.HasFlag(MessageFlags.Seen))
                flagList.Add("\\Seen");
            if (flags.HasFlag(MessageFlags.Answered))
                flagList.Add("\\Answered");
            if (flags.HasFlag(MessageFlags.Flagged))
                flagList.Add("\\Flagged");
            if (flags.HasFlag(MessageFlags.Deleted))
                flagList.Add("\\Deleted");
            if (flags.HasFlag(MessageFlags.Draft))
                flagList.Add("\\Draft");
            if (flags.HasFlag(MessageFlags.Recent))
                flagList.Add("\\Recent");

            // Note: Custom flags would need special handling since they're not in MessageFlags enum
            // For full custom flag support, we'd need to use IMAP extensions or raw IMAP commands
            
            return flagList;
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