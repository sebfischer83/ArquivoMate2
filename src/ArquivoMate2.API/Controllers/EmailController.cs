using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using MailKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailServiceFactory _emailServiceFactory;
        private readonly IEmailSettingsRepository _emailSettingsRepository;
        private readonly ICurrentUserService _currentUserService;

        public EmailController(
            IEmailServiceFactory emailServiceFactory,
            IEmailSettingsRepository emailSettingsRepository,
            ICurrentUserService currentUserService)
        {
            _emailServiceFactory = emailServiceFactory;
            _emailSettingsRepository = emailSettingsRepository;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Retrieves emails based on specified criteria
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<IEnumerable<EmailMessage>>> SearchEmails([FromBody] EmailCriteria criteria, CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                var emails = await emailService.GetEmailsAsync(criteria, cancellationToken);
                return Ok(emails);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve emails", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets the total count of emails in the mailbox
        /// </summary>
        [HttpGet("count")]
        public async Task<ActionResult<int>> GetEmailCount(CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                var count = await emailService.GetEmailCountAsync(cancellationToken);
                return Ok(count);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to get email count", error = ex.Message });
            }
        }

        /// <summary>
        /// Tests the email connection with current settings
        /// </summary>
        [HttpPost("test-connection")]
        public async Task<ActionResult<bool>> TestConnection(CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                var result = await emailService.TestConnectionAsync(cancellationToken);
                return Ok(new { success = result, message = result ? "Connection successful" : "Connection failed" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gets the current user's email settings
        /// </summary>
        [HttpGet("settings")]
        public async Task<ActionResult<EmailSettings>> GetEmailSettings(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var settings = await _emailSettingsRepository.GetEmailSettingsAsync(userId, cancellationToken);
                if (settings == null)
                {
                    return NotFound(new { message = "Email settings not found" });
                }

                // Remove password from response for security
                var safeSettings = new
                {
                    settings.Id,
                    settings.UserId,
                    settings.ProviderType,
                    settings.Server,
                    settings.Port,
                    settings.UseSsl,
                    settings.Username,
                    settings.DisplayName,
                    settings.IsActive,
                    settings.CreatedAt,
                    settings.UpdatedAt,
                    settings.ConnectionTimeout,
                    settings.DefaultFolder,
                    settings.AutoReconnect
                };

                return Ok(safeSettings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve email settings", error = ex.Message });
            }
        }

        /// <summary>
        /// Saves or updates email settings for the current user
        /// </summary>
        [HttpPost("settings")]
        public async Task<ActionResult> SaveEmailSettings([FromBody] SaveEmailSettingsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var emailSettings = new EmailSettings
                {
                    UserId = userId,
                    ProviderType = request.ProviderType,
                    Server = request.Server,
                    Port = request.Port,
                    UseSsl = request.UseSsl,
                    Username = request.Username,
                    Password = request.Password,
                    DisplayName = request.DisplayName ?? request.Username,
                    IsActive = true,
                    ConnectionTimeout = request.ConnectionTimeout ?? 30000,
                    DefaultFolder = request.DefaultFolder ?? "INBOX",
                    AutoReconnect = request.AutoReconnect ?? true
                };

                await _emailSettingsRepository.SaveEmailSettingsAsync(emailSettings, cancellationToken);
                return Ok(new { message = "Email settings saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to save email settings", error = ex.Message });
            }
        }

        /// <summary>
        /// Deletes email settings for the current user
        /// </summary>
        [HttpDelete("settings")]
        public async Task<ActionResult> DeleteEmailSettings(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                await _emailSettingsRepository.DeleteEmailSettingsAsync(userId, cancellationToken);
                return Ok(new { message = "Email settings deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete email settings", error = ex.Message });
            }
        }

        /// <summary>
        /// Moves an email to a different folder and sets a custom IMAP flag
        /// </summary>
        [HttpPost("move-with-flag")]
        public async Task<ActionResult> MoveEmailWithFlag([FromBody] MoveEmailWithFlagRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                
                await emailService.MoveEmailWithFlagAsync(
                    request.SourceFolderName,
                    request.DestinationFolderName,
                    request.Uid,
                    request.CustomFlag,
                    cancellationToken);

                return Ok(new { message = "Email moved successfully and custom flag set" });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to move email", error = ex.Message });
            }
        }

        /// <summary>
        /// Searches for emails excluding those with specific flags (e.g., already processed emails)
        /// </summary>
        [HttpPost("search-unprocessed")]
        public async Task<ActionResult<IEnumerable<EmailMessage>>> SearchUnprocessedEmails([FromBody] SearchUnprocessedEmailsRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                
                var criteria = new EmailCriteria
                {
                    FolderName = request.FolderName ?? "INBOX",
                    MaxResults = request.MaxResults,
                    Skip = request.Skip,
                    SortDescending = request.SortDescending,
                    ExcludeFlags = request.ExcludeFlags, // E.g., ["Processed", "Archived"]
                    SubjectContains = request.SubjectContains,
                    FromContains = request.FromContains,
                    DateFrom = request.DateFrom,
                    DateTo = request.DateTo
                };

                var emails = await emailService.GetEmailsAsync(criteria, cancellationToken);
                return Ok(emails);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve unprocessed emails", error = ex.Message });
            }
        }
    }

    public class MoveEmailWithFlagRequest
    {
        [Required]
        public string SourceFolderName { get; set; } = string.Empty;

        [Required]
        public string DestinationFolderName { get; set; } = string.Empty;

        [Required]
        public uint Uid { get; set; }

        [Required]
        public string CustomFlag { get; set; } = string.Empty;
    }

    public class SearchUnprocessedEmailsRequest
    {
        public string? FolderName { get; set; } = "INBOX";
        public int MaxResults { get; set; } = 100;
        public int Skip { get; set; } = 0;
        public bool SortDescending { get; set; } = true;
        public List<string>? ExcludeFlags { get; set; }
        public string? SubjectContains { get; set; }
        public string? FromContains { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
}