using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ArquivoMate2.Application.Interfaces;
using Marten;
using ArquivoMate2.Domain.Features;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels;
using System;
using MediatR;

namespace ArquivoMate2.API.Controllers.Feature
{
    [ApiController]
    public abstract class FeatureController : ControllerBase
    {
        protected readonly IMediator _mediator;

        protected FeatureController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Derived controllers must provide the feature key they represent.
        /// </summary>
        protected abstract string FeatureKey { get; }

        /// <summary>
        /// Exposes the feature processing status for the current controller's FeatureKey.
        /// Route: [ControllerRoute]/status/{documentId}
        /// </summary>
        [HttpGet("status/{documentId:guid}")]
        [ProducesResponseType(200, Type = typeof(ApiResponse<DocumentFeatureProcessingDto>))]
        public async Task<ActionResult<ApiResponse<DocumentFeatureProcessingDto>>> GetFeatureStatus(Guid documentId, CancellationToken cancellationToken)
        {
            var dto = await _mediator.Send(new ArquivoMate2.Application.Queries.Features.GetDocumentFeatureProcessingQuery(documentId, FeatureKey), cancellationToken);
            var response = new ApiResponse<DocumentFeatureProcessingDto>(dto);
            return Ok(response);
        }

        /// <summary>
        /// Restart processing of a feature for a document when it previously failed.
        /// PUT /restart/{documentId}
        /// </summary>
        [HttpPut("restart/{documentId:guid}")]
        [ProducesResponseType(200)]
        public async Task<ActionResult> RestartFeatureProcessing(Guid documentId, CancellationToken cancellationToken)
        {
            var ok = await _mediator.Send(new ArquivoMate2.Application.Commands.Features.RestartDocumentFeatureProcessingCommand(documentId, FeatureKey));
            if (!ok) return NotFound();
            return Ok();
        }
    }
}
