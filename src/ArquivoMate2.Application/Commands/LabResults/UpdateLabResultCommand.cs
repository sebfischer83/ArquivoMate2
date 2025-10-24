using MediatR;
using ArquivoMate2.Shared.Models;
using System;

namespace ArquivoMate2.Application.Commands.LabResults
{
    public sealed record UpdateLabResultCommand(LabResultDto Dto) : IRequest<bool>;
}
