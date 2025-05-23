﻿using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using Hangfire;
using Marten;
using Marten.Pagination;
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
        private readonly IMapper _mapper;

        public DocumentsController(IMediator mediator, IWebHostEnvironment env, ICurrentUserService currentUserService, IMapper mapper)
        {
            _mediator = mediator;
            _env = env;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        [HttpGet("pending")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<DocumentStatusDto>))]
        public async Task<IActionResult> GetPendingDocuments(CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;

            var pendingDocuments = await querySession.Query<Infrastructure.Persistance.DocumentView>()
                .Where(doc => (doc.Status != ProcessingStatus.Completed && doc.Status != ProcessingStatus.Failed) && doc.UserId == userId)
                .ToListAsync(cancellationToken);

            var result = pendingDocuments.Select(doc => new DocumentStatusDto
            {
                DocumentId = doc.Id,
                Status = doc.Status,
                UploadedAt = doc.UploadedAt
            }).ToList();

            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken cancellationToken, [FromServices] OcrSettings ocrSettings)
        {
            if (request.File is null || request.File.Length == 0)
                return BadRequest();

            var id = await _mediator.Send(new UploadDocumentCommand(request), cancellationToken);

            BackgroundJob.Enqueue<DocumentProcessingService>("documents", svc => svc.ProcessAsync(id, _currentUserService.UserId));

            return CreatedAtAction(nameof(Upload), new { id }, id);
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentListDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([FromQuery] DocumentListRequestDto requestDto, 
            CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.Query<Infrastructure.Persistance.DocumentView>().Where(d => d.UserId == userId).
                ToPagedListAsync(requestDto.Page, requestDto.PageSize);
            if (view is null)
                return NotFound();
            if (view.Count == 0)
                return NotFound();


            DocumentListDto documentListDto = new();
            var documents = _mapper.Map<DocumentListItemDto[]>(view);
            documentListDto.Documents = documents;

            documentListDto.TotalCount = view.TotalItemCount;
            documentListDto.HasNextPage = view.HasNextPage;
            documentListDto.PageCount = view.PageCount;
            documentListDto.HasPreviousPage = view.HasPreviousPage;
            documentListDto.IsLastPage = view.IsLastPage;
            documentListDto.IsFirstPage = view.IsFirstPage;
            documentListDto.CurrentPage = requestDto.Page;

            return Ok(documentListDto);

        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DocumentDto))]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken, [FromServices] IQuerySession querySession)
        {
            var userId = _currentUserService.UserId;
            var view = await querySession.LoadAsync<Infrastructure.Persistance.DocumentView>(id, cancellationToken);
            if (view is null)
                return NotFound();

            if (view.UserId != userId)
                return Forbid();

            var events = await querySession.Events.FetchStreamAsync(id, token: cancellationToken);
            var documentDto = _mapper.Map<DocumentDto>(view);

            return Ok(documentDto);

        }
    }
}

