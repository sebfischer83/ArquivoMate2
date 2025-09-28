# CONTRIBUTING

Willkommen und danke, dass du zu diesem Projekt beitragen möchtest! Dieses Dokument beschreibt die verbindlichen Konventionen, Patterns und Workflows für Beiträge.

> Quelle der Wahrheit für wiederkehrende Muster: `.github/instructions/memory.instruction.md`

Bitte vor jedem PR kurz prüfen, ob neue implizite Regeln als neues Pattern dort ergänzt werden sollten.

---
## Inhaltsverzeichnis
1. Grundprinzipien
2. Architektur & Struktur
3. Patterns (Verbindlich)
4. Internationalisierung (i18n)
5. Angular / Frontend Stilregeln
6. Komponenten & Platzierung
7. Commit-Konventionen (Conventional Commits)
8. Branch & PR Workflow
9. Tests
10. Fehlermeldungen & Logging
11. Einführung neuer Patterns
12. Lokale Quality Checks (optional Script)

---
## 1. Grundprinzipien
- Saubere, kleine Commits mit eindeutigem Zweck.
- Keine toten/commented-out Codeblöcke im finalen PR.
- Bevorzugt reine, side-effect-freie Funktionen für Logik.
- Keine „magischen Strings“ für i18n-Schlüssel – konsistent benennen.

## 2. Architektur & Struktur
- Angular 20 Standalone Components.
- Feature Pages unter: `src/app/main/pages/<feature>/`.
- Wiederverwendbare generische Komponenten unter: `src/app/components/`.
- Services unter: `src/app/services/` (API-spezifisch) oder `src/app/main/pages/<feature>/services/` (falls streng lokal).
- Generierter API-Client: `src/app/client/` (nicht manuell ändern, sondern OpenAPI Regeneration nutzen).

## 3. Patterns (Verbindlich)
Aktueller Auszug (vollständige Liste siehe `.github/instructions/memory.instruction.md`):

| Pattern | Beschreibung |
|---------|--------------|
| 001 | Kein neues `*ngIf` mehr – stattdessen Angular Control Flow `@if/@else` verwenden. |
| 002 | Wiederverwendbare (nicht page-spezifische) Komponenten unter `src/app/components/` ablegen. |
| 003 | Transloco: Keine `prefix:` oder `read:` Nutzung – Übersetzungs-Keys immer voll qualifiziert, z.B. `t('Document.Download')`. |

Wenn du ein neues wiederkehrendes Muster erkennst, siehe Abschnitt 11.

## 4. Internationalisierung (i18n)
- Loader lädt primär aus `/i18n/<lang>.json` (Dateien liegen in `public/i18n`).
- Fallback: `/assets/i18n` (nur falls später umgezogen wird).
- Keine Nutzung von `prefix:` im Template. Immer: `t('Namespace.Key')`.
- Namespaces groß schreiben (z.B. `Document`, `LandingPage`).
- Neue Keys alphabetisch gruppieren, semantisch geordnet.
- Keine doppelten Keys – erst prüfen.

### Beispiel
```html
<span>{{ t('Document.Download') }}</span>
```

## 5. Angular / Frontend Stilregeln
- Control Flow: `@if/@for/@switch` statt `*ngIf/*ngFor` in neuen Templates.
- Signale (Angular Signals) statt übermäßiger RxJS-Subjects für lokalen UI-State.
- Async API: Service -> subscribe im Component-Level mit Fehlerbehandlung + visuellem Zustand.
- CSS: Komponenten-Scoped SCSS; keine globalen Utility-Klassen hinzufügen ohne Abstimmung.

## 6. Komponenten & Platzierung
- Page-spezifische UI: `src/app/main/pages/<feature>/components/...`.
- Quer-genutzte UI: `src/app/components/`.
- Pipes & Direktiven: wenn mehrfach genutzt -> `src/app/components/` oder eigenes `shared` Unterverzeichnis.

## 7. Commit-Konventionen (Conventional Commits)
Format: `<type>(optional-scope): <beschreibung>`

Empfohlene Types: `feat`, `fix`, `refactor`, `docs`, `test`, `build`, `chore`, `perf`, `style`.

Beispiele:
- `feat(document): Notizen-Composer hinzugefügt`
- `fix(pdf-viewer): worker URL Auflösung repariert`
- `refactor(i18n): Keys vollqualifiziert`

## 8. Branch & PR Workflow
- Branch Naming: `<type>/<kurz-kontext>-<ticket/issue>` (z.B. `feat/notes-edit-123`).
- PR Beschreibung: Was wurde geändert? Warum? Screenshots bei UI-Änderungen.
- Checkliste im PR (manuell):
  - [ ] Build erfolgreich
  - [ ] Keine neuen Lint-/Template-Warnungen
  - [ ] i18n Keys konsistent
  - [ ] Patterns verletzt? (Nein)

## 9. Tests
- Unit Tests für Pipes, reine Utils, Services mit Logik.
- Komponenten-Tests für kritische Interaktionslogik (z.B. Optimistic Update, Fehlerpfade).
- Mindestens Happy Path + 1 Fehlerfall pro Service.

## 10. Fehlermeldungen & Logging
- Konsistente Nutzerfehler: deutsche/englische Übersetzungen anlegen (kein Hardcode außer Debug).
- `console.log` nur für gezielte Debug-Sessions; vor Merge entfernen.

## 11. Einführung neuer Patterns
1. Pattern formulieren (kurz, prägnant, warum?).
2. In `.github/instructions/memory.instruction.md` als neue laufende Nummer hinzufügen.
3. Falls automatisierbar: Check in Pattern-Skript ergänzen.

## 12. Lokale Quality Checks (optional Script)
Empfohlenes (noch optionales) Skript (kann später fest integriert werden) zur Sicherung der Patterns:
```bash
npm run pattern-check
```
Validiert u.a.:
- Keine neuen `*ngIf`-Direktiven
- Kein `prefix:` im Transloco Template
- Keine unqualifizierten Document-Keys

## 13. Fragen / Abstimmung
Bei Unsicherheiten: kurze Notiz in PR oder Issue eröffnen. Lieber früh abstimmen als später refactoren.

---
Danke für deine Beiträge! 🙌
