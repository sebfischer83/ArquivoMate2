using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Controllers.Feature
{
    [ApiController]
    [Authorize]
    [Route("api/feature/labresults")]
    public class LabResultFeatureController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDocumentAccessService _documentAccessService;

        public LabResultFeatureController(IMediator mediator, ICurrentUserService currentUserService, IDocumentAccessService documentAccessService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
            _documentAccessService = documentAccessService;
        }

        [HttpGet("{documentId:guid}")]
        [ProducesResponseType(200, Type = typeof(ApiResponse<System.Collections.Generic.List<LabResultDto>>))]
        public async Task<ActionResult<ApiResponse<System.Collections.Generic.List<LabResultDto>>>> GetByDocument(Guid documentId, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(documentId, userId, cancellationToken);
            if (!hasAccess)
            {
                return NotFound();
            }

            var data = await _mediator.Send(new ArquivoMate2.Application.Queries.LabResults.GetLabResultsByDocumentQuery(documentId), cancellationToken);
            var response = new ApiResponse<System.Collections.Generic.List<LabResultDto>>(data);
            return Ok(response);
        }
    }
}
