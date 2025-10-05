# Dynamic Document Grouping & Collections

## 1. Overview
The dynamic grouping feature lets a client build an on?demand (lazy loaded) hierarchical navigation tree of a user’s documents based on an *ordered list* of grouping dimensions. No grouping configuration is persisted server?side; instead the client specifies the desired dimension order per request.

Supported dimensions (you choose the order):
- **Collection**
- **Year** (derived from `Date ?? UploadedAt`)
- **Month** (derived from `Date ?? UploadedAt`)
- **Type**
- **Language**

Each request returns only the *next* level (no deep trees in one call) enabling efficient drill?down with minimal payloads.

Collections are purely organizational labels (not permission constructs). Documents can belong to zero or many collections.

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

This endpoint is *stateless*: a *grouping* is simply an ordered array of dimension names plus an optional `path` describing the already drilled segments. You "create" a grouping by deciding the order client?side and calling the endpoint.

### 3.1 Defining (Creating) a Grouping
You do **not** persist a grouping. To "create" one:
1. Choose dimensions in desired order (e.g. `[Collection, Year, Month]`).
2. Call the grouping endpoint with an empty `path` to fetch the top level.
3. As the user clicks a node, append its `{ dimension, key }` to `path` and call again.
4. Stop when `hasChildren=false`.

Example – Root request:
```json
{
  "groups": ["Collection", "Year", "Month"],
  "path": []
}
```
Response (Collections level – truncated):
```json
{
  "dimension": "Year",
  "key": "2025",
  "label": "2025",
  "count": 128,
  "hasChildren": true
}
```

### 3.2 Request Model
```json
{
  "groups": ["Collection","Year","Month"],
  "path": [ { "dimension": "Collection", "key": "<collectionId or (none)>" } ]
}
```
- `groups`: Ordered, distinct list. All must be in the allowed set.
- `path`: Prefix subset (0..groups.Length-1). Each element’s `dimension` must match the corresponding index in `groups`.

### 3.3 Response Model (`DocumentGroupingNode`)
```json
{
  "dimension": "Year",
  "key": "2025",
  "label": "2025",
  "count": 128,
  "hasChildren": true
}
```

### 3.4 Dimension Semantics
| Dimension | Key | Label | Special Buckets |
|-----------|-----|-------|-----------------|
| Collection | Collection GUID or `(none)` | Collection name or `(None)` | `(none)` docs without collection |
| Year | YYYY or `(unknown)` | Same | `(unknown)` if no date |
| Month | 1..12 or `(unknown)` | `MM - MonthName` or `(Unknown)` | `(unknown)` if no month |
| Type | Raw or `(none)` | Raw or `(None)` | `(none)` empty |
| Language | ISO code or `(none)` | ISO code or `(None)` | `(none)` empty |

### 3.5 Ordering Rules
- Collection: `(None)` first, then label A?Z
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
- `(unknown)` ? unresolved temporal bucket (no date / month)

---
## 4. Mapping Grouping Path to Document List Query
Once the user drills to a node, you fetch the *documents* via `GET /api/documents` using the enhanced filters:

| Path Dimension | How to translate to list query |
|----------------|--------------------------------|
| Collection | Add its GUID to `collectionIds` (multi allowed). `(none)`: **omit** `collectionIds` and client?side exclude collected docs OR implement future negation if needed. |
| Year | Set `year=YYYY` |
| Month | Set `month=MM` (only meaningful together with `year` – server matches derived month) |
| Type | Set `type=...` |
| Language | Set `language=...` |

### 4.1 Examples
1. Documents in collection `c1` for 2025:
```
GET /api/documents?collectionIds=c1&year=2025
```
2. Documents in collection `c1` – March 2025 only:
```
GET /api/documents?collectionIds=c1&year=2025&month=3
```
3. German invoices (no collection filtering):
```
GET /api/documents?type=Invoice&language=de
```
4. From grouping chain (Collection -> Year -> Month -> Type):
- Path chosen: Collection=c1, Year=2025, Month=3, Type=Invoice
```
GET /api/documents?collectionIds=c1&year=2025&month=3&type=Invoice
```

### 4.2 Edge Cases
| Scenario | Handling |
|----------|----------|
| Month provided without Year | Still filters by month over `(Date ?? UploadedAt).Month` (may mix years) |
| Invalid month (<1 or >12) | Returns empty result (short?circuited) |
| Multiple collectionIds | OR semantics (document in any) |
| Document in multiple selected collections | Listed once (base query distinct by Id) |

### 4.3 Performance Notes
- Collection filter precomputes matching document IDs before pagination.
- Year/Month comparisons use the coalesced date expression `(Date ?? UploadedAt)`; consider indexing if dataset grows large.

---
## 5. Best Practices for Clients
| Goal | Recommendation |
|------|----------------|
| Persist a user’s preferred grouping | Store the `groups` array client?side (e.g. local storage / profile) |
| Fast back navigation | Cache previous level node arrays keyed by serialized `path` |
| Avoid redundant calls | Do not call the grouping endpoint when `hasChildren=false` |
| Empty result nodes | Nodes with `count=0` are never returned; no need to filter |
| Handling `(none)` | Provide a distinct UI label (e.g. “Unassigned”) |
| Handling `(unknown)` | Show a neutral icon and optional tooltip (“No date detected”) |

---
## 6. Extending Grouping (Adding a Dimension)
1. Add name to `Allowed` set in `DocumentGroupingService`.
2. Include field in base document projection (`MinDoc`).
3. Implement `GroupBy...` or reuse `GroupByString`.
4. Extend validation docs & client dimension selector.
5. Optionally add list query filter property & server filtering.

---
## 7. Frequently Asked Questions
**Q: How do I get actual document IDs from grouping?**  
A: Use the list endpoint with filters translated from the grouping path (see section 4). Grouping endpoint intentionally returns counts only.

**Q: Can I require that a document is in *all* of multiple collections?**  
A: Current semantics are OR. AND semantics would require an additional request param or post?filtering.

**Q: How do I get documents *without* any collection directly?**  
A: Call grouping to show `(None)` bucket for UX; list endpoint currently lacks an explicit `noCollection=true` flag—workaround: call without `collectionIds` and exclude IDs that appear in memberships locally, or add a small enhancement.

**Q: Are shared documents included?**  
A: Not yet; only owned documents are grouped (future extension: incorporate `DocumentAccessView`).

---
## 8. Changelog
| Date | Change |
|------|--------|
| 2025-10-05 | Initial grouping docs created |
| 2025-10-05 | Added list query filters (collectionIds, year, month, language) & path mapping section |

---
*Last updated: 2025-10-05*
