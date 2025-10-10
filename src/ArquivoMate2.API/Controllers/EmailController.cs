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
                return Ok(new ApiResponse<int>(count, true));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = ex.Message }, false, ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to get email count" }, false, ex.Message));
            }
        }

        /// <summary>
        /// Tests the email connection with current settings
        /// </summary>
        [HttpPost("test-connection")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ConnectionTestResultDto>))]
        public async Task<ActionResult<ApiResponse<ConnectionTestResultDto>>> TestConnection(CancellationToken cancellationToken = default)
        {
            try
            {
                var emailService = await _emailServiceFactory.CreateEmailServiceAsync(cancellationToken);
                var result = await emailService.TestConnectionAsync(cancellationToken);
                var payload = new ConnectionTestResultDto { Success = result, Message = result ? "Connection successful" : "Connection failed" };
                return Ok(new ApiResponse<ConnectionTestResultDto>(payload, true));
            }
            catch (Exception ex)
            {
                var payload = new ConnectionTestResultDto { Success = false, Message = ex.Message };
                return Ok(new ApiResponse<ConnectionTestResultDto>(payload, false, ex.Message));
            }
        }

        /// <summary>
        /// Gets the current user's email settings
        /// </summary>
        [HttpGet("settings")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EmailSettingsDto>))]
        public async Task<ActionResult<ApiResponse<EmailSettingsDto>>> GetEmailSettings(CancellationToken cancellationToken = default)
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
                    return Ok(new ApiResponse<EmailSettingsDto>(null, true));
                }

                var dto = _mapper.Map<EmailSettingsDto>(settings);
                return Ok(new ApiResponse<EmailSettingsDto>(dto, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to retrieve email settings" }, false, ex.Message));
            }
        }

        /// <summary>
        /// Saves or updates email settings for the current user
        /// </summary>
        [HttpPost("settings")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<EmailSettingsDto>))]
        public async Task<ActionResult<ApiResponse<EmailSettingsDto>>> SaveEmailSettings([FromBody] SaveEmailSettingsRequest request, CancellationToken cancellationToken = default)
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
                var dto = _mapper.Map<EmailSettingsDto>(emailSettings);
                return Ok(new ApiResponse<EmailSettingsDto>(dto, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to save email settings" }, false, ex.Message));
            }
        }

        /// <summary>
        /// Deletes email settings for the current user
        /// </summary>
        [HttpDelete("settings")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResponseMessageDto>))]
        public async Task<ActionResult<ApiResponse<ResponseMessageDto>>> DeleteEmailSettings(CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            try
            {
                await _emailSettingsRepository.DeleteEmailSettingsAsync(userId, cancellationToken);
                return Ok(new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Email settings deleted successfully" }, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to delete email settings" }, false, ex.Message));
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
                    return Ok(new ApiResponse<EmailCriteriaDto>(new EmailCriteriaDto(), true));
                }

                var criteriaDto = _mapper.Map<EmailCriteriaDto>(criteria);
                return Ok(new ApiResponse<EmailCriteriaDto>(criteriaDto, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to retrieve email criteria" }, false, ex.Message));
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
                return BadRequest(new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Name is required" }, false, "Name is required"));
            }

            try
            {
                var criteria = _mapper.Map<DomainEmailCriteria>(request);
                criteria.UserId = userId;

                var savedCriteria = await _emailCriteriaRepository.SaveEmailCriteriaAsync(criteria, cancellationToken);
                var criteriaDto = _mapper.Map<EmailCriteriaDto>(savedCriteria);

                return Ok(new ApiResponse<EmailCriteriaDto>(criteriaDto, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to save email criteria" }, false, ex.Message));
            }
        }

        /// <summary>
        /// Deletes email criteria for the current user
        /// </summary>
        [HttpDelete("criteria")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<ResponseMessageDto>))]
        public async Task<ActionResult<ApiResponse<ResponseMessageDto>>> DeleteEmailCriteria(CancellationToken cancellationToken = default)
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
                    return Ok(new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Email criteria not found" }, true));
                }

                return Ok(new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Email criteria deleted successfully" }, true));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResponseMessageDto>(new ResponseMessageDto { Message = "Failed to delete email criteria" }, false, ex.Message));
            }
        }

        #endregion
    }
}