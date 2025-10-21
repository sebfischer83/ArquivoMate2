using System;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using MediatR;

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
            definition.SystemFeature = string.IsNullOrWhiteSpace(request.SystemFeature) ? string.Empty : request.SystemFeature.Trim();
            definition.UserDefinedFunction = string.IsNullOrWhiteSpace(request.UserDefinedFunction) ? string.Empty : request.UserDefinedFunction.Trim();
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
                SystemFeature = definition.SystemFeature,
                UserDefinedFunction = definition.UserDefinedFunction,
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = assigned
            };

            return new ApiResponse<DocumentTypeDto>(dto);
        }
    }
}
