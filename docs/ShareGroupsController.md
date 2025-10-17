# ShareGroupsController (API)

This document describes the `ShareGroupsController` in the API: its purpose, behavior, endpoints and example usage.

Overview
- Controller: `ArquivoMate2.API.Controllers.ShareGroupsController`
- Base route: `api/share-groups`
- Authorization: requires an authenticated user (`[Authorize]`).
- Purpose: Manage user share groups — named collections of user IDs used to simplify document sharing.

Key details
- The controller uses `IMediator` (MediatR) for commands and queries and `ICurrentUserService` to resolve the current user's ID.
- Successful JSON responses are wrapped in the project's `ApiResponse<T>` envelope.

Available endpoints

1) List
- Method: `GET /api/share-groups`
- Description: Returns all share groups owned by the current user.
- Auth: required
- Response: `200 OK` with `ApiResponse<IEnumerable<ShareGroupDto>>`
- Example:
  - curl: `curl -H "Authorization: Bearer <token>" https://api.example.com/api/share-groups`

2) Create
- Method: `POST /api/share-groups`
- Description: Creates a new share group with a name and members.
- Auth: required
- Request body (JSON):
  - `name` (string) — group name
  - `memberUserIds` (string[])
- Response:
  - `201 Created` with `ApiResponse<ShareGroupDto>` and a `Location` header pointing to `GetById`.
- Errors: `400 Bad Request` when the body is missing or invalid.
- Example:
  - curl:
    ```
    curl -X POST \
      -H "Authorization: Bearer <token>" \
      -H "Content-Type: application/json" \
      -d '{"name":"Accounting","memberUserIds":["user-a","user-b"]}' \
      https://api.example.com/api/share-groups
    ```

3) GetById
- Method: `GET /api/share-groups/{groupId}`
- Description: Retrieves the share group with the specified ID (only groups owned by the current user).
- Auth: required
- Response:
  - `200 OK` with `ApiResponse<ShareGroupDto>` when found
  - `404 Not Found` when not found or not accessible
- Note: Implementation reads the user's groups and filters by ID.

4) Update
- Method: `PUT /api/share-groups/{groupId}`
- Description: Updates name or members of an existing group.
- Auth: required
- Request body (JSON): same shape as Create (`name`, `memberUserIds`).
- Response:
  - `200 OK` with `ApiResponse<ShareGroupDto>` on success
  - `400 Bad Request` when body is missing/invalid
  - `404 Not Found` when group not found or access denied
- Example:
  - curl:
    ```
    curl -X PUT \
      -H "Authorization: Bearer <token>" \
      -H "Content-Type: application/json" \
      -d '{"name":"New Group","memberUserIds":["user-c"]}' \
      https://api.example.com/api/share-groups/<groupId>
    ```

5) Delete
- Method: `DELETE /api/share-groups/{groupId}`
- Description: Deletes a group owned by the current user.
- Auth: required
- Response:
  - `204 No Content` on success
  - `404 Not Found` when the group is missing or not accessible
- Example: `curl -X DELETE -H "Authorization: Bearer <token>" https://api.example.com/api/share-groups/<groupId>`

DTOs / Requests (expected shapes)
- `CreateShareGroupRequest` (example):
  - `name`: string
  - `memberUserIds`: string[]
- `UpdateShareGroupRequest`: same as Create
- `ShareGroupDto` (example fields):
  - `id`: string
  - `name`: string
  - `memberUserIds`: string[]
  - `ownerUserId`: string

Security & access rules
- All endpoints require authentication. The controller uses `ICurrentUserService` to obtain the current user ID and restrict operations to groups owned by that user.
- `GetById`, `Update` and `Delete` ensure the requested group belongs to the caller. For `GetById` the controller loads the user's groups and filters by ID.

Implementation notes
- Business logic is delegated to MediatR handlers via commands and queries (`GetShareGroupsQuery`, `CreateShareGroupCommand`, `UpdateShareGroupCommand`, `DeleteShareGroupCommand`).
- `Create` returns `CreatedAtAction(nameof(GetById), new { groupId = group.Id }, group)` which sets the `Location` header.

Error handling
- Invalid input: `400 Bad Request` (e.g. null body)
- Not found / no access: `404 Not Found`
- Successful deletion: `204 No Content`
- Successful creation: `201 Created` with the created group in the response body

Usage tips
- Use a valid access token or authenticated cookie according to the project's OIDC setup.
- Group IDs are strings; use the `id` returned by the Create call for subsequent updates or deletion.

Code references
- Controller source: `src/ArquivoMate2.API/Controllers/ShareGroupsController.cs`
- Related commands/queries: search for `GetShareGroupsQuery`, `CreateShareGroupCommand`, `UpdateShareGroupCommand`, `DeleteShareGroupCommand` in the application project.

Summary
- `ShareGroupsController` provides a small CRUD API for managing a user's share groups. It delegates logic to MediatR handlers and enforces owner-scoped access using `ICurrentUserService`.