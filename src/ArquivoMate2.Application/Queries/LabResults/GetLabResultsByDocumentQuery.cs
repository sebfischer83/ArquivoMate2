using System;
using System.Collections.Generic;
using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.LabResults
{
    public sealed record GetLabResultsByDocumentQuery(Guid DocumentId) : IRequest<List<LabResultDto>>;
}
