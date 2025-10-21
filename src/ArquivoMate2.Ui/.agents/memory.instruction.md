---
applyTo: '**'
---

# Agent Memory — User Preferences

- preference: Do not modify files inside `src/app/main/main-area` without explicit user permission.
- note: This applies to any edits to files under `main-area` (e.g., `main-area.page.ts`, `main-area.page.html`, `main-area.page.scss`).
- recordedAt: 2025-10-12T00:00:00Z

- confirm-dialog-usage: |
	Preferred pattern for confirmation dialogs in this project:
	- Use Taiga `TUI_CONFIRM` with `TuiDialogService.open(TUI_CONFIRM, {...})`.
	- Inject `TuiDialogService` (from `@taiga-ui/core`) as `dialogs` and call:
		`this.dialogs.open<boolean>(TUI_CONFIRM, { label, data: { content, yes, no } })`.
	- Subscribe to the returned observable and run the action when truthy.
	- Ensure `TuiConfirmService` (from `@taiga-ui/kit`) is provided at app-level or the appropriate module is imported so `TUI_CONFIRM` works at runtime.
	- Example (document component):
		`this.dialogs.open<boolean>(TUI_CONFIRM, { label: 'Akzeptieren', data: { content: msg, yes, no } }).subscribe(res => { if (res) this.acceptDocument(); });`

	recordedAt: 2025-10-21T00:00:00Z
