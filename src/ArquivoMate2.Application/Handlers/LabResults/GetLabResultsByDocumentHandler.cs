using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.LabResults;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using System.Collections.Generic;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class GetLabResultsByDocumentHandler : IRequestHandler<GetLabResultsByDocumentQuery, List<LabResultDto>>
    {
        private readonly IQuerySession _query;

        public GetLabResultsByDocumentHandler(IQuerySession query)
        {
            _query = query;
        }

        public async Task<List<LabResultDto>> Handle(GetLabResultsByDocumentQuery request, CancellationToken cancellationToken)
        {
            var results = await _query.Query<LabResult>()
                .Where(r => r.DocumentId == request.DocumentId)
                .ToListAsync(cancellationToken);

            var dtos = results.Select(r => new LabResultDto
            {
                Id = r.Id,
                DocumentId = r.DocumentId,
                Patient = r.Patient,
                LabName = r.LabName,
                Date = r.Date,
                Points = r.Points.Select(p => new LabResultPointDto
                {
                    ResultRaw = p.ResultRaw,
                    ResultNumeric = p.ResultNumeric,
                    ResultComparator = p.ResultComparator,
                    Unit = p.Unit,
                    Reference = p.Reference,
                    ReferenceComparator = p.ReferenceComparator,
                    ReferenceFrom = p.ReferenceFrom,
                    ReferenceTo = p.ReferenceTo,
                    NormalizedResult = p.NormalizedResult,
                    NormalizedUnit = p.NormalizedUnit,
                    NormalizedReferenceFrom = p.NormalizedReferenceFrom,
                    NormalizedReferenceTo = p.NormalizedReferenceTo
                }).ToList()
            }).ToList();

            return dtos;
        }
    }
}
