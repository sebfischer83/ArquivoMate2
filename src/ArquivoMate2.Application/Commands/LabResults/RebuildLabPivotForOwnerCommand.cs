using MediatR;

namespace ArquivoMate2.Application.Commands.LabResults
{
    public sealed record RebuildLabPivotForOwnerCommand(string OwnerId) : IRequest<Unit>;
}
