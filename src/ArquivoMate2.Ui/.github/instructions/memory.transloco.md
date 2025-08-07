# Transloco-Nutzung in ArquivoMate2.Ui
#
## Pattern: Transloco mit prefix für Komponenten-Scoped Keys

- **Empfohlen:** Transloco-Direktive mit prefix für Komponenten-Scoped Keys:
  ```html
  <ng-container *transloco="let t; prefix: 'componentName'">
    <h1>{{ t('title') }}</h1>
    <p>{{ t('desc') }}</p>
  </ng-container>
  ```
- **Vorteile:**
  - Kürzere Keys im Template (`t('key')` statt `t('componentName.key')`)
  - Bessere Lesbarkeit und Wartbarkeit
  - Konsistente Struktur für alle Komponenten


## Pattern: Transloco in Angular Standalone-Komponenten

- **Import:** TranslocoDirective wird als Import in der Komponente verwendet.
- **Template:**
  - Übersetzungen werden über `<ng-container *transloco="let t">` bereitgestellt.
  - Zugriff auf Übersetzungen erfolgt mit `{{ t('key') }}` im Template.
  - Der Transloco-Pipe wird nicht verwendet.
- **Beispiel:**
  ```html
  <ng-container *transloco="let t">
    <p>{{ t('title') }}</p>
  </ng-container>
  ```
- **Komponenten-Setup:**
  ```typescript
  import { TranslocoDirective } from '@jsverse/transloco';
  // ...
  imports: [TranslocoDirective]
  ```
- **Hinweis:**
  - Die Transloco-Pipe (`| transloco`) ist nicht aktiv, stattdessen wird die Direktive verwendet.
  - Die Übersetzungsfunktion `t` ist im Template über das ng-container verfügbar.

## Beispiel aus `landingPage.component.ts` und `landingPage.component.html`
- Siehe aktuelle Implementierung:
  - Komponente: `LandingPageComponent`
  - Template: `<ng-container *transloco="let t"> ... </ng-container>`

---
Letztes Update: 2025-08-07
