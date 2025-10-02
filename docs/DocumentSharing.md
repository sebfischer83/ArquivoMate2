# Document Sharing and Permission Flags

This document summarizes how collaborative access to documents is modelled and enforced in ArquivoMate2 after the introduction of permission flags.

## Core Concepts

### `DocumentShare`
Each manual share is stored as a `DocumentShare` aggregate containing the document identifier, the owner that granted access, the share target (user or group), the timestamp of the grant, and the granted permission set. The permission payload is normalized so that `Read` access is always implied whenever other capabilities are granted.【F:src/ArquivoMate2.Domain/Sharing/DocumentShare.cs†L6-L47】

### Permission Flags
Permissions are represented through the `[Flags]` enum `DocumentPermissions`. It currently exposes `Read`, `Edit`, and `Delete` values plus a combined `All` shortcut. `Read` remains the baseline capability and is automatically added if callers omit it. Legacy APIs can continue to use the deprecated `CanEdit` boolean, which now proxies into the new flag set for backward compatibility.【F:src/ArquivoMate2.Shared/Models/Sharing/DocumentPermissions.cs†L1-L12】【F:src/ArquivoMate2.Shared/Models/Sharing/CreateDocumentShareRequest.cs†L1-L24】

### Share Targets and Automation Rules
Shares can be created manually for individual users or groups via the `ShareTarget` abstraction. Automation continues to rely on `ShareAutomationRule` entities; whenever a rule triggers, the resulting `DocumentShare` uses the same permission semantics as manual shares, enabling owners to auto-share documents with edit or delete rights if desired.

## Access Projection

Access decisions are backed by the `DocumentAccessView`, a Marten projection keyed by the document identifier. The view tracks:

- direct user assignments (`DirectUserPermissions`)
- group assignments (`GroupPermissions`)
- per-user aggregated permissions after group expansion (`EffectiveUserPermissions`)
- convenience hash sets for read, edit, and delete participants (`EffectiveUserIds`, `EffectiveEditUserIds`, `EffectiveDeleteUserIds`)
- bookkeeping counters for direct user and group shares

The updater merges new shares, collapses duplicate grants by bitwise OR-ing their permission flags, expands group membership, and materializes the effective permission lookups that are later consumed by the API and search index.【F:src/ArquivoMate2.Infrastructure/Persistance/DocumentAccessView.cs†L1-L19】【F:src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessUpdater.cs†L1-L125】

## Authorization Flow

When APIs need to enforce access, they call into `DocumentAccessService`. The service first treats document owners as implicit `All` permission holders. For non-owners it reads the `DocumentAccessView` and checks whether the aggregated permission flags include the requested capability via the reusable `HasPermissionAsync` helper. Convenience methods `HasAccessToDocumentAsync` and `HasEditAccessToDocumentAsync` are thin wrappers over the new helper.【F:src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessService.cs†L1-L66】

The service also exposes `GetSharedDocumentIdsAsync`, which returns the set of documents where the user is listed in `EffectiveUserIds` but is not the owner—this continues to drive the "Shared with me" experience.【F:src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessService.cs†L68-L76】

## Search Index Synchronization

Whenever the updater changes a document's sharing state it sends the list of non-owner readers to the search index so that search results respect sharing constraints. The same normalized permission logic ensures that new flag combinations automatically flow into the projection and derived sets.【F:src/ArquivoMate2.Infrastructure/Services/Sharing/DocumentAccessUpdater.cs†L29-L93】

## Extensibility Considerations

- **New permissions**: introducing a new flag only requires extending the `DocumentPermissions` enum and expanding the projection to materialize the derived set if needed.
- **UI/API compatibility**: clients can start sending the new flag combinations immediately. Older clients can continue toggling `CanEdit` until they migrate.
- **Group dynamics**: whenever group membership changes, re-running the updater guarantees that effective permissions stay aligned with current group members.
