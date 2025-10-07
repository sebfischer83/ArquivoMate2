using ArquivoMate2.API.Results;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Sharing;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Route("api/share")]
    public class PublicShareController : ControllerBase
    {
        private readonly IExternalShareService _shareService;
        private readonly IFileAccessTokenService _tokenService;
        private readonly IDocumentArtifactStreamer _streamer;

        public PublicShareController(IExternalShareService shareService, IFileAccessTokenService tokenService, IDocumentArtifactStreamer streamer)
        {
            _shareService = shareService;
            _tokenService = tokenService;
            _streamer = streamer;
        }

        /// <summary>
        /// Streams a shared document artifact to anonymous callers with a valid share token.
        /// </summary>
        /// <param name="shareId">Identifier of the public share that should be accessed.</param>
        /// <param name="token">Share access token that validates the request.</param>
        /// <param name="ct">Cancellation token forwarded from the HTTP request.</param>
        [HttpGet("{shareId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid shareId, [FromQuery] string token, CancellationToken ct)
        {
            if (!_tokenService.TryValidateShareToken(token, out var tShare, out var exp) || tShare != shareId)
                return NotFound();
            if (DateTimeOffset.UtcNow > exp) return NotFound();

            var share = await _shareService.GetAsync(shareId, ct);
            if (share == null || share.Revoked || share.ExpiresAtUtc < DateTime.UtcNow)
                return NotFound();

            try
            {
                var (writeAsync, contentType) = await _streamer.GetAsync(share.DocumentId, share.Artifact, ct);
                return new PushStreamResult(contentType, writeAsync);
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
