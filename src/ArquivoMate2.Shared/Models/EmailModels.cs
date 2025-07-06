using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    /// <summary>
    /// Email message model
    /// </summary>
    public class EmailMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public List<string> To { get; set; } = new();
        public List<string> Cc { get; set; } = new();
        public List<string> Bcc { get; set; } = new();
        public DateTime Date { get; set; }
        public string Body { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool HasAttachments { get; set; }
        public List<EmailAttachment> Attachments { get; set; } = new();
        public int Size { get; set; }
        public string FolderName { get; set; } = string.Empty;

        /// <summary>
        /// IMAP UID of the email (for IMAP operations like moving, flagging)
        /// </summary>
        public uint Uid { get; set; }

        /// <summary>
        /// IMAP flags associated with this email (including custom flags)
        /// </summary>
        public List<string> Flags { get; set; } = new();
    }

    /// <summary>
    /// Email attachment model
    /// </summary>
    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int Size { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Email search criteria model
    /// </summary>
    public class EmailCriteria
    {
        public string? SubjectContains { get; set; }
        public string? FromContains { get; set; }
        public string? ToContains { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? IsRead { get; set; }
        public bool? HasAttachments { get; set; }
        public string? FolderName { get; set; } = "INBOX";
        public int MaxResults { get; set; } = 100;
        public int Skip { get; set; } = 0;
        public EmailSortBy SortBy { get; set; } = EmailSortBy.Date;
        public bool SortDescending { get; set; } = true;

        /// <summary>
        /// Maximum number of days to search back in time. If set, this will automatically calculate DateFrom.
        /// Takes precedence only if DateFrom is null. Default is 30 days.
        /// </summary>
        public int? MaxDaysBack { get; set; } = 30;

        /// <summary>
        /// Exclude emails that have any of these flags (e.g., "Processed", "Archived")
        /// </summary>
        public List<string>? ExcludeFlags { get; set; }

        /// <summary>
        /// Only include emails that have all of these flags
        /// </summary>
        public List<string>? IncludeFlags { get; set; }

        /// <summary>
        /// Gets the effective DateFrom value, either from the explicit DateFrom property 
        /// or calculated from MaxDaysBack if DateFrom is null.
        /// </summary>
        /// <returns>The effective start date for email search, or null if neither is set</returns>
        public DateTime? GetEffectiveDateFrom()
        {
            // DateFrom has priority if explicitly set
            if (DateFrom.HasValue)
                return DateFrom.Value;

            // Fall back to MaxDaysBack calculation
            if (MaxDaysBack.HasValue && MaxDaysBack.Value > 0)
                return DateTime.UtcNow.AddDays(-MaxDaysBack.Value);

            return null;
        }
    }
}