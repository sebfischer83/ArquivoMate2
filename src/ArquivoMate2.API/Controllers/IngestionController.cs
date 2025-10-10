using ArquivoMate2.API.Filters;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Shared.ApiModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Route("api/ingestion")]
    [ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
    public class IngestionController : ControllerBase
    {
        private readonly IIngestionProvider _ingestionProvider;
        private readonly ILogger<IngestionController> _logger;

        public IngestionController(IIngestionProvider ingestionProvider, ILogger<IngestionController> logger)
        {
            _ingestionProvider = ingestionProvider;
            _logger = logger;
        }

        /// <summary>
        /// Allows API key authenticated clients to drop files into the ingestion directory.
        /// </summary>
        [HttpPost("files")]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(ApiResponse<string>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<string>>> UploadAsync([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<string>(default, success: false, message: "File payload is empty."));
            }

            if (!HttpContext.Items.TryGetValue(nameof(UserProfile), out var userObj) || userObj is not UserProfile user)
            {
                return Unauthorized();
            }

            await using var stream = file.OpenReadStream();
            var storedPath = await _ingestionProvider.SaveIncomingFileAsync(user.Id, file.FileName, stream, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Queued ingestion file {File} for user {UserId} via API.", file.FileName, user.Id);

            return Accepted(new ApiResponse<string>(storedPath, message: "File accepted for ingestion."));
        }
    }
}
