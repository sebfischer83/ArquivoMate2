using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces;

namespace ArquivoMate2.Infrastructure.Services.Llm
{
    // OpenAI embeddings client implementation calling the OpenAI REST API.
    // Implements IEmbeddingsClient so it can be injected and mocked in tests.
    public sealed class OpenAiEmbeddingsClient : IEmbeddingsClient, IDisposable
    {
        private const string DefaultEndpoint = "https://api.openai.com/v1/embeddings";
        private readonly string _model;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OpenAiEmbeddingsClient(string model, string apiKey)
        {
            _model = model ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key must be provided", nameof(apiKey));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArquivoMate2-OpenAiEmbeddingsClient/1.0");
        }

        public async Task<IReadOnlyList<EmbeddingResponse>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Count == 0) return Array.Empty<EmbeddingResponse>();

            var payload = new OpenAiEmbeddingsRequest
            {
                Model = _model,
                Input = inputs
            };

            var response = await SendWithRetriesAsync(payload, cancellationToken).ConfigureAwait(false);
            if (response?.Data == null) return Array.Empty<EmbeddingResponse>();

            var result = response.Data.Select(d => new EmbeddingResponse { Vector = Array.AsReadOnly(d.Embedding) }).ToList().AsReadOnly();
            return result;
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var payload = new OpenAiEmbeddingsRequest
            {
                Model = _model,
                Input = new[] { input }
            };

            var response = await SendWithRetriesAsync(payload, cancellationToken).ConfigureAwait(false);
            var first = response?.Data != null && response.Data.Count > 0 ? response.Data[0] : null;
            return first is null ? new EmbeddingResponse { Vector = Array.Empty<float>() } : new EmbeddingResponse { Vector = Array.AsReadOnly(first.Embedding) };
        }

        private async Task<OpenAiEmbeddingsResponse?> SendWithRetriesAsync(OpenAiEmbeddingsRequest payload, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            var delay = 500; // ms

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = JsonContent.Create(payload);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint) { Content = request };

                HttpResponseMessage httpResponse = null!;
                try
                {
                    httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var parsed = await httpResponse.Content.ReadFromJsonAsync<OpenAiEmbeddingsResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
                        return parsed;
                    }

                    // For rate limit or server errors, allow retry
                    if ((int)httpResponse.StatusCode == 429 || ((int)httpResponse.StatusCode >= 500 && (int)httpResponse.StatusCode < 600))
                    {
                        if (attempt == maxAttempts)
                        {
                            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            throw new HttpRequestException($"OpenAI embeddings request failed after {attempt} attempts. Status: {httpResponse.StatusCode}, Body: {body}");
                        }

                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        delay *= 2;
                        continue;
                    }

                    // Non-retriable error
                    var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new HttpRequestException($"OpenAI embeddings request failed with status {(int)httpResponse.StatusCode}: {httpResponse.ReasonPhrase}. Body: {content}");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay *= 2;
                    continue;
                }
                finally
                {
                    httpResponse?.Dispose();
                }

                break;
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _httpClient.Dispose();
            _disposed = true;
        }

        // Local DTOs for OpenAI JSON payload/response
        private sealed class OpenAiEmbeddingsRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("input")] public IReadOnlyList<string> Input { get; set; } = Array.Empty<string>();
        }

        private sealed class OpenAiEmbeddingItem
        {
            [JsonPropertyName("embedding")] public float[] Embedding { get; set; } = Array.Empty<float>();
        }

        private sealed class OpenAiEmbeddingsResponse
        {
            [JsonPropertyName("data")] public List<OpenAiEmbeddingItem>? Data { get; set; }
        }
    }

    public sealed class EmbeddingResponse
    {
        public IReadOnlyList<float> Vector { get; set; } = Array.Empty<float>();
    }
}
