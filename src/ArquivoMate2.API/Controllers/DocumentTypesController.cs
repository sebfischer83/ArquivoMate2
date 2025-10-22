using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/document-types")]
    public class DocumentTypesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DocumentTypesController> _logger;

        public DocumentTypesController(IMediator mediator, ICurrentUserService currentUserService, ILogger<DocumentTypesController> logger)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<DocumentTypeDto>>))]
        public async Task<ActionResult<ApiResponse<List<DocumentTypeDto>>>> GetAsync(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var result = await _mediator.Send(new ArquivoMate2.Application.Queries.DocumentTypes.ListDocumentTypesQuery(userId), cancellationToken);
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<DocumentTypeDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<DocumentTypeDto>>> CreateAsync([FromBody] CreateDocumentTypeRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Document type name is required." });
            }

            var userId = _currentUserService.UserId;
            var systemFeatures = request.SystemFeatures != null && request.SystemFeatures.Count > 0 ? request.SystemFeatures : new List<string>();
            var userDefinedFunctions = request.UserDefinedFunctions != null && request.UserDefinedFunctions.Count > 0 ? request.UserDefinedFunctions : new List<string>();

            var result = await _mediator.Send(new ArquivoMate2.Application.Commands.DocumentTypes.CreateDocumentTypeCommand(userId, request.Name, systemFeatures, userDefinedFunctions), cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            _logger.LogInformation("User {UserId} created document type {DocumentTypeName}", userId, request.Name.Trim());

            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<DocumentTypeDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<DocumentTypeDto>>> UpdateAsync(Guid id, [FromBody] UpdateDocumentTypeRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Document type name is required." });
            }

            var userId = _currentUserService.UserId;
            var systemFeatures = request.SystemFeatures != null && request.SystemFeatures.Count > 0 ? request.SystemFeatures : new List<string>();
            var userDefinedFunctions = request.UserDefinedFunctions != null && request.UserDefinedFunctions.Count > 0 ? request.UserDefinedFunctions : new List<string>();

            var result = await _mediator.Send(new ArquivoMate2.Application.Commands.DocumentTypes.UpdateDocumentTypeCommand(id, userId, request.Name, systemFeatures, userDefinedFunctions), cancellationToken);

            if (!result.Success)
            {
                if (result.Message == "Document type not found.") return NotFound(result);
                return BadRequest(result);
            }

            _logger.LogInformation("User {UserId} updated document type {DocumentTypeId} to name {DocumentTypeName}", userId, id, request.Name.Trim());

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            try
            {
                var success = await _mediator.Send(new ArquivoMate2.Application.Commands.DocumentTypes.DeleteDocumentTypeCommand(id, userId), cancellationToken);
                if (!success) return NotFound();
                _logger.LogInformation("User {UserId} deleted document type {DocumentTypeId}", userId, id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse { Success = false, Message = ex.Message });
            }
        }
    }
}
