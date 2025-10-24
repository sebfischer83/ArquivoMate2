IF Pattern (project-wide)
=========================

Short description
-----------------
This project uses a project-specific preprocessor pattern `@if (condition) { ... }` instead of Angular's `*ngIf` in templates.

Why
----
- Consistent template style across the codebase
- A preprocessing/template transformation step can perform extra checks and optimizations before Angular compilation

How to use
----------
Examples:

- Badge (before):

  <tui-badge *ngIf="notesCount && notesCount > 0">{{ notesCount }}</tui-badge>

  Replaced with:

  @if (notesCount && notesCount > 0) { <tui-badge>{{ notesCount }}</tui-badge> }

- Button (before):

  <button *ngIf="showLabResults">{{ t('Document.Tabs.LabResults') }}</button>

  Replaced with:

  @if (showLabResults) { <button>{{ t('Document.Tabs.LabResults') }}</button> }

Important
---------
- Use the `@if` pattern exclusively in new and modified templates.
- Keep spacing and curly braces consistent — the preprocessor expects a stable format.
- Some editors/IDEs may flag `@if` as invalid Angular syntax. That is expected because the transformation happens before Angular compiles the templates.

Recommendation
--------------
- When refactoring templates, replace `*ngIf` occurrences with `@if`.
- If CI or tests report syntax errors related to this pattern, check the preprocessing step in the build pipeline rather than the template file itself.

File created by: automated code assistant
Date: 2025-10-24
