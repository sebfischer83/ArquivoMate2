using ArquivoMate2.Application.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    // No-op embeddings client used when embeddings are disabled.
    public sealed class NullEmbeddingsClient : IEmbeddingsClient
    {
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float[]>>(new List<float[]>());

        public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<float>());
    }
}
