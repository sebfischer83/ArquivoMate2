# ArquivoMate2 Project Overview

This document summarizes the solution structure so it can be referenced in future tasks.

## Solution Structure (Layered Architecture)

1. API (Presentation)
2. Application (Use Cases / Orchestrators)
3. Domain (Core Model & Domain Events)
4. Infrastructure (Implementations / External Integrations)
5. Shared (Cross-layer DTOs / Contracts)
6. Tests (Unit / Integration Tests)

Dependencies follow clean architecture principles:
- API depends on Application (+ Shared) only.
- Application depends on Domain (+ Shared) abstractions.
- Infrastructure depends on Application & Domain to provide implementations.
- Shared contains pure cross-layer contracts (no dependencies on other projects except BCL).
- Tests can reference any project as needed.

---
## .NET Project Inventory

| Project | Target Framework | Responsibilities | Notable Dependencies |
| --- | --- | --- | --- |
| `src/ArquivoMate2.API` | net9.0 | ASP.NET Core host, HTTP endpoints, delivery token issuance, background job scheduling. | ASP.NET Core, Hangfire, MediatR, Marten (query session), AutoMapper |
| `src/ArquivoMate2.Application` | net9.0 | Application services, commands/handlers, background processing, service interfaces. | MediatR, Marten abstractions, Mime types, OCR settings, custom interfaces |
| `src/ArquivoMate2.Domain` | net9.0 | Aggregates (`Document`), processes (`ImportProcess`), domain events, value objects. | Pure BCL, shared enums |
| `src/ArquivoMate2.Infrastructure` | net9.0 | Marten projections, storage/delivery providers, email connectors, search + LLM integrations. | Marten, Meilisearch, Minio/S3, OpenAI, EasyCaching |
| `src/ArquivoMate2.Shared` | net9.0 | DTOs and request/response contracts shared with UI and external clients. | None beyond BCL |
| `tests/ArquivoMate2.Application.Tests` | net9.0 | Unit tests for application handlers/services. | xUnit, FluentAssertions (check csproj for references) |
| `tests/ArquivoMate2.Domain.Tests` | net9.0 | Unit tests for domain aggregates and value objects. | xUnit, FluentAssertions |
| `tests/ArquivoMate2.Infrastructure.Tests` | net9.0 | Tests for infrastructure services (paths, storage adapters). | xUnit, FluentAssertions |
| `tests/ArquivoMate2.Tests` | net9.0 | Legacy test project for regression coverage; migrate scenarios into layered tests over time. | xUnit |

Use this table to quickly locate the correct project when adding features or tests.

---
## Project Details

### ArquivoMate2.API
Purpose: ASP.NET Core Web API hosting endpoints and real-time hubs.
Key Elements:
- Controllers: `DocumentsController`, `EmailController`, `ImportHistoryController`
- SignalR Hub: `DocumentProcessingHub`
- Notification implementation: `SignalRDocumentProcessingNotifier`
- Composition root in `Program.cs`

### ArquivoMate2.Application
Purpose: Application layer containing use-case orchestration, commands, handlers, and service abstractions.
Key Elements:
- Commands & Handlers: (Upload, Process, Update, Hide, UpdateIndex)
- Services: `DocumentProcessingService`, `EmailDocumentBackgroundService`
- Interfaces (Ports): `IStorageProvider`, `IEmailService`, `IChatBot`, `ISearchClient`, `IDeliveryProvider`, `IThumbnailService`, `IFileMetadataService`, `IPathService`, `ICurrentUserService`, repositories, notifier
- Models: `EmailDocument`, `DocumentAnalysisResult`, `PartyInfo`
- Settings: `OcrSettings`
Responsibility: Coordinate domain operations, invoke domain logic, publish notifications.

### ArquivoMate2.Domain
Purpose: Core domain model with entities, value objects, and domain events.
Key Elements:
- Entity: `Document`
- Aggregates / Processes: `ImportProcess`
- Value Objects: `Paths`, `DocumentMetadata`
- Events: `DocumentUploaded`, `DocumentFilesPrepared`, `DocumentContentExtracted`, `DocumentProcessed`, `DocumentUpdated`, `DocumentChatBotDataReceived`
- Import lifecycle commands/events: `InitDocumentImport`, `StartDocumentImport`, `MarkSucceededDocumentImport` (typo likely: Succeded), `MarkFailedDocumentImport`, `HideDocumentImport`
- Email domain objects: `EmailSettings`, `EmailCriteria`, `ProcessedEmail`
- Search projection model: `SearchDocument`
Defines invariants and state transitions independent of infrastructure.

### ArquivoMate2.Infrastructure
Purpose: Implement external integrations & persistence-related concerns.
Key Elements:
- Services: Email providers (IMAP/POP3/Null), Storage (S3), Delivery providers (BunnyCDN, S3), LLM chat bots (OpenAI), Search client, File metadata, Thumbnail generation, Path resolution, Current user service, Document processor
- Configuration factories: ChatBot, Auth, StorageProvider, DeliveryProvider
- Repositories: EmailSettingsRepository, EmailCriteriaRepository, ProcessedEmailRepository
- Persistence projections / views: `DocumentProjection`, `DocumentView`, `ImportHistoryProjection`, `ImportHistoryView`
- Mapping helpers: `DocumentMapping`, `ImportHistoryMapping`, `EmailCriteriaMapping`, `EnumTranslationResolver`
- Settings objects: `OpenAISettings`, `ChatBotSettings`, storage & delivery provider settings
Implements Application interfaces, wires external systems.

### ArquivoMate2.Shared
Purpose: Cross-layer DTOs & contracts exchanged between API and clients (and sometimes Application layer).
Key Elements:
- Document DTOs: `DocumentDto`, `DocumentListDto`, `DocumentListItemDto`, `DocumentListRequestDto`, `DocumentStatsDto`, `DocumentEventDto`, `UpdateDocumentFieldsDto`
- Import DTOs: `ImportHistoryListDto`, `ImportHistoryListItemDto`, `ImportHistoryListRequestDto`
- Email DTOs: `SaveEmailSettingsRequest`, `SaveEmailCriteriaRequest`, `EmailCriteriaDto`, `EmailModels`, `EmailProviderType`
- Notifications / Requests: `DocumentProcessingNotification`, `UploadDocumentRequest`
- Enums: `ProcessingStatus`, `ImportSource`
Pure serialization models without business logic.

### Tests
Separated by layer to keep fast unit tests isolated from integration concerns.
Current Test Projects:
- `ArquivoMate2.Domain.Tests`: Pure domain model tests (entities, value objects, event application)
- `ArquivoMate2.Application.Tests`: Application layer tests (simple model tests now, extend with handler tests & mocks)
- `ArquivoMate2.Infrastructure.Tests`: Infrastructure service tests (e.g., path, storage abstractions). Extend with integration tests later.
- Legacy `ArquivoMate2.Tests`: Original placeholder; migrate or remove after porting tests.

---
## Cross-Cutting Concerns
- User Context: Provided via `ICurrentUserService` (Infrastructure implementation `CurrentUserService`).
- File & Path Handling: Abstracted by `IPathService` with implementation in Infrastructure.
- External Integrations: S3 (storage & delivery), BunnyCDN (delivery), OpenAI (LLM), Email (IMAP/POP3), Search (custom client), SignalR for real-time notifications.
- Background Processing: `EmailDocumentBackgroundService` for periodic email ingestion and document upload.

## Potential Enhancements (Not Implemented Yet)
- Add comprehensive test coverage (domain event tests, handler tests with in-memory fakes).
- Centralized exception handling & logging strategy documentation.
- Validation layer (e.g., FluentValidation) for incoming API DTOs.
- Consistent result wrapper or problem details for API responses.
- Integration test suite (Marten + Meili + S3 via testcontainers) under a dedicated `*.IntegrationTests` project.

## Layer Interaction Summary
API -> Application (commands/queries) -> Domain (state changes/events)
Infrastructure <- Application (through interfaces)
Shared <-> API & Application (data contracts)

---
Generated for internal assistant context. This file can be updated as the architecture evolves.
