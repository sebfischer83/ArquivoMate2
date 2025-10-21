using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using Marten;
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
        private readonly IDocumentSession _session;
        private readonly IQuerySession _querySession;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DocumentTypesController> _logger;

        public DocumentTypesController(
            IDocumentSession session,
            IQuerySession querySession,
            ICurrentUserService currentUserService,
            ILogger<DocumentTypesController> logger)
        {
            _session = session;
            _querySession = querySession;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<DocumentTypeDto>>))]
        public async Task<ActionResult<ApiResponse<List<DocumentTypeDto>>>> GetAsync(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            var definitions = await _querySession.Query<DocumentTypeDefinition>()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
            var assigned = await _querySession.Query<UserDocumentType>()
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

            var assignedSet = assigned.Select(x => x.DocumentTypeId).ToHashSet();
            var dtos = definitions.Select(def => new DocumentTypeDto
            {
                Id = def.Id,
                Name = def.Name,
                IsLocked = def.IsLocked,
                CreatedAtUtc = def.CreatedAtUtc,
                UpdatedAtUtc = def.UpdatedAtUtc,
                IsAssignedToCurrentUser = assignedSet.Contains(def.Id)
            }).ToList();

            return Ok(new ApiResponse<List<DocumentTypeDto>>(dtos));
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

            var trimmedName = request.Name.Trim();

            var exists = await _querySession.Query<DocumentTypeDefinition>()
                .AnyAsync(x => x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase), cancellationToken);
            if (exists)
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Document type already exists." });
            }

            var definition = new DocumentTypeDefinition
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                IsLocked = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _session.Store(definition);

            var userId = _currentUserService.UserId;
            _session.Store(new UserDocumentType
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DocumentTypeId = definition.Id,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} created document type {DocumentTypeName}", userId, trimmedName);

            var dto = new DocumentTypeDto
            {
                Id = definition.Id,
                Name = definition.Name,
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = true
            };

            return StatusCode(StatusCodes.Status201Created, new ApiResponse<DocumentTypeDto>(dto));
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

            var definition = await _session.Query<DocumentTypeDefinition>()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (definition == null)
            {
                return NotFound(new ApiResponse { Success = false, Message = "Document type not found." });
            }

            if (definition.IsLocked)
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Seeded document types cannot be modified." });
            }

            var trimmedName = request.Name.Trim();
            var duplicate = await _session.Query<DocumentTypeDefinition>()
                .AnyAsync(x => x.Id != id && x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase), cancellationToken);
            if (duplicate)
            {
                return BadRequest(new ApiResponse { Success = false, Message = "A document type with this name already exists." });
            }

            definition.Name = trimmedName;
            definition.UpdatedAtUtc = DateTime.UtcNow;
            _session.Store(definition);
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} updated document type {DocumentTypeId} to name {DocumentTypeName}", userId, definition.Id, trimmedName);

            var userId = _currentUserService.UserId;
            var assigned = await _session.Query<UserDocumentType>()
                .AnyAsync(x => x.UserId == userId && x.DocumentTypeId == definition.Id, cancellationToken);

            var dto = new DocumentTypeDto
            {
                Id = definition.Id,
                Name = definition.Name,
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = assigned
            };

            return Ok(new ApiResponse<DocumentTypeDto>(dto));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var definition = await _session.Query<DocumentTypeDefinition>()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (definition == null)
            {
                return NotFound(new ApiResponse { Success = false, Message = "Document type not found." });
            }

            if (definition.IsLocked)
            {
                return BadRequest(new ApiResponse { Success = false, Message = "Seeded document types cannot be deleted." });
            }

            var mappings = await _session.Query<UserDocumentType>()
                .Where(x => x.DocumentTypeId == id)
                .ToListAsync(cancellationToken);

            foreach (var mapping in mappings)
            {
                _session.Delete(mapping);
            }

            _session.Delete(definition);
            await _session.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} deleted document type {DocumentTypeId}", _currentUserService.UserId, id);

            return NoContent();
        }
    }
}
