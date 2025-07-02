using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Models
{
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
    }

    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int Size { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

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
    }

    public enum EmailSortBy
    {
        Date,
        Subject,
        From,
        Size
    }
}