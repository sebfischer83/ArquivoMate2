using ArquivoMate2.Shared.Models;
using MediatR;
using Newtonsoft.Json.Linq;

namespace ArquivoMate2.Application.Commands
{
    public record UpdateDocumentCommand(Guid DocumentId, UpdateDocumentFieldsDto Dto): IRequest<PatchResult>;

    public enum PatchResult
    {
        Success,
        Forbidden,
        Failed,
        Invalid
    }
}
