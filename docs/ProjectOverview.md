# ArquivoMate2 Project Overview

This document summarizes the solution structure so it can be referenced in future tasks. It now contains consolidated details about the solution, grouping and sharing subsystems.

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

Use this table to quickly locate the correct project when adding features or tests.

---

## Project Details

### ArquivoMate2.API
Purpose: ASP.NET Core Web API hosting endpoints and real-time hubs.
Key Elements:
- Controllers: `DocumentsController`, `EmailController`, `ImportHistoryController`, `CollectionsController`, `UsersController`, `PartiesController`, `DocumentNotesController`, `DocumentSharesController`, `ShareGroupsController`, `ShareAutomationRulesController`, `DocumentGroupingController`, `DeliveryController`, `PublicShareController`, `MaintenanceController`
- SignalR Hub: `DocumentProcessingHub`
- Notification implementation: `SignalRDocumentProcessingNotifier`
- Composition root in `Program.cs`

API response format (global change)
- The API now returns a consistent wrapper for JSON responses: `ApiResponse<T>` for successful results and RFC7807 `ProblemDetails` / `ValidationProblemDetails` for errors.
- Controllers were updated so their action signatures and `ProducesResponseType` attributes document `ApiResponse<T>` for 2xx JSON responses. File streaming endpoints (e.g. delivery, public share, maintenance zip) remain returning `FileResult` as before.
- A global MVC filter (`ApiResponseWrapperFilter`) plus middleware (`ProblemDetailsMiddleware`) and a Swagger operation filter produce consistent runtime behavior and OpenAPI docs.

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
- Import lifecycle commands/events
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
- Api models: `ApiResponse<T>` (new) for consistent success responses
Pure serialization models without business logic.

### Tests
Separated by layer to keep fast unit tests isolated from integration concerns.
Current Test Projects:
- `ArquivoMate2.Domain.Tests`: Pure domain model tests (entities, value objects, event application)
- `ArquivoMate2.Application.Tests`: Application layer tests (simple model tests now, extend with handler tests & mocks)
- `ArquivoMate2.Infrastructure.Tests`: Infrastructure service tests (e.g., path, storage abstractions). Extend with integration tests later.
- Legacy `ArquivoMate2.Tests`: Original placeholder; migrate or remove after porting tests.

---

## Document Sharing & Permission Flags

This section consolidates sharing-related documentation into the project overview.

### Core Concepts

- Document shares are represented by the `DocumentShare` aggregate which contains the `DocumentId`, `OwnerUserId`, a `ShareTarget` (user or group), timestamp and permission flags. Permissions are normalized so that `Read` is always implied when others are granted.
- Permissions use a `[Flags]` enum `DocumentPermissions` with values `Read`, `Edit`, `Delete`, and `All`. Legacy `CanEdit` boolean proxies into this flag set for backwards compatibility.
- Share automation rules (`ShareAutomationRule`) can create shares automatically using the same permission semantics.

### Access Projection

- `DocumentAccessView` is a Marten projection keyed by the document id. It tracks direct user permissions, group permissions, effective per-user permissions after group expansion, and sets for quick lookup (`EffectiveUserIds`, `EffectiveEditUserIds`, `EffectiveDeleteUserIds`). The updater consolidates permissions (bitwise OR) and materializes derived collections consumed by APIs and the search index.

### Authorization Flow

- APIs call `DocumentAccessService` to enforce access. Owners are treated as implicit holders of all permissions. For others the service looks up `DocumentAccessView` and validates permission flags.
- Convenience methods `HasAccessToDocumentAsync` and `HasEditAccessToDocumentAsync` wrap the generic `HasPermissionAsync` helper.

### Extensibility

- New permission flags require updating the enum and, if needed, the projection logic to expose derived sets. Clients may send flag combinations directly; old clients can continue to use `CanEdit` until migrated.

References: `src/ArquivoMate2.Domain/Sharing/DocumentShare.cs`, `src/ArquivoMate2.Shared/Models/Sharing/DocumentPermissions.cs`, `src/ArquivoMate2.Infrastructure/Persistance/DocumentAccessView.cs`, `src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessUpdater.cs`, `src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessService.cs`.

---

## Dynamic Document Grouping & Collections

This section consolidates the grouping docs describing how dynamic grouping works.

### Overview
The dynamic grouping feature lets a client build an on-demand hierarchical navigation tree of a user's documents (owned and shared) based on an ordered list of grouping dimensions supplied per request. No grouping configuration is persisted server-side; the client provides the dimension order.

Supported dimensions:
- Collection
- Year (derived from `Date ?? UploadedAt`)
- Month (derived from `Date ?? UploadedAt`)
- Type
- Language

Requests return the next level only (lazy drill-down) enabling efficient navigation.

Collections are organizational only and are user-owned; shared documents are placed in a `(shared)` bucket at the Collection level and do not appear inside another user's collections.

### Collections API
Base route: `/api/collections` (authenticated)
- List, Get, Create, Update, Delete collections
- Assign documents to collections and remove documents from collections

Notes:
- Collection names are unique per user.
- Deleting a collection removes memberships but not documents.

### Grouping API
Endpoint: `POST /api/documents/grouping`

- Stateless: request contains an ordered dimension array and optional path describing drill-down state.
- `(shared)` is a special key representing documents shared with the caller. `(none)` represents absence (unassigned).
- The client may filter results afterward using `GET /api/documents` with query parameters (collectionIds, year, month, type, language).

### Ordering and Buckets
- Collection order: `(None)`, `(Shared)`, then collection names A→Z
- Year / Month: numeric DESC, `(Unknown)` last
- Type / Language: `(None)` first, then A→Z

Validation rules and examples are preserved from the original docs; see code references for implementation details.

---

## API Response & OpenAPI Changes

- All controller JSON responses are documented using the generic `ApiResponse<T>` wrapper to make the OpenAPI specification precise and consistent for generated clients.
- Runtime behavior:
  - Successful JSON responses are wrapped in `ApiResponse<T>` (filter/middleware ensures controllers returning plain DTOs are wrapped at runtime).
  - Errors are returned as `ProblemDetails` (`application/problem+json`). Model validation failures produce `ValidationProblemDetails` with an `errors` dictionary.
- File-streaming endpoints (delivery, public share, maintenance export) continue to return `FileResult`/binary responses unchanged.
- The Swagger UI now shows response schemas with the `ApiResponse<T>` envelope for 2xx responses.

---

## Cross-Cutting Concerns
- User Context: Provided via `ICurrentUserService` (Infrastructure implementation `CurrentUserService`).
- File & Path Handling: Abstracted by `IPathService` with implementation in Infrastructure.
- External Integrations: S3 (storage & delivery), BunnyCDN (delivery), OpenAI (LLM), Email (IMAP/POP3), Search (custom client), SignalR for real-time notifications.
- Background Processing: `EmailDocumentBackgroundService` for periodic email ingestion and document upload.

---

## Potential Enhancements (Not Implemented Yet)
- Add comprehensive test coverage (domain event tests, handler tests with in-memory fakes).
- Centralized exception handling & logging strategy documentation.
- Validation layer (e.g., FluentValidation) for incoming API DTOs.
- Consistent result wrapper or problem details for API responses.

---

## Layer Interaction Summary
API -> Application (commands/queries) -> Domain (state changes/events)
Application <- Infrastructure implementations through interfaces
Shared <-> API & Application (data contracts)

---

*Last updated: 2025-10-05*
