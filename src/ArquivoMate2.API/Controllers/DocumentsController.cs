using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Shared.Models;
using Hangfire;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _env;
        private readonly ICurrentUserService _currentUserService;

        public DocumentsController(IMediator mediator, IWebHostEnvironment env, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            var id = await _mediator.Send(new UploadDocumentCommand(request), cancellationToken);

            BackgroundJob.Enqueue<DocumentProcessingService>(svc => svc.ProcessAsync(id, _currentUserService.UserId));

            return CreatedAtAction(nameof(Upload), new { id }, null);
        }

        [HttpGet("{id:guid}")]
        //[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            // Dokument aus dem Marten-Read-Model laden
            var view = await querySession.LoadAsync<Infrastructure.Persistance.DocumentView>(id, cancellationToken);
            if (view is null)
                return NotFound();

            // Mapping auf das API-DTO
           
            return Ok("");
        }
    }
}

