using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.LabResults
{
    public sealed record GetLabPivotByOwnerQuery(string OwnerId) : IRequest<LabPivotTableDto?>;
}
