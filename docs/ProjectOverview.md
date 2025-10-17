# ArquivoMate2 Project Overview

## Summary
ArquivoMate2 follows a layered architecture that separates presentation, application orchestration, core domain logic, and infrastructure integrations. This document summarises the solution structure and highlights the major subsystems so contributors can navigate the codebase quickly.

## Current Status
The architecture described here reflects the .NET 9 upgrade completed in 2025. API endpoints use a consistent `ApiResponse<T>` wrapper, streaming delivery is in production, and document sharing and grouping features are actively maintained.

## Solution Structure
1. **API (Presentation)** – ASP.NET Core host, HTTP endpoints, delivery token issuance, background job scheduling.
2. **Application (Use Cases / Orchestrators)** – Commands, queries, background processing, service contracts.
3. **Domain (Core Model & Domain Events)** – Aggregates, entities, value objects, domain events.
4. **Infrastructure (Implementations / External Integrations)** – Persistence, external services, projections.
5. **Shared (Cross-layer DTOs / Contracts)** – Serialisable models shared with clients.
6. **Tests (Unit / Integration Tests)** – Layer-specific test suites.

Clean architecture rules apply: API depends only on Application/Shared, Application depends on Domain/Shared, Infrastructure implements interfaces from Application/Domain, and Shared remains dependency-free beyond the BCL.

### Project Inventory
| Project | Target | Responsibilities | Key Dependencies |
| --- | --- | --- | --- |
| `src/ArquivoMate2.API` | net9.0 | ASP.NET Core host, controllers, SignalR hubs, delivery token issuance. | ASP.NET Core, Hangfire, MediatR, Marten, AutoMapper |
| `src/ArquivoMate2.Application` | net9.0 | Commands, queries, orchestrators, background services, service interfaces. | MediatR, Marten abstractions |
| `src/ArquivoMate2.Domain` | net9.0 | Aggregates (`Document`), processes (`ImportProcess`), domain events, value objects. | BCL only |
| `src/ArquivoMate2.Infrastructure` | net9.0 | Marten projections, storage and delivery providers, email connectors, search & LLM integrations. | Marten, Meilisearch, Minio/S3, OpenAI, FusionCache |
| `src/ArquivoMate2.Shared` | net9.0 | DTOs, request/response contracts, enums. | None beyond BCL |
| `tests/ArquivoMate2.Application.Tests` | net9.0 | Application handler and service tests. | xUnit, FluentAssertions |
| `tests/ArquivoMate2.Domain.Tests` | net9.0 | Domain aggregate and value-object tests. | xUnit, FluentAssertions |
| `tests/ArquivoMate2.Infrastructure.Tests` | net9.0 | Infrastructure adapter tests (storage, paths, streaming). | xUnit, FluentAssertions |

## Key Components
### API (`src/ArquivoMate2.API`)
- Controllers for documents, email, imports, collections, parties, notes, sharing, grouping, delivery, public shares, maintenance, and users.
- SignalR hub (`DocumentProcessingHub`) with notifier implementation `SignalRDocumentProcessingNotifier`.
- Composition root in `Program.cs` plus middleware for response wrapping (`ApiResponseWrapperFilter`, `ProblemDetailsMiddleware`).

### Application (`src/ArquivoMate2.Application`)
- Command/query handlers for upload, processing, updating, hiding, and indexing documents.
- Services such as `DocumentProcessingService` and `EmailDocumentBackgroundService`.
- Abstractions for storage, email, chatbots, search, delivery, thumbnails, metadata, paths, current user context, and notifications.

### Domain (`src/ArquivoMate2.Domain`)
- Aggregate root `Document` and process manager `ImportProcess`.
- Value objects (paths, metadata) and domain events (`DocumentUploaded`, `DocumentFilesPrepared`, `DocumentContentExtracted`, `DocumentProcessed`, `DocumentUpdated`, `DocumentChatBotDataReceived`).

### Infrastructure (`src/ArquivoMate2.Infrastructure`)
- Implementations for storage/delivery providers (S3, BunnyCDN, server delivery), email connectors (IMAP/POP3), search clients, OpenAI chatbot, thumbnail generation, path resolution, and current-user context.
- Marten projections and read models (`DocumentProjection`, `DocumentView`, `ImportHistoryProjection`, `ImportHistoryView`).
- Configuration factories for chatbot, authentication, storage, and delivery providers.

### Shared (`src/ArquivoMate2.Shared`)
- Serialisable DTOs for documents, imports, emails, notifications, and API responses (`ApiResponse<T>`).
- Shared enums such as `ProcessingStatus` and `ImportSource`.

### Tests (`tests/*`)
- Layer-specific suites to keep domain tests fast and infrastructure tests focused on integration with adapters.

## Subsystems
### Streaming Delivery
- `IDocumentArtifactStreamer` resolves artifact metadata and returns a streaming delegate plus content type.
- `IStorageProvider.StreamFileAsync` streams bytes to a provided consumer; implementations should prefer zero-copy streaming.
- `DocumentArtifactStreamer` unwraps per-artifact DEKs and supports version 1 (AES-GCM) and version 2 (AES-CBC + HMAC) encryption formats.
- `DeliveryController` validates signed tokens, checks permissions, and streams artifacts without buffering entire files. Detailed guidance lives in `docs/delivery.md`.

### Document Sharing & Permissions
- `DocumentShare` aggregate records share targets (users/groups) and permission flags (`DocumentPermissions`).
- `DocumentAccessView` projection aggregates direct and inherited permissions for fast lookup by `DocumentAccessService`.
- Helper methods (`HasAccessToDocumentAsync`, `HasEditAccessToDocumentAsync`) enforce permissions consistently. See also `docs/FeatureSharing.md`.

### Dynamic Grouping & Collections
- Clients request hierarchical groupings via `POST /api/documents/grouping`, supplying an ordered list of dimensions (collection, year, month, type, language).
- Collections are user-owned; shared documents surface in a `(shared)` bucket and never appear inside another user's collections.
- `CollectionsController` manages CRUD operations and membership. Ordering rules: `(None)`, `(Shared)`, names A→Z; years/months descending with `(Unknown)` last; type/language with `(None)` first then alphabetical.

### API Response & OpenAPI
- Successful JSON responses wrap payloads in `ApiResponse<T>`; errors use RFC7807 `ProblemDetails`/`ValidationProblemDetails`.
- Filters and middleware ensure runtime consistency and accurate Swagger documentation. File-streaming endpoints continue to return `FileResult` without the wrapper.

## Cross-Cutting Concerns
- **User Context:** `ICurrentUserService` abstraction with infrastructure implementation.
- **File & Path Handling:** `IPathService` centralises file-system semantics.
- **External Integrations:** S3/Minio for storage, BunnyCDN for delivery, OpenAI for LLM operations, IMAP/POP3 for email ingestion, custom search client, SignalR for notifications.
- **Background Processing:** `EmailDocumentBackgroundService` ingests and uploads email-sourced documents.

## Future Improvements
- Expand automated test coverage across domain events, handlers with in-memory fakes, and infrastructure integrations.
- Document centralised exception handling and logging strategies.
- Introduce a validation layer (e.g., FluentValidation) for incoming API DTOs.
- Continue optimising streaming delivery to eliminate remaining buffering in storage providers.

## References
- `docs/delivery.md`
- `docs/FeatureSharing.md`
- `docs/DocumentChatbotQuestionFlow.md`
- `src/ArquivoMate2.*`
- `tests/ArquivoMate2.*`
