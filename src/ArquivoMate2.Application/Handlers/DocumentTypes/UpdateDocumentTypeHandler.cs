using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using MediatR;
using System.Linq;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Handlers.DocumentTypes
{
    public class UpdateDocumentTypeHandler : IRequestHandler<UpdateDocumentTypeCommand, ApiResponse<DocumentTypeDto>>
    {
        private readonly IDocumentSession _session;

        public UpdateDocumentTypeHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<ApiResponse<DocumentTypeDto>> Handle(UpdateDocumentTypeCommand request, CancellationToken cancellationToken)
        {
            var definition = await _session.Query<DocumentTypeDefinition>()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (definition == null)
            {
                return new ApiResponse<DocumentTypeDto> { Success = false, Message = "Document type not found." };
            }

            if (definition.IsLocked)
            {
                return new ApiResponse<DocumentTypeDto> { Success = false, Message = "Seeded document types cannot be modified." };
            }

            var trimmedName = request.Name.Trim();
            var duplicate = await _session.Query<DocumentTypeDefinition>()
                .AnyAsync(x => x.Id != request.Id && x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase), cancellationToken);
            if (duplicate)
            {
                return new ApiResponse<DocumentTypeDto> { Success = false, Message = "A document type with this name already exists." };
            }

            definition.Name = trimmedName;

            // assign lists (use provided lists or empty lists)
            definition.SystemFeatures = request.SystemFeatures != null && request.SystemFeatures.Count > 0
                ? new List<string>(request.SystemFeatures)
                : new List<string>();

            definition.UserDefinedFunctions = request.UserDefinedFunctions != null && request.UserDefinedFunctions.Count > 0
                ? new List<string>(request.UserDefinedFunctions)
                : new List<string>();

            definition.NormalizedName = trimmedName.ToUpperInvariant();
            definition.UpdatedAtUtc = DateTime.UtcNow;
            _session.Store(definition);
            await _session.SaveChangesAsync(cancellationToken);

            var assigned = await _session.Query<UserDocumentType>()
                .AnyAsync(x => x.UserId == request.UserId && x.DocumentTypeId == definition.Id, cancellationToken);

            var dto = new DocumentTypeDto
            {
                Id = definition.Id,
                Name = definition.Name,
                SystemFeatures = definition.SystemFeatures ?? new List<string>(),
                UserDefinedFunctions = definition.UserDefinedFunctions ?? new List<string>(),
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = assigned
            };

            return new ApiResponse<DocumentTypeDto>(dto);
        }
    }
}
