using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IEmbeddingsClient
    {
        Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
        Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
    }
}
