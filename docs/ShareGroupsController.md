# ShareGroupsController (API)

## Summary
`ShareGroupsController` provides CRUD endpoints for managing reusable groups of user IDs, simplifying document sharing for teams that often collaborate with the same set of people.

## Current Status
The controller lives in `ArquivoMate2.API` and is protected by `[Authorize]`. Responses are wrapped in `ApiResponse<T>` and the controller delegates logic to MediatR handlers so the application layer enforces ownership rules.

## Key Components
- **Route:** `/api/share-groups`
- **Dependencies:** `IMediator` for commands/queries, `ICurrentUserService` for resolving the caller’s user ID.
- **DTOs:** `ShareGroupDto`, `CreateShareGroupRequest`, and `UpdateShareGroupRequest` (each contains `name` and `memberUserIds`).

## Process Flow
1. Clients authenticate via OIDC and call the desired endpoint.
2. The controller forwards work to handlers (`GetShareGroupsQuery`, `CreateShareGroupCommand`, `UpdateShareGroupCommand`, `DeleteShareGroupCommand`).
3. Handlers verify ownership, perform the requested change, and return results wrapped in `ApiResponse<T>`; missing or unauthorised resources surface as standard HTTP errors.

## Endpoint Summary
| Method & Route | Description | Success Response |
| --- | --- | --- |
| `GET /api/share-groups` | List groups owned by the current user. | `200 OK` with `ApiResponse<IEnumerable<ShareGroupDto>>` |
| `POST /api/share-groups` | Create a group with a name and member IDs. | `201 Created` with `Location` header and `ApiResponse<ShareGroupDto>` |
| `GET /api/share-groups/{groupId}` | Retrieve a single group owned by the caller. | `200 OK` when found, `404 Not Found` otherwise |
| `PUT /api/share-groups/{groupId}` | Update a group’s name or members. | `200 OK` with updated group or `404 Not Found` |
| `DELETE /api/share-groups/{groupId}` | Delete a group owned by the caller. | `204 No Content` |

## Operational Guidance
- All endpoints require authentication (bearer token or session cookie).
- Ownership checks ensure users can access only their own groups.
- Invalid payloads return `400 Bad Request`; missing groups return `404 Not Found`.
- Use the `id` returned from creation for follow-up updates or deletions.

## References
- `src/ArquivoMate2.API/Controllers/ShareGroupsController.cs`
- `src/ArquivoMate2.Application/Sharing/Commands`
- `src/ArquivoMate2.Application/Sharing/Queries`
