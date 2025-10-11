# Document Question-Answering Chatbot

This guide explains how ArquivoMate2 handles interactive questions about documents, how the vector store contributes context, and how recent document history is incorporated into the conversation.

## End-to-End Question Flow

1. A client calls the `AskDocumentQuestionQuery` endpoint. The `AskDocumentQuestionQueryHandler` first validates that the request contains a non-empty question and verifies that the current user can access the requested document via `IDocumentAccessService`.
2. The handler loads the `DocumentView` projection for the document through Marten. If no projection is found or the document is deleted, the query returns `null`.
3. When the request flags `includeHistory`, the handler loads the most recent domain events for the document by streaming its event log through Marten. Events are sorted descending by timestamp, the newest ten are selected, and each entry is formatted as an ISO timestamp, event name, and JSON payload.
4. The handler builds a `DocumentQuestionContext` that packages the document metadata (title, summary, keywords, language), full text content, and the optional history entries. This context is passed to the chatbot so it can reason about the document.
5. The handler instantiates a `DocumentQuestionTooling` helper, which exposes a `QueryDocumentsAsync` method. This helper wraps the search index (`ISearchClient`), the `DocumentView` read model, and the file metadata service so that the chatbot can issue structured catalogue lookups via function calling.
6. Finally, `IChatBot.AnswerQuestion` is invoked with the context, question, and tooling. The handler maps the chatbot’s `DocumentAnswerResult` (answer, citations, recommended documents, counts) to the DTO returned to the client.

## Chatbot Execution Details

The default implementation (`OpenAIChatBot`) prepares the prompt and orchestrates tool calls when answering a question:

- The document content is split into deterministic 1,200-character chunks. Each chunk is assigned a stable identifier (e.g. `chunk_3`) along with the character offsets. These chunks become the payload for the `load_document_chunk` tool.
- The chatbot composes a metadata section summarising the document title, summary, keywords, language, and the most recent history entries provided by the handler. It then lists every available chunk with its identifier and character range, reminding the model to load a chunk before quoting it.
- A system prompt instructs the LLM to rely on tool calls: `load_document_chunk` fetches chunk content for citations, while `query_documents` invokes `DocumentQuestionTooling.QueryDocumentsAsync` to search or filter the user’s broader catalogue.
- The chatbot iteratively handles tool calls, deserialises their JSON arguments, and appends tool responses back into the conversation. Exceptions during `query_documents` are caught and returned as structured errors so the model can recover.
- Once the LLM produces a final message, the chatbot expects compact JSON describing the answer, citations, suggested documents, and optional aggregate counts. The implementation parses that JSON and resolves chunk IDs back to snippets before returning the `DocumentAnswerResult` to the application layer.

## Vector Store Integration

Document ingestion triggers `ProcessDocumentHandler` to call `IDocumentVectorizationService.StoreDocumentAsync`. The default implementation (`DocumentVectorizationService`) performs the following steps:

1. Deterministically chunk the document content so the chunk IDs match those used by the chatbot.
2. Generate embeddings for every chunk with the configured OpenAI embedding model.
3. Persist the embeddings, chunk identifiers, offsets, and metadata into a PostgreSQL table (`document_vectors`) using the `pgvector` extension.

When a document is deleted or re-processed with empty content, the handler removes its vectors through `DeleteDocumentAsync`. The vectorization service also exposes `FindRelevantChunkIdsAsync`, enabling similarity search for the most relevant chunks to a user question. Although the current chatbot implementation loads chunks directly from the in-memory context, these precomputed vectors provide the foundation for retrieval-augmented improvements—such as seeding the prompt with only the top-N relevant chunk IDs or answering catalogue-wide semantic queries. If no vector store connection string is configured, the platform falls back to `NullDocumentVectorizationService`, which logs the lack of vector support and returns empty results without failing the request.

## Handling Document History

History integration is optional and driven by the request payload. When enabled, the handler fetches the latest events from the document’s event stream through Marten. Each entry is formatted as `YYYY-MM-DDTHH:MM:SSZ - EventName: {payload}` and passed to the chatbot. The prompt builder includes up to five of these entries in the metadata section so the LLM can reference recent changes (for example, processing milestones or previous chatbot interactions) when formulating an answer.

Because history loading occurs in the query handler, failures only generate a warning log; the chatbot continues without history if Marten throws an exception. This approach keeps question answering resilient even when the event store is temporarily unavailable.
