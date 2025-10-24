using MediatR;
using System;

namespace ArquivoMate2.Application.Commands.LabResults
{
    public sealed record DeleteLabResultCommand(Guid Id) : IRequest<bool>;
}
