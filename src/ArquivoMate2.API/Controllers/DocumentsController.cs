using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Shared.Models;
using Hangfire;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(IMediator mediator, IWebHostEnvironment env)
        {
            _mediator = mediator;
            _env = env;
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            var uploads = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var filePath = Path.Combine(uploads, request.File.FileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await request.File.CopyToAsync(stream, cancellationToken);

            var id = await _mediator.Send(new UploadDocumentCommand(filePath), cancellationToken);

            // Automatically enqueue processing after successful upload
            BackgroundJob.Enqueue<DocumentProcessingService>(svc => svc.ProcessAsync(id));

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

