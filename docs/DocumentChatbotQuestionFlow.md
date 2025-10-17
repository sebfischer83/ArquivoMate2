# Document Question-Answering Chatbot

## Summary
This guide outlines how ArquivoMate2 answers interactive questions about documents, the role of the vector store, and the way document history is incorporated into the conversation.

## Current Status
`AskDocumentQuestionQuery` is available in the application layer and expects documents to be pre-processed into deterministic chunks. Vector storage is optional but recommended; when disabled the chatbot still responds, albeit without semantic retrieval.

## Key Components
- **Query Handler:** `AskDocumentQuestionQueryHandler` validates input, enforces access with `IDocumentAccessService`, and hydrates the `DocumentView` projection from Marten.
- **Context Builder:** Constructs `DocumentQuestionContext` with metadata, full text, and optional history entries before invoking the chatbot.
- **Tooling Adapter:** `DocumentQuestionTooling` exposes structured catalogue search via `QueryDocumentsAsync`, wrapping `ISearchClient`, the read model, and file metadata services.
- **Chatbot Interface:** `IChatBot.AnswerQuestion` returns `DocumentAnswerResult` (answer text, citations, recommended documents, counts) for API consumption.

## Process Flow
1. The client submits a question to `AskDocumentQuestionQuery`.
2. The handler validates the payload, checks document access, and loads the `DocumentView` projection.
3. If `includeHistory` is set, the ten most recent domain events are streamed from Marten, sorted by timestamp, formatted, and added to the context.
4. The handler creates the `DocumentQuestionContext` and initialises `DocumentQuestionTooling`.
5. `IChatBot.AnswerQuestion` receives the context, question, and tooling; the handler maps the result DTO back to the client.

## Implementation Details
- **Prompt Assembly:** `OpenAIChatBot` splits document content into deterministic 1,200-character chunks (e.g., `chunk_3`) and exposes them through the `load_document_chunk` tool.
- **System Prompt:** Summaries of metadata, keywords, and history guide the LLM. The prompt instructs the model to request chunks via `load_document_chunk` and to perform catalogue queries through `query_documents`.
- **Tool Handling:** Tool invocations are deserialised, executed, and their responses appended to the conversation. Exceptions from `query_documents` are caught and returned as structured errors so the model can recover gracefully.
- **Response Parsing:** The chatbot expects compact JSON describing answer text, citations, recommended documents, and aggregate counts. Returned chunk IDs are mapped back to snippets before producing `DocumentAnswerResult`.

## Operational Guidance
- **Vector Store:** `ProcessDocumentHandler` calls `IDocumentVectorizationService.StoreDocumentAsync`, which chunks content, generates embeddings with the configured OpenAI model, and stores them in PostgreSQL using `pgvector`. When disabled, the null implementation logs the absence of vectors and returns empty results without failing the request.
- **History Loading:** History is optional and controlled by the request. Up to five recent events (formatted as `YYYY-MM-DDTHH:MM:SSZ - EventName: {payload}`) are included. Failures emit warnings only; the chatbot continues without history for resiliency.
- **Deletion Handling:** Re-processing with empty content or deleting a document triggers `DeleteDocumentAsync` to remove stored vectors.

## Future Improvements
- Seed the prompt with similarity-ranked chunk IDs from the vector store to reduce token usage.
- Expand `DocumentQuestionTooling` with additional catalogue filters (tags, owners, collections).
- Introduce caching for frequently accessed chunk payloads during heavy chat sessions.

## References
- `src/ArquivoMate2.Application/Documents/Queries/AskDocumentQuestionQuery.cs`
- `src/ArquivoMate2.Application/Documents/AskDocumentQuestionQueryHandler.cs`
- `src/ArquivoMate2.Infrastructure/ChatBot/OpenAIChatBot.cs`
- `src/ArquivoMate2.Infrastructure/Documents/Vectorization`
