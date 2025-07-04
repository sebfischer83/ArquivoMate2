using System;

namespace ArquivoMate2.Domain.Email
{
    /// <summary>
    /// Domain entity representing a processed email to prevent duplicate processing
    /// </summary>
    public class ProcessedEmail
    {
        /// <summary>
        /// Unique identifier for this processed email record
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The IMAP UID of the processed email
        /// </summary>
        public uint EmailUid { get; set; }

        /// <summary>
        /// The user ID who owns this email account
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Email message ID from the email headers
        /// </summary>
        public string EmailMessageId { get; set; } = string.Empty;

        /// <summary>
        /// Email subject for reference
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Email sender for reference
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Processing status of the email
        /// </summary>
        public EmailProcessingStatus Status { get; set; }

        /// <summary>
        /// When the email was processed
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Document ID if a document was created from this email
        /// </summary>
        public Guid? DocumentId { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Source folder where the email was found
        /// </summary>
        public string SourceFolder { get; set; } = string.Empty;

        /// <summary>
        /// Destination folder where the email was moved (if applicable)
        /// </summary>
        public string? DestinationFolder { get; set; }
    }

    /// <summary>
    /// Status of email processing
    /// </summary>
    public enum EmailProcessingStatus
    {
        /// <summary>
        /// Email processing was successful
        /// </summary>
        Success = 0,

        /// <summary>
        /// Email had no attachments and was moved without document creation
        /// </summary>
        NoAttachments = 1,

        /// <summary>
        /// Email processing failed due to an error
        /// </summary>
        Failed = 2,

        /// <summary>
        /// Email was skipped (e.g., already processed, filtered out)
        /// </summary>
        Skipped = 3
    }
}