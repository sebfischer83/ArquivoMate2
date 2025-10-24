using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.LabResults;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using System.Collections.Generic;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class GetLabPivotByOwnerHandler : IRequestHandler<GetLabPivotByOwnerQuery, LabPivotTableDto?>
    {
        private readonly IQuerySession _query;

        public GetLabPivotByOwnerHandler(IQuerySession query)
        {
            _query = query;
        }

        public async Task<LabPivotTableDto?> Handle(GetLabPivotByOwnerQuery request, CancellationToken cancellationToken)
        {
            var pivot = await _query.Query<LabPivotTable>().Where(p => p.OwnerId == request.OwnerId).FirstOrDefaultAsync(cancellationToken);
            if (pivot == null) return null;

            var dto = new LabPivotTableDto
            {
                OwnerId = pivot.OwnerId,
                ColumnsDesc = pivot.ColumnsDesc,
                Rows = pivot.Rows.Select(r => new PivotRowDto
                {
                    Parameter = r.Parameter,
                    Unit = r.Unit,
                    ValuesByCol = r.ValuesByCol
                }).ToList()
            };

            return dto;
        }
    }
}
