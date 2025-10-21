using System.Collections.Generic;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using MediatR;

namespace ArquivoMate2.Application.Queries.DocumentTypes
{
    public record ListDocumentTypesQuery(string UserId) : IRequest<ApiResponse<IEnumerable<DocumentTypeDto>>>;
}
