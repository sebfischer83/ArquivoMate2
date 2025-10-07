using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IDocumentArtifactStreamer
    {
        Task<(Func<Stream, CancellationToken, Task> WriteToAsync, string ContentType)> GetAsync(Guid documentId, string artifact, CancellationToken ct);
    }
}
