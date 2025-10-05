# ArquivoMate2Ui

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 20.0.1.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.

## Reusable Components

### `am-document-card-grid`
A standalone component that displays a paginated grid of document cards (thumbnail + meta info).

Inputs:
- `items: DocumentListItemDto[]` current page items
- `totalCount: number` total number of documents
- `page: number` current page (1-based)
- `pageCount: number` total pages
- `pageSize: number` current page size
- `pageSizeOptions: number[]` selectable sizes (default `[10,20,50]`)
- `loading: boolean` backend call in-flight
- `error: string | null` optional error message
- `alwaysShowControls: boolean` force pagination + size selector even with a single page (default true)
- `loadingVariant: 'shimmer' | 'minimal'` choose loading visuals (default `shimmer`)
- `showOverlayOnInitialLoad: boolean` overlay spinner only for very first load (reduces flicker) default true
- `preserveItemsWhileLoading: boolean` keep current items visible on reload instead of skeletons (default false)
- `loadingDebounceMs: number` delay before loading visuals appear (default 120ms)
- `loadingMinVisibleMs: number` minimum visible duration of loading state once shown (default 250ms)
- `cardMinWidth: number` Mindestbreite einer Karte in Pixeln (default 240, zuvor 300 / implizit 220) – steuert die responsiven Spalten

Outputs:
- `pageChange(page: number)` when user navigates pages
- `pageSizeChange(size: number)` when user selects new page size
- `itemClick(DocumentListItemDto)` when a card is clicked
- `reload()` manual reload requested

Loading behaviour summary:
1. Request starts -> waits `loadingDebounceMs`; if request finishes earlier => no flicker.
2. After debounce passes -> loading visuals appear and stay at least `loadingMinVisibleMs`.
3. First load: optional overlay + skeletons
4. Subsequent loads: skeletons unless `preserveItemsWhileLoading=true`
5. Empty & not loading -> empty message

Recommended tuning examples:
```html
<!-- Ultra responsive (less flicker) -->
<am-document-card-grid [loadingDebounceMs]="200" [loadingMinVisibleMs]="300" />

<!-- Aggressive immediate feedback -->
<am-document-card-grid [loadingDebounceMs]="0" [loadingMinVisibleMs]="200" />

<!-- Keep old content during reload -->
<am-document-card-grid [preserveItemsWhileLoading]="true" />
```

### Filtering Documents
The `DocumentsFacadeService` now supports basic filters:
- `Search` (debounced 300ms)
- `Type`
- `Accepted`

Usage in a component (see `dashboard.component.ts`):
```ts
onSearchInput(val: string) { this.facade.setSearchTerm(val.trim()); }
```
Additional filters like date range or price can be wired similarly by adding signals and setting the appropriate params in the `load` method.

#### Optional: Taiga UI Pagination
`document-card-grid` kann statt eigener Buttons die Taiga Komponente nutzen.
Aktuell eingebaut: `<tui-pagination [length]="pageCount || 1" [index]="page - 1" (indexChange)="onIndexChange($event)" />`

Vorteile: A11y, konsistentes Styling, weniger eigener Code. PageSize-Select kann bei Bedarf später via `tui-select` ergänzt werden.

### Theming (Dark/Light)
`document-card-grid` nutzt CSS Custom Properties (z.B. `--am-bg-surface`, `--am-text-primary`). Ein Dark Mode kann durch Setzen von `data-theme="dark"` am nächsthöheren Container (z.B. `<body>`) aktiviert werden. Die Komponente reagiert via `:host-context([data-theme='dark'])`.

Um globale Werte anzupassen, im globalen Stylesheet z.B.:
```scss
body[data-theme='dark'] {
  --tui-base-01: #181818;
  --tui-base-02: #1f1f1f;
  --tui-base-03: #2a2a2a;
  --tui-base-04: #3a3a3a;
  --tui-text-01: #f5f5f5;
  --tui-text-02: #b0b0b0;
  --tui-accent: #3b82f6;
}
```

### `am-document-card`
Leichtgewichtige Presentational-Komponente für ein einzelnes Dokument. Aus dem Grid extrahiert, um Wiederverwendung und zukünftige Erweiterungen (Badges, Kontextmenü, Selektion) zu erleichtern.

Inputs:
- `document: DocumentListItemDto` (required)
- `busy?: boolean` (optional Overlay-Hinweis – derzeit nicht genutzt)
- `variant: 'regular' | 'compact' | 'mini'` Layout-Dichte (default `compact`)

Outputs:
- `cardClick(DocumentListItemDto)` bei Klick / Aktivierung

Template zeigt aktuell:
- Thumbnail (`thumbnailPath`) oder Platzhalter
- Titel (Fallback auf `id`)
- Upload-Datum (kurzes Datum), Typ, Accepted-/Encrypted-Indikatoren
- Summary (falls vorhanden, ellipsed)

Verwendung:
```html
<am-document-card [document]="doc" (cardClick)="openDetail(doc)"></am-document-card>
```

Styling:
- Nutzt dieselben CSS Custom Properties wie das Grid (`--am-bg-surface`, `--am-border`, etc.)
- Dark Mode kompatibel

Erweiterungsideen:
- Selektionszustand (`selected` Input + Outline)
- Kontext-Action-Menü (3-Dot Button / Right-Click)
- Status-Badge (z.B. Verarbeitung läuft)
- Lazy-LQIP Preview / Progressive Image Loading

#### Taiga UI Integration (Surface + Badges)
Die Karte nutzt `tuiSurface` (appearance `flat`) für konsistentes Theming & Hover-Effekte sowie `tui-badge` für Zustände:

| Zustand | Darstellung |
|---------|------------|
| `accepted` | Grünes Badge `OK` |
| `encrypted` | Neutrales Badge `LOCK` |

Eigene Box-Shadow/Border wurden entfernt zugunsten der Design Tokens von Taiga. Für zusätzliche Semantik können zukünftig Icons (`@taiga-ui/icons`) ergänzt oder Badges durch Tooltips erweitert werden.

##### (Revert) Weg von `tuiCardMedium`
Nach Test der Integration von `tuiCardMedium` wurde wieder auf eine eigene leichte Card-Struktur zurück gewechselt, um mehr Kontrolle über Layout-Dichte, Spacing und zukünftige Erweiterungen (z.B. Selektion, Drag & Drop) zu behalten. Die Karte nutzt nun wieder eine eigene Wrapper-Div mit Shadow/Radius und denselben Overlay-Badges. Tokens von Taiga (Farben) werden weiterhin konsumiert.

##### Kompakte Variante mit Titel-Overlay
Die Karte wurde weiter verdichtet: Der Titel befindet sich jetzt in einer halbtransparenten Overlay-Leiste direkt am oberen Rand des Thumbnails. Accepted/Encrypted-Badges wurden in diese Leiste integriert. Vorteile:
- Spart vertikalen Platz (geringere Gesamthöhe)
- Fokus auf Vorschaubild
- Mehr Karten pro Zeile möglich bei gleicher Breite

Technische Umsetzung:
- Overlay `.thumb-head` mit linear-gradient Hintergrund + Blur
- Titel und Badges zweizeilig clampbar (max zwei Titelzeilen)
- Verkleinerte Typografie (`.7rem` Titel, `.6-.66rem` Meta)
- Entfernte separate Badge-Stack und H3 Titelblock

##### Summary im Overlay
Die Summary wird jetzt (statt unter dem Bild) als eigenes Overlay-Panel (`.thumb-summary .summary-text`) innerhalb des Thumbnails dargestellt:
- Hintergrund halbtransparenter Light/Dark Gradient (nicht rein weiß → Kontrast zu weißen Dokumentseiten)
- Regulär: 3 Zeilen Clamp, Compact: 2, Mini: ausgeblendet
- Farbmodus reagiert auf Dark Mode Tokens (`tui-text-02`)
- Künftige Option: Ein-/Ausblendung via Input oder Hover-Only

Anpassungsmöglichkeiten (zukünftig):
- Umschaltbar per Input (`variant="regular|compact|mini"`)
- Optionales Ausblenden des Summary-Blocks
- Dynamische Einblendung der Overlay-Leiste nur bei Hover (Performance/Lesbarkeit abwägen)

##### Erweiterte UI/UX (Dark Mode & Accessibility)
- Badges (OK / LOCK) jetzt im Thumbnail als Overlay (`.badge-stack`) positioniert – spart vertikalen Platz.
- Typografie: Titel größer (0.9rem), Summary mit besserer Lesbarkeit (line-height 1.3, 3 Zeilen Clamp).
- Keyboard Support: Aktivierung per Enter & Space (`(keydown.enter)` / `(keydown.space)`).
- Reduced Motion: Hover-Translation deaktiviert für `prefers-reduced-motion`.
- Dark Mode Feintuning: Hintergrundwechsel auf Hover (`base-03` → `base-04`), sekundärer Text via Token.
