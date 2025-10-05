# Dynamic Document Grouping & Collections

## 1. Overview
The dynamic grouping feature lets a client build an on?demand (lazy loaded) hierarchical navigation tree of a user’s documents (owned **and** shared with the user) based on an *ordered list* of grouping dimensions. No grouping configuration is persisted server?side; instead the client specifies the desired dimension order per request.

Supported dimensions (you choose the order):
- **Collection**
- **Year** (derived from `Date ?? UploadedAt`)
- **Month** (derived from `Date ?? UploadedAt`)
- **Type**
- **Language**

Each request returns only the *next* level (no deep trees in one call) enabling efficient drill?down with minimal payloads.

Collections are purely organizational labels (not permission constructs). Documents can belong to zero or many collections. Shared documents never appear under a user collection; they are placed in a dedicated `(shared)` bucket at the Collection level.

---
## 2. Collections API (Organizational Layer)
All endpoints require authentication and operate only on the calling user’s data.

Base route: `/api/collections`

| Action | Method & Route | Body | Response |
|--------|----------------|------|----------|
| List collections | GET `/api/collections` | – | 200 `[CollectionDto]` |
| Get collection | GET `/api/collections/{id}` | – | 200 / 404 |
| Create collection | POST `/api/collections` | `{ "name": "Invoices" }` | 201 + `CollectionDto` |
| Update collection | PUT `/api/collections/{id}` | `{ "name": "Renamed" }` | 200 / 404 |
| Delete collection | DELETE `/api/collections/{id}` | – | 204 / 404 |
| Assign docs | POST `/api/collections/{id}/assign` | `{ "collectionId":"(optional or same)", "documentIds":[..] }` | 200 `{ createdCount: n }` |
| Remove doc | DELETE `/api/collections/{id}/documents/{documentId}` | – | 204 / 404 |

### Notes
- `createdCount` counts only *new* assignments (silently skips already assigned or foreign docs).
- Collection names are unique per user (case?insensitive).
- Deleting a collection removes its memberships (documents remain intact).

---
## 3. Grouping API (Dynamic Hierarchies)
Endpoint: `POST /api/documents/grouping`

This endpoint is *stateless*: a *grouping* is an ordered dimension array plus an optional `path` describing the already drilled segments. Owned and shared documents are combined; shared docs are clearly separated in the Collection dimension.

### 3.1 Defining (Creating) a Grouping
You do **not** persist a grouping. To "create" one:
1. Choose dimensions in desired order (e.g. `[Collection, Year, Month]`).
2. Call the endpoint with an empty `path` for the root.
3. On node click, append `{ dimension, key }` to `path` and call again.
4. Stop when `hasChildren=false`.

### 3.2 Request Model
```json
{
  "groups": ["Collection","Year","Month"],
  "path": [ { "dimension": "Collection", "key": "(shared)" } ]
}
```
`(shared)` selects the shared documents bucket.

### 3.3 Response Node (`DocumentGroupingNode`)
```json
{
  "dimension": "Collection",
  "key": "(shared)",
  "label": "(Shared)",
  "count": 12,
  "hasChildren": true
}
```

### 3.4 Dimension Semantics
| Dimension | Key | Label | Special Buckets |
|-----------|-----|-------|-----------------|
| Collection | Collection GUID, `(none)`, `(shared)` | Name or `(None)` / `(Shared)` | `(none)` = own docs w/o collection; `(shared)` = shared docs |
| Year | YYYY or `(unknown)` | Same | `(unknown)` if no date |
| Month | 1..12 or `(unknown)` | `MM - MonthName` / `(Unknown)` | `(unknown)` if no month |
| Type | Raw or `(none)` | Raw / `(None)` | `(none)` empty |
| Language | ISO code or `(none)` | ISO / `(None)` | `(none)` empty |

### 3.5 Ordering Rules
Collection order priority: `(None)` ? `(Shared)` ? collection names (A?Z). Remaining dimensions unchanged:
- Year: numeric DESC, `(Unknown)` last
- Month: numeric DESC, `(Unknown)` last
- Type / Language: `(None)` first, then A?Z

### 3.6 Validation Errors (HTTP 400)
- Empty `groups`
- Unsupported dimension
- Duplicate dimension
- `path` longer than `groups`
- Order mismatch (prefix rule broken)

### 3.7 Special Keys
- `(none)` ? absence bucket (no collection / empty type / empty language)
- `(shared)` ? documents shared **with** the user (never mixed into `(none)`)
- `(unknown)` ? unresolved temporal bucket (no date / month)

---
## 4. Mapping Grouping Path to Document List Query
After drilling to a node, fetch documents via `GET /api/documents` using filters:

| Path Dimension | List Query Translation |
|----------------|------------------------|
| Collection GUID | `collectionIds=<id>` |
| `(none)` | Omit `collectionIds`; client optionally excludes those in user collections (no explicit flag yet) |
| `(shared)` | Use a future enhancement: currently list endpoint does not have `sharedOnly`; client filters by difference (shared IDs) if needed |
| Year | `year=YYYY` |
| Month | `month=MM` (with or without year) |
| Type | `type=...` |
| Language | `language=...` |

> NOTE: A dedicated `sharedOnly` or `excludeCollections` filter can be added later for server-side `(shared)` / `(none)` resolution in document listing.

### 4.1 Examples
1. Shared documents for 2025:
```
POST /api/documents/grouping {"groups":["Collection","Year"],"path":[{"dimension":"Collection","key":"(shared)"}]}
```
Then:
```
GET /api/documents?year=2025   (client narrows locally to shared subset)
```
2. Collection `c1` March 2025:
```
GET /api/documents?collectionIds=c1&year=2025&month=3
```

---
## 5. Best Practices for Clients
| Goal | Recommendation |
|------|----------------|
| Distinguish shared vs unassigned | Rely on `(shared)` vs `(none)` buckets |
| Avoid mixing states | Never assume `(none)` contains shared docs |
| Cache shared IDs | Cache last shared doc ID set for faster local filtering |
| User clarity | Provide tooltip for `(shared)` (e.g. “Documents shared with you”) |

---
## 6. Extending Grouping (Adding a Dimension)
1. Add name to `Allowed` in `DocumentGroupingService`.
2. Include field in projection (`MinDoc`).
3. Implement grouping method.
4. Update docs & client selector.
5. Add optional list filter param.

---
## 7. Frequently Asked Questions
**Q: Are shared documents included?**  
Yes. They appear only in the `(shared)` bucket under the Collection dimension.

**Q: Can shared docs appear inside my own collections?**  
No. Collections are user-owned; memberships do not cross ownership.

**Q: How do I list only shared documents via /api/documents?**  
Currently you must fetch documents and client-filter by intersection with the shared bucket. A native `sharedOnly` parameter can be added later.

**Q: Are unprocessed shared documents included?**  
No. Only processed, not deleted shared docs are grouped for clarity.

(Other FAQ items unchanged.)

---
## 8. Changelog
| Date | Change |
|------|--------|
| 2025-10-05 | Initial grouping docs created |
| 2025-10-05 | Added list query filters (collectionIds, year, month, language) & path mapping section |
| 2025-10-05 | Added shared documents grouping with `(shared)` bucket |

---
*Last updated: 2025-10-05*
