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

            var definition = new DocumentTypeDefinition
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                NormalizedName = trimmedName.ToUpperInvariant(),
                SystemFeature = string.IsNullOrWhiteSpace(request.SystemFeature) ? string.Empty : request.SystemFeature.Trim(),
                UserDefinedFunction = string.IsNullOrWhiteSpace(request.UserDefinedFunction) ? string.Empty : request.UserDefinedFunction.Trim(),
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
                SystemFeature = definition.SystemFeature,
                IsLocked = definition.IsLocked,
                CreatedAtUtc = definition.CreatedAtUtc,
                UpdatedAtUtc = definition.UpdatedAtUtc,
                IsAssignedToCurrentUser = true
            };

            return new ApiResponse<DocumentTypeDto>(dto);
        }
    }
}
