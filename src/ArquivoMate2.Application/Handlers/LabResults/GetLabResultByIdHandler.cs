using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Queries.LabResults;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class GetLabResultByIdHandler : IRequestHandler<GetLabResultByIdQuery, LabResultDto?>
    {
        private readonly IQuerySession _query;

        public GetLabResultByIdHandler(IQuerySession query)
        {
            _query = query;
        }

        public async Task<LabResultDto?> Handle(GetLabResultByIdQuery request, CancellationToken cancellationToken)
        {
            var r = await _query.Query<LabResult>().Where(x => x.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (r == null) return null;
            var dto = new LabResultDto
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
            };
            return dto;
        }
    }
}
