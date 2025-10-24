using MediatR;
using ArquivoMate2.Shared.Models;
using System;

namespace ArquivoMate2.Application.Queries.LabResults
{
    public sealed record GetLabPivotByDocumentQuery(Guid DocumentId) : IRequest<LabPivotTableDto?>;
}
