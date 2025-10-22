using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ArquivoMate2.Application.Queries.DocumentTypes;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using ArquivoMate2.Domain.DocumentTypes;
using Marten;
using MediatR;

namespace ArquivoMate2.Application.Handlers.DocumentTypes
{
    public class ListDocumentTypesHandler : IRequestHandler<ListDocumentTypesQuery, ApiResponse<IEnumerable<DocumentTypeDto>>>
    {
        private readonly IQuerySession _querySession;

        public ListDocumentTypesHandler(IQuerySession querySession)
        {
            _querySession = querySession;
        }

        public async Task<ApiResponse<IEnumerable<DocumentTypeDto>>> Handle(ListDocumentTypesQuery request, CancellationToken cancellationToken)
        {
            var definitions = await _querySession.Query<DocumentTypeDefinition>()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var assigned = await _querySession.Query<UserDocumentType>()
                .Where(x => x.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            var assignedSet = assigned.Select(x => x.DocumentTypeId).ToHashSet();
            var dtos = definitions.Select(def => new DocumentTypeDto
            {
                Id = def.Id,
                Name = def.Name,
                SystemFeatures = def.SystemFeatures ?? new List<string>(),
                UserDefinedFunctions = def.UserDefinedFunctions ?? new List<string>(),
                IsLocked = def.IsLocked,
                CreatedAtUtc = def.CreatedAtUtc,
                UpdatedAtUtc = def.UpdatedAtUtc,
                IsAssignedToCurrentUser = assignedSet.Contains(def.Id)
            }).ToList();

            return new ApiResponse<IEnumerable<DocumentTypeDto>>(dtos);
        }
    }
}
