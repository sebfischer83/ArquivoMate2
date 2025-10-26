using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace ArquivoMate2.API.Controllers.Feature
{
    [ApiController]
    [Authorize]
    [Route("api/feature/labresults")]
    public class LabResultFeatureController : FeatureController
    {
        private readonly IMediator _mediator;
        private readonly IQuerySession _querySession;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDocumentAccessService _documentAccessService;
        private readonly ArquivoMate2.Application.Interfaces.Sharing.IDocumentOwnershipLookup _ownershipLookup;

        public LabResultFeatureController(IMediator mediator, IQuerySession querySession, ICurrentUserService currentUserService, IDocumentAccessService documentAccessService, ArquivoMate2.Application.Interfaces.Sharing.IDocumentOwnershipLookup ownershipLookup)
            : base(mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _querySession = querySession ?? throw new ArgumentNullException(nameof(querySession));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _documentAccessService = documentAccessService ?? throw new ArgumentNullException(nameof(documentAccessService));
            _ownershipLookup = ownershipLookup ?? throw new ArgumentNullException(nameof(ownershipLookup));
        }

        protected override string FeatureKey => "lab-results";

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

        [HttpPut]
        [ProducesResponseType(200)]
        public async Task<ActionResult> UpdateLabResult([FromBody] LabResultDto dto, CancellationToken cancellationToken)
        {
            // verify access to document
            var userId = _currentUserService.UserId;
            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(dto.DocumentId, userId, cancellationToken);
            if (!hasAccess) return NotFound();

            var ok = await _mediator.Send(new ArquivoMate2.Application.Commands.LabResults.UpdateLabResultCommand(dto), cancellationToken);
            if (!ok) return NotFound();

            // rebuild pivot for owner
            var owner = await _ownershipLookup.GetAsync(dto.DocumentId, cancellationToken);
            if (owner.HasValue && !owner.Value.Deleted)
            {
                await _mediator.Send(new ArquivoMate2.Application.Commands.LabResults.RebuildLabPivotForOwnerCommand(owner.Value.UserId), cancellationToken);
            }

            return Ok();
        }

        [HttpDelete("{labResultId:guid}")]
        [ProducesResponseType(200)]
        public async Task<ActionResult> DeleteLabResult(Guid labResultId, CancellationToken cancellationToken)
        {
            // find lab result to get document id
            var lr = await _mediator.Send(new ArquivoMate2.Application.Queries.LabResults.GetLabResultByIdQuery(labResultId), cancellationToken);
            if (lr == null) return NotFound();

            var userId = _currentUserService.UserId;
            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(lr.DocumentId, userId, cancellationToken);
            if (!hasAccess) return NotFound();

            var ok = await _mediator.Send(new ArquivoMate2.Application.Commands.LabResults.DeleteLabResultCommand(labResultId), cancellationToken);
            if (!ok) return NotFound();

            var owner = await _ownershipLookup.GetAsync(lr.DocumentId, cancellationToken);
            if (owner.HasValue && !owner.Value.Deleted)
            {
                await _mediator.Send(new ArquivoMate2.Application.Commands.LabResults.RebuildLabPivotForOwnerCommand(owner.Value.UserId), cancellationToken);
            }

            return Ok();
        }

        [HttpGet("pivot/{documentId:guid}")]
        [ProducesResponseType(200, Type = typeof(ApiResponse<LabPivotTableDto>))]
        public async Task<ActionResult<ApiResponse<LabPivotTableDto>>> GetPivotForDocument(Guid documentId, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var hasAccess = await _documentAccessService.HasAccessToDocumentAsync(documentId, userId, cancellationToken);
            if (!hasAccess)
            {
                return NotFound();
            }

            var pivot = await _mediator.Send(new ArquivoMate2.Application.Queries.LabResults.GetLabPivotByDocumentQuery(documentId), cancellationToken);
            var response = new ApiResponse<LabPivotTableDto>(pivot);
            return Ok(response);
        }

        [HttpGet("pivot/owner")]
        [ProducesResponseType(200, Type = typeof(ApiResponse<LabPivotTableDto>))]
        public async Task<ActionResult<ApiResponse<LabPivotTableDto>>> GetPivotForCurrentUser(CancellationToken cancellationToken)
        {
            var ownerId = _currentUserService.UserId;
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return NotFound();
            }

            var pivot = await _mediator.Send(new ArquivoMate2.Application.Queries.LabResults.GetLabPivotByOwnerQuery(ownerId), cancellationToken);
            var response = new ApiResponse<LabPivotTableDto>(pivot);
            return Ok(response);
        }
    }
}
