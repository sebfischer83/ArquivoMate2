using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services.Documents;
using ArquivoMate2.Infrastructure.Configuration.Llm;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Vectorization
{
    public sealed class DocumentVectorizationService : IDocumentVectorizationService
    {
        private readonly string _connectionString;
        private readonly IEmbeddingsClient _embeddingsClient;
        private readonly ILogger<DocumentVectorizationService> _logger;
        private readonly int _embeddingDimensions;

        private static bool _typeMapperConfigured;
        private static bool _schemaInitialized;
        private static readonly SemaphoreSlim _schemaLock = new(1, 1);

        public DocumentVectorizationService(string connectionString, OpenAISettings settings, IEmbeddingsClient embeddingsClient, ILogger<DocumentVectorizationService> logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Vector store connection string must be configured", nameof(connectionString));
            }

            _connectionString = connectionString;
            _logger = logger;
            _embeddingDimensions = settings.EmbeddingDimensions > 0 ? settings.EmbeddingDimensions : 1536;
            _embeddingsClient = embeddingsClient ?? throw new ArgumentNullException(nameof(embeddingsClient));

            ConfigureTypeMapper();
        }

        public async Task StoreDocumentAsync(Guid documentId, string userId, string content, CancellationToken cancellationToken)
        {
            var chunks = DocumentChunking.Split(content);
            if (chunks.Count == 0)
            {
                await DeleteDocumentAsync(documentId, userId, cancellationToken);
                return;
            }

            var texts = chunks.Select(c => c.Content).ToList();
            var embeddings = await GenerateEmbeddingsAsync(texts, cancellationToken);

            // Validate embeddings count and dimensions
            if (embeddings == null || embeddings.Count != chunks.Count)
            {
                _logger.LogError("Embeddings count mismatch for document {DocumentId}: expected {ExpectedCount} but got {ActualCount}. Aborting vector storage.", documentId, chunks.Count, embeddings?.Count ?? 0);
                try
                {
                    await DeleteDocumentAsync(documentId, userId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete existing vectors after embeddings count mismatch for document {DocumentId}", documentId);
                }

                return;
            }

            for (var i = 0; i < embeddings.Count; i++)
            {
                var emb = embeddings[i];
                if (emb == null || emb.Length != _embeddingDimensions)
                {
                    _logger.LogError("Embeddings dimension mismatch for document {DocumentId} at chunk index {Index}: expected {ExpectedDim} but got {ActualDim}. Aborting vector storage.", documentId, i, _embeddingDimensions, emb?.Length ?? 0);
                    try
                    {
                        await DeleteDocumentAsync(documentId, userId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete existing vectors after embeddings dimension mismatch for document {DocumentId}", documentId);
                    }

                    return;
                }
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var deleteCommand = new NpgsqlCommand("DELETE FROM document_vectors WHERE document_id = @doc AND user_id = @user", connection, transaction);
            deleteCommand.Parameters.AddWithValue("doc", documentId);
            deleteCommand.Parameters.AddWithValue("user", userId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            for (var index = 0; index < chunks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = chunks[index];
                var embedding = embeddings[index];

                var insertCommand = new NpgsqlCommand(@"INSERT INTO document_vectors
                        (id, document_id, user_id, chunk_id, chunk_index, start_offset, end_offset, embedding, created_at)
                        VALUES (@id, @doc, @user, @chunk_id, @chunk_index, @start, @end, @embedding, @created_at)", connection, transaction);

                insertCommand.Parameters.AddWithValue("id", Guid.NewGuid());
                insertCommand.Parameters.AddWithValue("doc", documentId);
                insertCommand.Parameters.AddWithValue("user", userId);
                insertCommand.Parameters.AddWithValue("chunk_id", chunk.Id);
                insertCommand.Parameters.AddWithValue("chunk_index", chunk.Index);
                insertCommand.Parameters.AddWithValue("start", chunk.Start);
                insertCommand.Parameters.AddWithValue("end", chunk.End);
                insertCommand.Parameters.AddWithValue("embedding", new Vector(embedding));
                insertCommand.Parameters.AddWithValue("created_at", DateTimeOffset.UtcNow);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Stored {ChunkCount} vector chunks for document {DocumentId}", chunks.Count, documentId);
        }

        public async Task DeleteDocumentAsync(Guid documentId, string userId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);

            var command = new NpgsqlCommand("DELETE FROM document_vectors WHERE document_id = @doc AND user_id = @user", connection);
            command.Parameters.AddWithValue("doc", documentId);
            command.Parameters.AddWithValue("user", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<string>> FindRelevantChunkIdsAsync(Guid documentId, string userId, string question, int limit, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return Array.Empty<string>();
            }

            var embedding = await GenerateEmbeddingAsync(question, cancellationToken);

            // Validate embedding dimension before querying DB
            if (embedding == null || embedding.Length != _embeddingDimensions)
            {
                _logger.LogWarning("Query embedding dimension mismatch for document {DocumentId}: expected {ExpectedDim} but got {ActualDim}. Returning empty results.", documentId, _embeddingDimensions, embedding?.Length ?? 0);
                return Array.Empty<string>();
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);

            var command = new NpgsqlCommand(@"SELECT chunk_id FROM document_vectors
                    WHERE document_id = @doc AND user_id = @user
                    ORDER BY embedding <=> @embedding
                    LIMIT @limit", connection);

            command.Parameters.AddWithValue("doc", documentId);
            command.Parameters.AddWithValue("user", userId);
            command.Parameters.AddWithValue("embedding", new Vector(embedding));
            command.Parameters.AddWithValue("limit", limit);

            var result = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        private async Task EnsureSchemaAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            if (_schemaInitialized)
            {
                return;
            }

            await _schemaLock.WaitAsync(cancellationToken);
            try
            {
                if (_schemaInitialized)
                {
                    return;
                }

                var commandText = $@"
CREATE TABLE IF NOT EXISTS document_vectors (
    id uuid PRIMARY KEY,
    document_id uuid NOT NULL,
    user_id text NOT NULL,
    chunk_id text NOT NULL,
    chunk_index integer NOT NULL,
    start_offset integer NOT NULL,
    end_offset integer NOT NULL,
    embedding vector({_embeddingDimensions}) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_document_vectors_doc_user ON document_vectors (document_id, user_id);
CREATE INDEX IF NOT EXISTS idx_document_vectors_chunk ON document_vectors (document_id, chunk_id);
CREATE INDEX IF NOT EXISTS idx_document_vectors_embedding ON document_vectors USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
";

                using var command = new NpgsqlCommand(commandText, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _schemaInitialized = true;
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        private async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        {
            return await _embeddingsClient.GenerateEmbeddingsAsync(inputs, cancellationToken);
        }

        private async Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken)
        {
            return await _embeddingsClient.GenerateEmbeddingAsync(input, cancellationToken);
        }

        private static void ConfigureTypeMapper()
        {
            if (_typeMapperConfigured)
            {
                return;
            }

            NpgsqlConnection.GlobalTypeMapper.UseVector();
            _typeMapperConfigured = true;
        }
    }
}
