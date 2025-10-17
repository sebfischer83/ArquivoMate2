# Document Chatbot Architecture and Implementation Plan

## Summary
The goal is to let users open a document in the web client and ask free-form questions about its content. Responses are generated live by a large language model (LLM) that receives the document text and optional metadata as context. Existing document-processing pipelines and chatbot metadata storage remain unchanged.

## Current Status
The document describes a proposed architecture; the production system does not yet expose the outlined chatbot endpoints. All components referenced below exist in the solution today, but the chatbot-specific interfaces and services must still be added.

## Key Components

### Domain and Application Layer
- Extend `IChatBot` with a method such as `Task<DocumentAnswerResult> AnswerQuestion(DocumentQuestionContext context, string question, CancellationToken ct)` or introduce a dedicated `IDocumentQuestionChatBot` interface to keep the existing `AnalyzeDocumentContent` flow intact.
- Implement a `DocumentQuestionAnsweringService` that loads the `DocumentAggregate` or corresponding read model, prepares the context (text, summary, keywords, user metadata), invokes `IChatBot.AnswerQuestion`, and returns the answer plus cited snippets.
- Plan for deterministic chunking so that only the relevant text is used for prompts. The service can reuse `ISearchClient` or the vector store for semantic retrieval.

### API Layer
- Add `POST /api/documents/{id}/chat` to `DocumentsController` in `ArquivoMate2.API`. The request includes the question and optional flags such as `includeHistory` or `maxSnippets`. The endpoint delegates to the `DocumentQuestionAnsweringService` and can either stream responses (Server-Sent Events) or return a synchronous payload.
- Expose `POST /api/documents/chat` for catalogue-wide questions that are not tied to a specific document. The response may contain recommended document IDs or aggregate counts.
- Define DTOs in `ArquivoMate2.Shared` (`DocumentQuestionRequestDto`, `DocumentAnswerDto`) that capture the answer text, cited passages, model name, and optional collections like `documents` or `documentCount`.
- Reuse existing authorization attributes (e.g., `[Authorize]`) and enforce rate limiting to prevent abuse.

### Infrastructure Layer
- Update `OpenAIChatBot` so that it supports chat completions with system/user messages, streaming responses, and function calling for chunk loading. Provide tools such as `load_document_chunk` and `query_documents` so the model can fetch context lazily.
- Extend `ChatBotSettings`, `ChatBotSettingsFactory`, and the related configuration (`appsettings`) with parameters for model, max tokens, temperature, and embedding behavior.
- Optionally integrate embeddings using OpenAI or Azure AI Search. Persist vectors in the existing search infrastructure or a dedicated store to support retrieval-augmented generation (RAG).

### Frontend (Angular)
- Extend `DocumentsService` with `askDocumentQuestion(id: string, request: DocumentQuestionRequestDto)` that calls the new endpoint. Support streaming answers through `EventSource` or RxJS wrappers when SSE is enabled.
- Introduce a `DocumentChatComponent` that provides question input, history, and answer rendering. Reuse existing resolvers to inject document summaries and keywords.
- If NgRx is used, add actions/effects for question submission and answer handling. Otherwise maintain the state locally within the component.
- Provide UX affordances such as loading indicators, error handling for timeouts or unavailable models, and privacy/cost notices.

## Implementation Plan
1. Conduct an architecture review to finalise the prompting approach (retrieval-only versus full RAG).
2. Update interfaces, add the service, and implement the new endpoints in the backend.
3. Extend the infrastructure layer with configuration updates, OpenAI tooling, and optional embedding support.
4. Build the Angular service and component, ensuring the UI matches the backend contract.
5. Add observability, complete automated tests, and roll out gradually.

## Non-Functional Considerations
- **Logging and Tracing:** Capture prompts (with sensitive data removed) and responses for monitoring while remaining GDPR-compliant.
- **Cost Management:** Enforce limits for prompt length, max tokens, and concurrent conversations.
- **Testing:** Provide unit tests for prompt assembly, integration tests with a mock `IChatBot`, and end-to-end coverage via Cypress.
- **Security:** Ensure only authorised users can access documents and ask questions. Sanitize user input before forwarding to the LLM.

## References
- `src/ArquivoMate2.API/Controllers/DocumentsController.cs`
- `src/ArquivoMate2.Application` (service layer)
- `src/ArquivoMate2.Infrastructure/ChatBot/OpenAIChatBot.cs`
- `src/ArquivoMate2.Web` (Angular client)

