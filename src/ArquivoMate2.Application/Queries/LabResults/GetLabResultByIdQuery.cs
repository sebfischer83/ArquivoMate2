using ArquivoMate2.Shared.Models;
using MediatR;
using System;

namespace ArquivoMate2.Application.Queries.LabResults
{
    public sealed record GetLabResultByIdQuery(Guid Id) : IRequest<LabResultDto?>;
}
