using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.LabResults;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using System.Collections.Generic;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;
using ArquivoMate2.Domain.ReadModels;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class GetLabPivotByDocumentHandler : IRequestHandler<GetLabPivotByDocumentQuery, LabPivotTableDto?>
    {
        private readonly IQuerySession _query;

        public GetLabPivotByDocumentHandler(IQuerySession query)
        {
            _query = query;
        }

        public async Task<LabPivotTableDto?> Handle(GetLabPivotByDocumentQuery request, CancellationToken cancellationToken)
        {
            // Find owner id for the provided document
            var ownerId = await _query.Query<DocumentView>()
                .Where(d => d.Id == request.DocumentId)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(ownerId)) return null;

            var pivot = await _query.Query<LabPivotTable>().Where(p => p.OwnerId == ownerId).FirstOrDefaultAsync(cancellationToken);
            if (pivot == null) return null;

            // Load the specific document's LabResults dates to determine which columns to include
            var docDates = await _query.Query<ArquivoMate2.Application.Features.Processors.LabResults.Domain.LabResult>()
                .Where(r => r.DocumentId == request.DocumentId)
                .Select(r => r.Date)
                .ToListAsync(cancellationToken);

            if (docDates.Count == 0)
            {
                // No lab results for this document -> return empty pivot
                return new LabPivotTableDto { OwnerId = ownerId, ColumnsDesc = new List<System.DateOnly>(), Rows = new List<PivotRowDto>() };
            }

            // Build filtered pivot containing only columns present in docDates
            var filteredCols = pivot.ColumnsDesc.Where(c => docDates.Contains(c)).OrderByDescending(d => d).ToList();
            var colIndexes = filteredCols.Select(c => pivot.ColumnsDesc.IndexOf(c)).ToList();

            var rows = pivot.Rows.Select(r => new PivotRowDto
            {
                Parameter = r.Parameter,
                Unit = r.Unit,
                ValuesByCol = colIndexes.Select(i => r.ValuesByCol.Count > i ? r.ValuesByCol[i] : null).ToList(),
                QualitativeByCol = colIndexes.Select(i => r.QualitativeByCol.Count > i ? r.QualitativeByCol[i] : null).ToList()
            }).Where(r => r.ValuesByCol.Any(v => v.HasValue) || r.QualitativeByCol.Any(q => !string.IsNullOrWhiteSpace(q))).ToList();

            var dto = new LabPivotTableDto
            {
                OwnerId = pivot.OwnerId,
                ColumnsDesc = filteredCols,
                Rows = rows
            };

            return dto;
        }
    }
}
