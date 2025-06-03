using MediatR;
using Newtonsoft.Json.Linq;

namespace ArquivoMate2.Application.Commands
{
    public record UpdateDocumentCommand(Guid DocumentId, JObject Json): IRequest<PatchResult>;

    public enum PatchResult
    {
        Success,
        Forbidden,
        Failed,
        Invalid
    }
}
