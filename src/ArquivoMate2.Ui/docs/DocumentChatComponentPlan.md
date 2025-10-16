# Plan zur Umsetzung einer Document-Chat-Komponente im Angular-Client

## Ausgangslage und Anforderungen
- Der Backend-Workflow für Dokumentfragen ist in `DocumentChatbotQuestionFlow.md` beschrieben und liefert Antworten inklusive Zitaten sowie optionaler Dokument-Historie.【F:docs/DocumentChatbotQuestionFlow.md†L1-L62】
- Das Architekturleitbild in `DocumentChatBotPlan.md` verlangt einen neuen Endpoint `/api/documents/{id}/chat`, passende DTOs und eine Angular-Komponente, die Fragen und Antworten visualisiert, einschließlich Fehler- und Ladezuständen.【F:docs/DocumentChatBotPlan.md†L9-L78】
- Die UI soll vorerst nur vorbereitet werden; eine vollständige Integration (Routing, Feature-Toggle) erfolgt später.

## Zielbild im Frontend
1. **Eigenständige Chat-Komponente** (`document-chat`):
   - Bietet Eingabefeld, Antwortbereich und optionalen Verlauf (lokal im State gehalten).
   - Visualisiert Quellenhinweise/Zitate aus den vom Service aufbereiteten `DocumentChatAnswerCitation`-Objekten (Snippet + Quelle) und listet empfohlene Dokumente (`DocumentChatAnswerReference`).
   - Unterstützt Live-Streaming, wenn das Backend SSE anbietet; fällt andernfalls auf einfache Antworten zurück.
2. **Service-Schicht**: Ergänzung der vorhandenen API-Clients um Methoden für die neuen Chat-Endpunkte `POST /api/documents/{id}/chat` und `POST /api/documents/chat`, inklusive DTO-Typen.
3. **Test- und Styling-Konzept**: Komponenten-Tests mit Angular Testing Library/Jest, Styling mit vorhandenen SCSS-Konventionen und Dark-Mode-Kompatibilität.

## Architektur- und Implementierungsplan
### 1. Datenmodelle
- Die Service-Schicht kapselt die vom Backend gelieferten `ApiResponse<DocumentAnswerDto>`-Payloads in lokale Typen `DocumentChatAnswer`, `DocumentChatAnswerCitation` und `DocumentChatAnswerReference`, sodass die UI keine generierten DTOs anfassen muss.
- `DocumentQuestionRequestDto` enthält neben der Frage das Flag `includeHistory`, das serverseitig bestimmt, ob die letzten Domänenereignisse in den Prompt eingebettet werden.【F:src/ArquivoMate2.Shared/Models/DocumentQuestionRequestDto.cs†L1-L12】 Stelle sicher, dass der Service-Aufruf dieses Flag aus der UI übergibt.
- Lokaler UI-State: Interface `DocumentChatMessage` mit Typen `"question" | "answer" | "system"`, optionalen Zitaten (`DocumentChatAnswerCitation[]`) und Listen empfohlener Dokumente (`DocumentChatAnswerReference[]`) sowie Zeitstempeln.
- Für eine optionale Historienvorschau kann die Komponente `DocumentEventDto`-Einträge aus dem bereits vorhandenen Dokumentdetail (`DocumentDto.history`) übernehmen; damit lassen sich die letzten Änderungen anzeigen, bevor die Frage abgeschickt wird.【F:src/ArquivoMate2.Shared/Models/DocumentDto.cs†L1-L56】【F:src/ArquivoMate2.Ui/src/app/components/document-history/document-history.component.ts†L1-L200】

### 2. Service-Layer
- Neue Klasse `DocumentChatService` in `src/app/services`:
  - Methoden `askQuestion(documentId: string, request: DocumentQuestionRequestDto)` (Promise/Observable) für dokumentspezifische Chats sowie `askCatalogQuestion(request: DocumentQuestionRequestDto)` für katalogweite Fragen.【F:src/ArquivoMate2.API/Controllers/DocumentsController.cs†L242-L286】
  - Beide Methoden akzeptieren ein UI-Optionsobjekt (z. B. `{ includeHistory: boolean }`) und setzen dieses in `DocumentQuestionRequestDto.includeHistory`, damit der Backend-Handler die letzten zehn Events lädt und in den Prompt aufnimmt.【F:docs/DocumentChatbotQuestionFlow.md†L3-L41】
  - Optional `streamQuestion(...)` für SSE, sofern die API später Streaming anbietet.
  - Nutzt bestehenden `ApiModule`/`BaseService` aus `src/app/client` zur HTTP-Kommunikation und verarbeitet `ApiResponse<DocumentAnswerDto>` Payloads, die direkt im `DocumentChatService` auf die lokalen `DocumentChatAnswer*`-Typen gemappt werden.
  - Kapselt Fehler- und Retry-Logik (z. B. `HttpErrorResponse` auf UI-geeignete Fehlerobjekte mappen).
- Optionaler Wrapper für Streaming: Utility in `src/app/utils` mit `EventSource`-Abstraktion, der Observables aus SSE erzeugt (z. B. `createEventSourceObservable(url, options)`).

### 3. UI-Komponente
- Verzeichnis `src/app/components/document-chat` mit folgenden Dateien:
  - `document-chat.component.ts|html|scss|spec.ts`.
  - Optionale Unterkomponente `document-chat-message` für einzelne Chat-Blasen.
- Funktionen der Hauptkomponente:
  1. Input-Properties zur Übergabe von Dokument-Metadaten (`@Input() document?: DocumentView`) sowie optional vorbereiteten Historien (`@Input() history: DocumentEventDto[] = []`). Ein UI-Flag (`@Input() historyDefault = false`) steuert, ob Fragen standardmäßig mit Verlauf gestellt werden.
  2. Formularsteuerung (`FormGroup`) für Frageneingabe, Validierung auf Nicht-Leerheit. Das Eingabefeld orientiert sich an den im Projekt etablierten Taiga UI-Formularbausteinen (`<tui-input>`, `<tui-textarea>`), sodass Styling und Interaktionen konsistent bleiben; Ladeindikatoren können über die eingebauten Prefix/Status-Slots ergänzt werden.
  3. Toggle/Checkbox „Verlauf einbeziehen“, das unmittelbar das `includeHistory`-Flag im Request spiegelt. Nutze dafür die in der App etablierten Taiga UI-Controls (`<tui-toggle>` für einen Switch oder `<tui-checkbox-labeled>` für eine Checkbox) inklusive passender `tuiHint`-Tooltips. Bei aktivierter Option zeigt die Komponente eine komprimierte Vorschau (z. B. `<app-document-history>`) mit den übergebenen `DocumentEventDto`-Einträgen an, damit Nutzer sehen, welcher Kontext in die Frage einfließt.【F:docs/DocumentChatbotQuestionFlow.md†L31-L46】【F:src/ArquivoMate2.Ui/src/app/components/document-history/document-history.component.ts†L1-L200】
  4. `messages`-State (Signal oder `BehaviorSubject`) mit Append-Logik für Frage/Antwort.
  5. Ladeindikator (`isLoading`), Fehleranzeige (`errorMessage`).
  6. Verarbeitung von Antwortobjekten: Darstellen von Text, Zitaten (`Citations` inkl. Quelle/Snippet), optionalen Dokumentempfehlungen (`Documents` inkl. Score) und Anzeige des verwendeten Modells (`Model`).
  7. Möglichkeit zum Abbrechen laufender Streams (Abbruch-Button, Abbruch über `AbortController`).

### 4. Styling & UX
- Nutzung bestehender Design Tokens (siehe `src/styles/_variables.scss` und Komponenten-Styles) sowie der vorhandenen Taiga UI Theme-Konfiguration (`TuiRootModule`, globale Farbpaletten), damit die Komponente nahtlos mit restlichen Screens harmoniert.
- Layout: flexibler Container mit Chat-Verlauf (scrollbar), Eingabebereich am unteren Rand.
- Responsives Verhalten (mobile vs. Desktop): Breakpoints gemäß globalen SCSS.
- Accessibility: ARIA-Rollen (`role="log"` für Verlauf, `aria-live="polite"` für Antworten), Tastatursteuerung.
- Statuskommunikation: Lade-Spinner (z. B. bestehende Komponente) und Fehlermeldungen über Toast-Service als Ergänzung. Buttons (Absenden/Abbrechen) nutzen `<button tuiButton>` mit den bereits etablierten Varianten (`appearance="primary"`, `appearance="flat"`); Icons stammen aus `@taiga-ui/icons` bzw. `@taiga-ui/kit`.

### 5. Tests & Qualitätssicherung
- Unit-Tests für Service (Mock von `HttpClient`, Prüfung von Request-Payloads, Fehlerpfaden).
- Komponententests: Rendern von Fragen/Antworten, Validierungsregeln, Anzeige von Zitaten.
- Optionale Cypress/E2E-Vorbereitung für spätere Integration.
- Dokumentation im Storybook-ähnlichen Setup (falls vorhanden) oder Markdown mit Screenshots (später).

### 6. Erweiterbarkeit & Integration
- Vorbereiten von Hook-Punkten für:
  - Historie (`includeHistory`-Flag) inkl. optionaler Übergabe von `DocumentEventDto[]` aus der bestehenden Dokumentdetailseite, um das Flag vorzubelegen und die Vorschau zu befüllen,
  - Dokumentübergreifende Fragen (Endpoint ohne `documentId`).
- Komponente exportieren, aber noch nicht in Routen/Layouts eingebunden.
- Feature-Flagging (z. B. über `Environment`-Konfiguration) einplanen.
- Konfigurierbare Limits (max. Tokens, Timeout) in Service über `Environment` beziehen.

### 7. Offene Fragen & Nächste Schritte
- Abstimmung des finalen DTO-Schemas mit Backend-Team.
- Entscheidung über Streaming vs. Polling, abhängig vom späteren API-Design.
- Klärung, ob globales State-Management (NgRx) eingebunden wird oder lokaler State genügt.

## Umsetzungsreihenfolge
1. DTO-Interfaces & Service-Skelett erstellen.
2. Chat-Komponentenstruktur und Basis-Styles anlegen.
3. Integration von Formular- und State-Logik ohne Backend-Anbindung (Mock-Daten für Story/Tests).
4. Testabdeckung aufbauen.
5. Nach Backend-Freigabe API-Integration vervollständigen und Komponente in App-Routing einhängen.
