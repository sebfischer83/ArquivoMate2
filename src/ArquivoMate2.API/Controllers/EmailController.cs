using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels;
using AutoMapper;
using MailKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using DomainEmailCriteria = ArquivoMate2.Domain.Email.EmailCriteria;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailServiceFactory _emailServiceFactory;
        private readonly IEmailSettingsRepository _emailSettingsRepository;
        private readonly IEmailCriteriaRepository _emailCriteriaRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public EmailController(
            IEmailServiceFactory emailServiceFactory,
            IEmailSettingsRepository emailSettingsRepository,
            IEmailCriteriaRepository emailCriteriaRepository,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _emailServiceFactory = emailServiceFactory;
            _emailSettingsRepository = emailSettingsRepository;
            _emailCriteriaRepository = emailCriteriaRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        /// <summary>
        /// Gets the total count of emails in the mailbox
        /// </summary>
        [HttpGet("count")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<int>))]
        public async Task<ActionResult<ApiResponse<int>>> GetEmailCount(CancellationToken cancellationToken = default)
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        public async Task<ActionResult<ApiResponse<object>>> TestConnection(CancellationToken cancellationToken = default)
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        public async Task<ActionResult<ApiResponse<object>>> GetEmailSettings(CancellationToken cancellationToken = default)
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        public async Task<ActionResult<ApiResponse<object>>> SaveEmailSettings([FromBody] SaveEmailSettingsRequest request, CancellationToken cancellationToken = default)
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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        public async Task<ActionResult<ApiResponse<object>>> DeleteEmailSettings(CancellationToken cancellationToken = default)
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

        #region Email Criteria Management

        /// <summary>
        /// Gets the email criteria for the current user
        /// </summary>
        [HttpGet("criteria")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EmailCriteriaDto>))]
        public async Task<ActionResult<ApiResponse<EmailCriteriaDto>>> GetEmailCriteria(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var criteria = await _emailCriteriaRepository.GetEmailCriteriaAsync(userId, cancellationToken);
                if (criteria == null)
                {
                    return NotFound(new { message = "Email criteria not found" });
                }

                var criteriaDto = _mapper.Map<EmailCriteriaDto>(criteria);
                return Ok(criteriaDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve email criteria", error = ex.Message });
            }
        }

        /// <summary>
        /// Saves (creates or updates) email criteria for the current user
        /// </summary>
        [HttpPost("criteria")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EmailCriteriaDto>))]
        public async Task<ActionResult<ApiResponse<EmailCriteriaDto>>> SaveEmailCriteria([FromBody] SaveEmailCriteriaRequest request, CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Name is required" });
            }

            try
            {
                var criteria = _mapper.Map<DomainEmailCriteria>(request);
                criteria.UserId = userId;

                var savedCriteria = await _emailCriteriaRepository.SaveEmailCriteriaAsync(criteria, cancellationToken);
                var criteriaDto = _mapper.Map<EmailCriteriaDto>(savedCriteria);

                return Ok(criteriaDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to save email criteria", error = ex.Message });
            }
        }

        /// <summary>
        /// Deletes email criteria for the current user
        /// </summary>
        [HttpDelete("criteria")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))]
        public async Task<ActionResult<ApiResponse<object>>> DeleteEmailCriteria(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                var deleted = await _emailCriteriaRepository.DeleteEmailCriteriaAsync(userId, cancellationToken);
                if (!deleted)
                {
                    return NotFound(new { message = "Email criteria not found" });
                }

                return Ok(new { message = "Email criteria deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete email criteria", error = ex.Message });
            }
        }

        #endregion
    }
}