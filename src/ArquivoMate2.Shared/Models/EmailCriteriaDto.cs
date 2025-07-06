using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    /// <summary>
    /// DTO for email criteria API responses
    /// </summary>
    public class EmailCriteriaDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Email search criteria properties
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
        public List<string>? ExcludeFlags { get; set; }
        public List<string>? IncludeFlags { get; set; }

        /// <summary>
        /// Maximum number of days to search back in time. If set, this will automatically calculate DateFrom.
        /// Takes precedence only if DateFrom is null. Default is 30 days.
        /// </summary>
        public int? MaxDaysBack { get; set; } = 30;
    }

    public enum EmailSortBy
    {
        Date,
        Subject,
        From,
        Size
    }
}