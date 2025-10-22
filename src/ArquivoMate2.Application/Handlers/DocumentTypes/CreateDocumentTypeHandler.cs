using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using MediatR;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Handlers.DocumentTypes
{
    public class CreateDocumentTypeHandler : IRequestHandler<CreateDocumentTypeCommand, ApiResponse<DocumentTypeDto>>
    {
        private readonly IDocumentSession _session;

        public CreateDocumentTypeHandler(IDocumentSession session)
        {
            _session = session;
        }

        public async Task<ApiResponse<DocumentTypeDto>> Handle(CreateDocumentTypeCommand request, CancellationToken cancellationToken)
        {
            var trimmedName = request.Name.Trim();

            var exists = await _session.Query<DocumentTypeDefinition>()
                .AnyAsync(x => x.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase), cancellationToken);
            if (exists)
            {
                return new ApiResponse<DocumentTypeDto> { Success = false, Message = "Document type already exists." };
            }

            var systemFeatures = request.SystemFeatures != null && request.SystemFeatures.Count > 0
                ? new List<string>(request.SystemFeatures)
                : new List<string>();

            var userDefinedFunctions = request.UserDefinedFunctions != null && request.UserDefinedFunctions.Count > 0
                ? new List<string>(request.UserDefinedFunctions)
                : new List<string>();

            var definition = new DocumentTypeDefinition
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                NormalizedName = trimmedName.ToUpperInvariant(),
                SystemFeatures = systemFeatures,
                UserDefinedFunctions = userDefinedFunctions,
                IsLocked = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            _session.Store(definition);

            _session.Store(new UserDocumentType
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                DocumentTypeId = definition.Id,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _session.SaveChangesAsync(cancellationToken);

            var dto = new DocumentTypeDto
            {
                Id = definition.Id,
                Name = definition.Name,
                SystemFeatures = definition.SystemFeatures ?? new List<string>(),
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = true,
                UserDefinedFunctions = definition.UserDefinedFunctions ?? new List<string>()
            };

            return new ApiResponse<DocumentTypeDto>(dto);
        }
    }
}
