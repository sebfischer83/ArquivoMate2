using ArquivoMate2.Shared.Models;
using System;
using System.Collections.Generic;

namespace ArquivoMate2.Domain.Email
{
    /// <summary>
    /// Domain model for user-specific email search criteria
    /// </summary>
    public class EmailCriteria
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; // User-friendly name for the criteria
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
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

        /// <summary>
        /// Converts this Domain EmailCriteria to a Shared EmailCriteria for service operations
        /// </summary>
        /// <returns>Shared EmailCriteria with all search properties mapped</returns>
        public ArquivoMate2.Shared.Models.EmailCriteria ToSharedEmailCriteria()
        {
            return new ArquivoMate2.Shared.Models.EmailCriteria
            {
                SubjectContains = this.SubjectContains,
                FromContains = this.FromContains,
                ToContains = this.ToContains,
                DateFrom = this.DateFrom,
                DateTo = this.DateTo,
                IsRead = this.IsRead,
                HasAttachments = this.HasAttachments,
                FolderName = this.FolderName,
                MaxResults = this.MaxResults,
                Skip = this.Skip,
                SortBy = this.SortBy,
                SortDescending = this.SortDescending,
                MaxDaysBack = this.MaxDaysBack,
                ExcludeFlags = this.ExcludeFlags,
                IncludeFlags = this.IncludeFlags
            };
        }

        /// <summary>
        /// Creates a default EmailCriteria for document processing with sensible defaults
        /// </summary>
        /// <returns>Default Shared EmailCriteria suitable for document processing</returns>
        public static ArquivoMate2.Shared.Models.EmailCriteria CreateDefaultForDocumentProcessing()
        {
            return new ArquivoMate2.Shared.Models.EmailCriteria
            {
                ExcludeFlags = new List<string> { "Processed" },
                MaxResults = 50,
                SubjectContains = "Document",
                MaxDaysBack = 7, // Only look back 7 days for document processing by default
                FolderName = "INBOX",
                SortBy = EmailSortBy.Date,
                SortDescending = true
            };
        }
    }
}