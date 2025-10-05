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

## Runtime Configuration

Die Anwendung lädt zur Initialisierung nur noch die Datei `public/runtime-config.json`. Diese bündelt jetzt sowohl Backend-Basis-URL (`apiBaseUrl`) als auch OIDC/OAuth Einstellungen unter dem Schlüssel `auth`.

Beispiel:
```json
{
  "apiBaseUrl": "https://api.example.com",
  "auth": {
    "issuer": "https://auth.example.com/realms/arquivomate2/",
    "clientId": "spa-client-id",
    "scope": "openid profile email"
  }
}
```

Fallbacks:
- Fehlt ein Wert, greift der Default aus `app.config.ts` (`defaultAuthConfig`).
- `redirectUri` wird immer zur Laufzeit auf `window.location.origin + '/app'` gesetzt (kein Eintrag nötig).

Entfernt: `auth-config.json` (war früher zweite Datei). Bitte keine alte Datei mehr ausrollen – sonst unnötiger Request.

Deployment-Hinweis:
- Diese JSON kann beim Container-/Server-Start überschrieben oder templated werden (kein Rebuild notwendig).
- Achte darauf, dass sie ohne Caching (oder mit Cache-Busting Headern) ausgeliefert wird, falls Umgebungen wechseln.

## Docker Deployment

Multi-Stage Dockerfile (`Dockerfile`) erstellt Produktionsbuild und serviert ihn via Nginx (Port 8080). Unterstützt Multi-Arch (amd64 & arm64) durch Buildx.

### Dynamische Version (Env Variable)
`runtime-config.json` enthält das Feld `version` mit Platzhalter `__VERSION__`. Beim Container-Start ersetzt der Entrypoint (`docker-entrypoint.sh`) diesen durch die Umgebungsvariable `VERSION` oder nutzt den Fallback `0.0.0-dev`.

Beispiel Run mit Version:
```bash
docker run --rm -e VERSION=1.3.7 -p 8080:8080 arquivomate2-ui:dev
```

Innerhalb der App kann dieser Wert clientseitig (HTTP Fetch der runtime-config) angezeigt oder für Debug-Zwecke geloggt werden.

### Konfiguration per Environment Variablen (Base URL & Auth)
`runtime-config.json` verwendet Platzhalter, die beim Container-Start ersetzt werden:

| Platzhalter            | Env Var            | Fallback                          |
|------------------------|--------------------|------------------------------------|
| `__API_BASE_URL__`     | `API_BASE_URL`     | `http://localhost:5000`            |
| `__OIDC_ISSUER__`      | `OIDC_ISSUER`      | `https://example-issuer.local/realms/app/` |
| `__OIDC_CLIENT_ID__`   | `OIDC_CLIENT_ID`   | `spa-client`                       |
| `__OIDC_SCOPE__`       | `OIDC_SCOPE`       | `openid profile email`            |
| `__VERSION__`          | `VERSION`          | `0.0.0-dev`                        |

Der Austausch passiert im Entrypoint `docker-entrypoint.sh` via `sed` bevor Nginx startet.

Lokale Entwicklung vs. Container:
- `public/runtime-config.json` enthält reale Dev-Werte (schnelles Debugging mit `ng serve`).
- `public/runtime-config.template.json` enthält Platzhalter und wird nur im Container verwendet (EntryPoint kopiert Template → `runtime-config.json` → ersetzt Tokens).
- Vorteil: Keine Token-Verschmutzung im lokalen Flow, aber flexible Substitution im Deployment.

### docker-compose Beispiel
```yaml
services:
  ui:
    image: dein-registry/arquivomate2-ui:latest
    container_name: arquivomate2-ui
    environment:
      API_BASE_URL: "https://api.prod.example.com"
      OIDC_ISSUER: "https://auth.prod.example.com/realms/arquivomate2/"
      OIDC_CLIENT_ID: "arquivomate2-spa"
      OIDC_SCOPE: "openid profile email offline_access"
      VERSION: "1.5.0"
    ports:
      - "8080:8080"
    restart: unless-stopped
```

Für mehrere Umgebungen (dev/stage/prod) können unterschiedliche Compose Override Files genutzt werden (`docker-compose -f compose.yml -f compose.prod.yml up -d`).

Buildx aktivieren (einmalig):
```bash
docker buildx create --use --name arquivomate2-builder
```

Multi-Arch Image bauen:
```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t dein-registry/arquivomate2-ui:latest \
  --push .
```

Lokaler einfacher Build (nur Host-Arch):
```bash
docker build -t arquivomate2-ui:dev .
```

Container starten:
```bash
docker run --rm -p 8080:8080 arquivomate2-ui:dev
```

Runtime Config austauschen ohne Rebuild:
```bash
docker run --rm -p 8080:8080 \
  -v $(pwd)/public/runtime-config.json:/usr/share/nginx/html/runtime-config.json:ro \
  arquivomate2-ui:dev
```

Hinweise:
- `runtime-config.json` wird mit `no-cache` Headern ausgeliefert (siehe `nginx.conf`).
- Statischer Asset Cache: 1 Jahr immutable (hash-basierte Dateinamen vom Angular Build).
- Exponierter Port im Container: 8080 (nicht 80) für Plattform-Kompatibilität.
- Nicht benötigte Dev-Abhängigkeiten landen nicht im finalen Nginx Layer.

## Reusable Components

### `am-document-card-grid`
A standalone component that displays a paginated grid of document cards (thumbnail + meta info).

#### Neuer Listenmodus (Tabellarische Ansicht)
Die Komponente unterstützt jetzt einen alternativen Listenmodus mit spaltenorientierter Darstellung (Vorschau, Titel, Zusammenfassung, Status, Typ, Upload-Datum). Ein Umschalter (Grid-Icon / List-Icon) befindet sich rechts in der Header-Leiste.

Eigenschaften des Listenmodus:
- Responsives Ausblenden weniger wichtiger Spalten (erst Datum, dann Typ, dann Summary) bei schmalen Viewports.
- Zeilen sind fokussierbar (Keyboard Enter/Space löst denselben Click aus wie in der Grid-Ansicht).
- Status-Icons (Accepted / Encrypted) analog zur Card-Ansicht.
- Ellipsis + Tooltip (title) für lange Summary-Inhalte.

Technische Umsetzung:
- Interner State `listView` (boolean) im Component.
- Bedingtes Template via `@if (!listView) { ... } @else { ... }`.
- Zusätzliche Styles in `document-card-grid.component.scss` (`.list-view`, `.list-head`, `.list-row`).

Persistenz & Ladezustände:
- Der ausgewählte Modus (Grid oder Liste) wird jetzt automatisch in `localStorage` (`am-doc-grid-view`) gespeichert und beim nächsten Laden wiederhergestellt.
- Während des Ladens (wenn keine vorhandenen Items gezeigt werden) erscheinen für den aktiven Modus passende Skeleton-Platzhalter:
  - Grid: Karten mit Thumbnail- und Text-Lines
  - Liste: Zeilen mit grauen Platzhalter-Blöcken für Vorschaubild, Text, Icons und Metadaten
  - Skeletons sind per Debounce/Min-Visible Logik geschützt (kein Flackern)

Optional zukünftige Erweiterungen:
- Sortierbare Spalten (z.B. Klick auf Header)
- Multi-Select (Checkbox-Spalte)
- Virtuelles Scrolling bei sehr großen Datenmengen
- Persistenz zusätzlicher User-Präferenzen (z.B. PageSize)


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
-##### Status- & Meta-Infos (Icons statt Badges)
##### Status- & Meta-Infos (Taiga Icons statt Badges)
Accepted / Encrypted sowie Upload-Datum und Typ werden jetzt im `meta` Block unterhalb des Thumbnails in einer `status-line` angezeigt. Die vorherigen Badges + Unicode-Zeichen wurden durch offizielle Taiga Icons ersetzt:

| Zustand      | Icon Token    | Darstellung / Semantik            |
|--------------|---------------|-----------------------------------|
| accepted     | `@tui.check`  | Erfolg (grün, `--tui-success-fill`)|
| encrypted    | `@tui.lock`   | Neutral/Gesperrt (gedämpfter Text) |

Implementation-Auszug:
```html
<div class="status-line">
  @if (document.accepted) { <tui-icon class="icon-status accepted" icon="@tui.check" title="Accepted" aria-label="Accepted" /> }
  @if (document.encrypted) { <tui-icon class="icon-status encrypted" icon="@tui.lock" title="Verschlüsselt" aria-label="Verschlüsselt" /> }
  @if (document.uploadedAt) { <span class="uploaded">{{ document.uploadedAt | date:'short' }}</span> }
  @if (document.type) { <span class="type">{{ document.type }}</span> }
</div>
```

Vorteile:
- Konsistenz mit restlichem Design System
- Bessere Pixel-Rendering-Qualität als Unicode-Symbole
- Leichte Austauschbarkeit / Erweiterbarkeit (Tooltips, andere States)

Zukünftig denkbar: Tooltip-Hinweise, weitere Icons (z.B. Processing, Shared), konditionale Gruppierung bei wenig Platz.

 Icons und Meta-Zeile sind außerhalb des Overlays → bessere Lesbarkeit & Fokus auf Thumbnail.
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
