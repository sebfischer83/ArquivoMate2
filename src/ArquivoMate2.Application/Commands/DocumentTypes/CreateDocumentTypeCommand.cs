using System;
using System.Collections.Generic;
using ArquivoMate2.Shared.ApiModels;
using ArquivoMate2.Shared.Models.DocumentTypes;
using MediatR;

namespace ArquivoMate2.Application.Commands.DocumentTypes
{
    public record CreateDocumentTypeCommand(string UserId, string Name, List<string>? SystemFeatures, List<string>? UserDefinedFunctions) : IRequest<ApiResponse<DocumentTypeDto>>;
}
