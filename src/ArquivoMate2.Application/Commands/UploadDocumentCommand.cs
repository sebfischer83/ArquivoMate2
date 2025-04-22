using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record UploadDocumentCommand(string FilePath) : IRequest<Guid>;
}
