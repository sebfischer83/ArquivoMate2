# Dokumenten-Chatbot – Architektur- und Umsetzungsleitfaden

## Zielbild
Ein Benutzer soll im Web-Client ein Dokument öffnen und anschließend freie Fragen zum Inhalt stellen können. Die Antwort wird live von einem LLM generiert, das den Dokumenttext und optional weitere Metadaten als Kontext erhält. Bestehende Komponenten für Dokumentverarbeitung und die Speicherung der Chatbot-Metadaten bleiben unverändert.

## Backend-Erweiterungen
### 1. Domänen- und Application-Layer
- **Neue Capability:** Ergänze `IChatBot` um eine Methode wie `Task<string> AnswerQuestion(DocumentContext context, string question, CancellationToken ct)` oder führe ein separates Interface `IDocumentQaChatBot` ein, damit `AnalyzeDocumentContent` unverändert bleibt. Die Implementierung soll neben der reinen Frage auch Dokumenttext, Zusammenfassung, Keywords und ggf. Historie entgegennehmen, um das Prompting zu verbessern.
- **Service-Klasse:** Implementiere einen `DocumentQuestionAnsweringService`, der
  1. das `DocumentAggregate` bzw. die Read-Model-Projektion lädt,
  2. den Kontext (Text, Summary, Keywords, Nutzer-Metadaten) vorbereitet,
  3. `IChatBot.AnswerQuestion` aufruft,
  4. das Ergebnis (Antwort + verwendete Quellen) zurückgibt.
- **Caching/Chunking:** Da Dokumente sehr groß sein können, plane ein Chunking (z. B. mit `ISearchClient` für semantische Suche über Absätze) ein, um nur relevante Textteile in den Prompt aufzunehmen.
- **Chat-Historie:** Lege einen dedizierten Event-Stream in Marten an (`DocumentChatTurnRecorded`/`CatalogChatTurnRecorded`), damit sich der Verlauf einzelner Gespräche reproduzierbar nachverfolgen lässt und dem LLM als Kontext bereitgestellt werden kann.

### 2. API-Layer
- **Controller:** Ergänze `DocumentsController` in `ArquivoMate2.API` um einen `POST /api/documents/{id}/chat`-Endpunkt. Der Request enthält `question` (Pflicht) und optionale Flags (`includeHistory`, `maxSnippets`, etc.). Der Endpoint delegiert an den `DocumentQuestionAnsweringService` und streamt Antworten (Server-Sent Events) oder liefert eine synchrone Antwort. Zusätzlich stelle einen `POST /api/documents/chat`-Endpunkt bereit, damit der Chatbot auch katalogweite Fragen ohne spezifischen Dokumentkontext beantworten und passende Dokument-IDs oder Zählwerte zurückliefern kann.
- **DTOs:** Lege Request/Response-Datenstrukturen in `ArquivoMate2.Shared` an (z. B. `DocumentQuestionRequestDto`, `DocumentAnswerDto` mit Antworttext, verwendeten Passagen, Modellname). Nutze AutoMapper-Profile, falls nötig. Erweitere die Antwort zusätzlich um optionale `documents` (Liste aus IDs, Titel, Zusammenfassung, Dateigröße) sowie `documentCount`, damit der Client Trefferlisten oder Statistiken anzeigen kann.
- **Auth & Rate Limiting:** Wiederverwende bestehende AuthZ-Mechanismen (z. B. `[Authorize]`). Füge Rate-Limiting/Metering ein, um Missbrauch zu verhindern.

### Beispiel-API-Flow
1. **Frage zu einem Dokument stellen**
   - Request
     ```http
     POST /api/documents/3f7f4d0d-9b3b-48f3-9f3c-9487b8ba3c27/chat
     Content-Type: application/json

     {
       "question": "Welche Vertragslaufzeit ist im Dokument vereinbart?",
       "includeHistory": true,
       "maxSnippets": 4
     }
     ```
   - Response (vereinfacht)
     ```json
     {
       "answer": "Der Vertrag läuft bis zum 31.12.2025 mit einer automatischen Verlängerung um 12 Monate, sofern nicht drei Monate vorher gekündigt wird.",
       "citations": [
         {
           "documentId": "3f7f4d0d-9b3b-48f3-9f3c-9487b8ba3c27",
           "chunkId": "chunk-05",
           "offsetStart": 1280,
           "offsetEnd": 1465
         }
       ],
       "history": [
         {
           "role": "user",
           "message": "Gibt es Sonderkündigungsrechte?"
         },
         {
           "role": "assistant",
           "message": "Ja, bei Zahlungsverzug kann innerhalb von 14 Tagen gekündigt werden."
         }
       ]
     }
     ```
2. **Katalogweite Frage stellen**
   - Request
     ```http
     POST /api/documents/chat
     Content-Type: application/json

     {
       "question": "Welche Dokumente beschreiben ebenfalls die Leistung 'Premium-Support'?",
       "filters": {
         "tags": ["Support"],
         "maxResults": 5
       }
     }
     ```
   - Response (vereinfacht)
     ```json
     {
       "answer": "Ich habe fünf Dokumente gefunden, die Premium-Support erwähnen.",
       "documents": [
         {
           "documentId": "2c143fa0-11be-4cf7-9f91-5d82b8f3b02f",
           "title": "Servicevertrag 2024",
           "summary": "Beinhaltet Premium-Support mit 24/7 Hotline.",
           "fileSize": 4189230
         }
       ],
       "documentCount": 5
     }
     ```

### 3. Infrastruktur
- **OpenAI-Implementierung:** Erweitere `OpenAIChatBot` so, dass die neue Methode Chat-Completion mit System-/User-Messages und JSONl oder Textantwort unterstützt. Nutze die Function-Calling-Tools (`load_document_chunk`), damit das Modell gezielt Dokumentsegmente anfordern kann, bevor es antwortet. Übergebe in der System-/User-Message nur Chunk-IDs plus Positionsbereiche – der eigentliche Text wird ausschließlich über das Tool nachgeladen. Ergänze ein zweites Tool `query_documents`, über das das Modell per Funktionsaufruf den Dokumentkatalog durchsuchen, filtern oder zählen kann. Verwende das Streaming-SDK, falls du SSE im Backend anbietest.
- **Konfiguration:** Ergänze `ChatBotSettings` (z. B. Max Tokens, Temperatur, Embedding-Modell). Aktualisiere `appsettings` und die `ChatBotSettingsFactory`, damit das neue Verhalten konfigurierbar bleibt.
- **Vektorsuche:** Speichere Chunk-Embeddings der Dokumente in einer pgvector-Datenbank (z. B. `arquivomate_vectors`) und ermögliche so semantische Kandidatenauswahl für Nachfragen. Die Connection-Strings sollen wie die übrigen Datenbanken konfigurierbar sein und der Docker-Compose-Stack legt das Schema per Init-Container an.

## Frontend-Anpassungen (Angular)
1. **Service:** Erweitere `DocumentsService` um eine Methode `askDocumentQuestion(id: string, request: DocumentQuestionRequestDto)` die den neuen Endpoint konsumiert. Bei Streaming: Nutze `EventSource` oder `RxJS`-Wrapper.
2. **UI-Komponenten:**
   - Neue `DocumentChatComponent`, die Frage-Input, Verlauf und Antworten anzeigt.
   - Wiederverwendung der bestehenden Resolver, um Dokumentdetails (Summary, Keywords) in die Komponente einzuspeisen.
   - Optional: Clientseitige Konversationsspeicherung, Anzeige der verwendeten Textstellen, Feedback-Buttons.
3. **State-Management:** Falls NgRx im Projekt genutzt wird, füge Actions/Effects für Frage-/Antwortzyklen hinzu. Alternativ verwende lokale Component State + Services.
4. **UX-Details:**
   - Ladeanimation während die Antwort generiert wird.
   - Fehlerhandling (Timeout, Modell nicht erreichbar).
   - Hinweis auf Kosten/Datenschutz.

## Sequenzdiagramm (vereinfachte Beschreibung)
1. UI sendet `POST /api/documents/{id}/chat` mit Frage.
2. Controller validiert, lädt Dokumentkontext, ruft Service.
3. Service ermittelt relevante Dokumentteile (ggf. via Suche) und ruft `IChatBot.AnswerQuestion`.
4. ChatBot-Implementierung baut Prompt, ruft OpenAI (oder anderes LLM) und gibt Antwort zurück.
5. Service verpackt Antwort, optional mit referenzierten Snippets.
6. UI zeigt Antwort im Chatverlauf und referenzierte Stellen an.

## Nicht-funktionale Anforderungen & ToDos
- **Logging/Tracing:** Protokolliere Prompts (ggf. pseudonymisiert) und Antworten für Monitoring, halte DSGVO-Konformität ein.
- **Kostenkontrolle:** Hinterlege Limits für maximale Prompt-Länge und Anzahl Tokens.
- **Testing:**
  - Unit-Tests für den Service (Prompt-Zusammenstellung, Auswahl der Textsnippets).
  - Integrationstest mit einem Mock-`IChatBot`.
  - End-to-End-Test im UI (Cypress) für Frage-Antwort-Fluss.
- **Security:** Stelle sicher, dass nur berechtigte Benutzer auf Dokumente zugreifen und Fragen stellen dürfen. Prüfe Input Sanitization.

## Umsetzungsschritte (Roadmap)
1. Architektur-Review und Festlegung des Prompting-Ansatzes (reine Retrieval vs. RAG).
2. Backend: Interface anpassen, Service implementieren, Endpoint hinzufügen.
3. Infrastruktur: OpenAI-Konfiguration erweitern, optional Embeddings/Chunking umsetzen.
4. Frontend: Service + Komponenten implementieren, UI/UX feintunen.
5. Tests, Observability, Rollout.

