# AI Assistant Instructions (LLMs)

Purpose
-------
This file contains explicit instructions for any AI assistant (LLM) that interacts with this repository. If you are an AI assistant, follow these rules before generating code, patches, or suggestions.

Mandatory rules
---------------
1. Always read and follow `src/ArquivoMate2.Ui/docs/IF_USAGE.md`. It defines the project's template conventions.
2. When editing or generating Angular templates, DO NOT use `*ngIf`. Use the project's `@if (condition) { ... }` preprocessor pattern instead.
3. If you generate template snippets, include both the original `*ngIf` example (if relevant) and the converted `@if` version.
4. Do not remove or contradict this file or `IF_USAGE.md` in generated changes.

How to behave as an assistant
-----------------------------
- Before proposing changes to `.html` files, scan for any existing `*ngIf` occurrences and suggest converting them to `@if`.
- If the user explicitly asks to emit Angular-native templates (without the preprocessor), warn about the repository convention and provide the `@if` equivalent.
- When creating PR suggestions or code, add a short comment referencing `src/ArquivoMate2.Ui/docs/IF_USAGE.md` so reviewers know why the `@if` form was used.

Examples
--------
Input request: "Add a conditional badge showing notes count"
Assistant output should include:

```html
@if (notesCount && notesCount > 0) { <tui-badge>{{ notesCount }}</tui-badge> }
```

And optionally show the original `*ngIf` form with explanation:

```html
<!-- original Angular form (not preferred in this repo) -->
<tui-badge *ngIf="notesCount && notesCount > 0">{{ notesCount }}</tui-badge>
```

If you cannot follow these rules for technical reasons, clearly state the limitation and ask the user whether to proceed with the repo-preferred style or an exception.

Languages
---------
Follow the repository language norms for UI text (Transloco keys etc.). Default to English in generated code comments; use German-only content only if the user requests it.

Location of convention: `src/ArquivoMate2.Ui/docs/IF_USAGE.md`
