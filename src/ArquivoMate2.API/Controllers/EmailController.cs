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

    }
}