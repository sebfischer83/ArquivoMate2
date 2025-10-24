using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands.LabResults;
using MediatR;
using Marten;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Interfaces;
using System;

namespace ArquivoMate2.Application.Handlers.LabResults
{
    public class UpdateLabResultHandler : IRequestHandler<UpdateLabResultCommand, bool>
    {
        private readonly IDocumentSession _session;
        private readonly IParameterNormalizer _parameterNormalizer;

        public UpdateLabResultHandler(IDocumentSession session, IParameterNormalizer parameterNormalizer)
        {
            _session = session;
            _parameterNormalizer = parameterNormalizer;
        }

        public async Task<bool> Handle(UpdateLabResultCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;
            var existing = await _session.LoadAsync<LabResult>(dto.Id, cancellationToken);
            if (existing == null) return false;

            // update simple fields
            existing.Patient = dto.Patient;
            existing.LabName = dto.LabName;
            existing.Date = dto.Date;

            // replace points
            existing.Points = dto.Points.Select(p => new LabResultPoint
            {
                Parameter = p.ResultRaw != null ? p.ResultRaw : string.Empty, // placeholder, mapping might differ
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
            }).ToList();

            _session.Store(existing);
            await _session.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
