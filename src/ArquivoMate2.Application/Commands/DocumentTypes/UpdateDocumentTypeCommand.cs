using System;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using MediatR;

namespace ArquivoMate2.Application.Commands.DocumentTypes
{
    public record UpdateDocumentTypeCommand(Guid Id, string UserId, string Name, string? SystemFeature, string? UserDefinedFunction) : IRequest<ApiResponse<DocumentTypeDto>>;
}
