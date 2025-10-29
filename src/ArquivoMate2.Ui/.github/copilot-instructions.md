# GitHub Copilot instructions for this repository

When generating or editing Angular templates in this repository, ALWAYS follow the project's template convention described in `src/ArquivoMate2.Ui/docs/IF_USAGE.md`.

Key rules (must be enforced):

- Use the `@if (condition) { ... }` preprocessor pattern instead of Angular's `*ngIf` in any `.html` templates.
- Do not introduce new `*ngIf` attributes in the codebase. If you see an existing `*ngIf`, prefer converting it to the `@if` pattern and keep formatting consistent with existing code.
- When producing template snippets for developers, show both the original `*ngIf` example and the converted `@if` version.

Examples:

Before:

```html
<tui-badge *ngIf="notesCount && notesCount > 0">{{ notesCount }}</tui-badge>
```

After (preferred):

```html
@if (notesCount && notesCount > 0) { <tui-badge>{{ notesCount }}</tui-badge> }
```

Why:

- This repository uses a preprocessing step that transforms `@if` blocks into valid Angular templates. The preprocessor also enforces consistent formatting and additional checks.

Location of the rule: `src/ArquivoMate2.Ui/docs/IF_USAGE.md` — read it before making template changes.

If you cannot follow this (for example, limited to Angular-only snippets), explicitly note the exception in the generated suggestion and provide the `@if` alternative.
# ArquivoMate2.Ui — AI Coding Agent Guide

NUTZE IMMER DIE AKTUELLE memory.instruction.md DATEI ALS REFERENZ.

## Architecture Overview

- **Framework:** Angular 20+ with Taiga UI for all UI components.
- **Structure:**  
  - Main app logic in `src/app/`
  - Feature modules under `src/app/main/pages/`
  - Shared services in `src/app/services/`
  - API client code in `src/app/client/` (OpenAPI-generated)
  - Models in `src/app/models/`
  - Utilities in `src/app/utils/`
- **Routing:**  
  - Centralized in `src/app/app.routes.ts`
  - Feature pages registered as lazy modules under `/main/pages/`
- **Styling:**  
  - Global styles in `src/styles.scss`
  - Component styles in `.scss` files next to each component

## Developer Workflows

- **Start Dev Server:**  
  - `ng serve` (or VS Code task: npm start)
- **Run Unit Tests:**  
  - `ng test` (or VS Code task: npm test)
- **Build for Production:**  
  - `ng build`
- **Generate Components/Services:**  
  - `ng generate component <name>`
  - `ng generate service <name>`
- **API Client Regeneration:**  
  - OpenAPI generator config in `ng-openapi-gen.json`
  - Regenerate with `npx ng-openapi-gen` (check for custom scripts in `package.json`)

## Project-Specific Patterns

- **Taiga UI:**  
  - Use Taiga UI components for all forms, tables, dialogs, and navigation.
  - Prefer `tui-table`, `tui-input`, `tui-dialog` for new UI features.
- **Service Layer:**  
  - All HTTP/API logic goes in Angular services under `src/app/services/`.
  - Use Angular's `HttpClient` and interceptors (`src/app/interceptors/`) for authentication and error handling.
- **State Management:**  
  - Use Angular signals or RxJS for local state; avoid global state libraries.
- **API Models:**  
  - Always import DTOs from `src/app/client/models/` for API types.
- **Error Handling:**  
  - Use centralized error handling in services and interceptors.
  - Display errors using Taiga UI `tui-alert` or `tui-error` components.

## Integration Points

- **Authentication:**  
  - Auth logic in `src/app/guards/auth.guard.ts` and `src/app/interceptors/auth.interceptor.ts`
- **SignalR:**  
  - Real-time updates via `src/app/services/signalr.service.ts`
- **OpenAPI:**  
  - All backend API calls use generated client in `src/app/client/`
- **External Config:**  
  - Auth config in `public/auth-config.json`
  - Proxy config for local API in `proxy.conf.json`

## Conventions & Patterns

- **Naming:**  
  - Components: PascalCase, e.g., `DashboardComponent`
  - Services: camelCase, e.g., `stateService`
  - DTOs: Suffix with `Dto`, e.g., `DocumentDto`
- **File Placement:**  
  - Place new features in their own folder under `src/app/main/pages/`
  - Shared logic in `src/app/services/` or `src/app/utils/`
- **Testing:**  
  - Use Angular TestBed for unit tests.
  - Place tests next to source files with `.spec.ts` suffix.

## Example: Adding a Feature Page

1. Generate component:  
   `ng generate component main/pages/my-feature`
2. Register route in `app.routes.ts`
3. Add navigation link in sidebar (if needed)
4. Use Taiga UI components for UI
5. Place business logic in a service under `src/app/services/`
6. Use DTOs from `src/app/client/models/`

Beachte immer die memory.instrunction.md Datei.