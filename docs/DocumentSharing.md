# Document Sharing Design Proposal

## Ausgangssituation
Aktuell werden Dokumente strikt über die `UserId` des Besitzers geladen. Dadurch können nur
jene Benutzer auf ein Dokument zugreifen, die es selbst erstellt haben. Für ein kollaboratives
Szenario sollen Dokumente an einzelne Benutzer oder eine ganze Gruppe freigegeben werden
können. Zusätzlich wünschen wir eine "Auto-Share"-Option, mit der alle bestehenden und
zukünftigen Dokumente eines Benutzers automatisch mit bestimmten Benutzern oder Gruppen
geteilt werden.

## Ziele
- **Feingranulare Zugriffskontrolle**: Ein Dokument kann explizit für einzelne Benutzer oder für
  Gruppen freigeschaltet werden.
- **Automatisiertes Teilen**: Benutzer können Regeln definieren, nach denen neue Dokumente
  automatisch geteilt werden.
- **Auditierbarkeit**: Jede Freigabe oder Änderung soll sich nachvollziehen lassen.
- **Kompatibilität**: Bestehende Endpunkte, die auf `UserId` filtern, sollen weiterhin
  funktionieren, ohne dass Dokumente doppelt gespeichert werden müssen.

## Domänenerweiterungen
### Dokumentfreigaben
Wir führen einen neuen Aggregattyp `DocumentShare` ein, der pro Kombination aus Dokument und
Freigabeziel (Benutzer oder Gruppe) gespeichert wird.

```csharp
public record DocumentShare(
    Guid DocumentId,
    string OwnerUserId,
    ShareTarget Target,
    DateTime SharedAt,
    string? GrantedBy);

public record ShareTarget
{
    public ShareTargetType Type { get; init; }
    public string Identifier { get; init; } = string.Empty; // UserId oder GroupId
}

public enum ShareTargetType
{
    User,
    Group
}
```

- `DocumentId`: Referenz auf das Dokument.
- `OwnerUserId`: Der ursprüngliche Besitzer, dient zur schnellen Authorisierung.
- `Target`: Ziel der Freigabe (`UserId` oder `GroupId`).
- `GrantedBy`: Optionaler Benutzer, der die Freigabe erstellt hat (für Auditing).

Für Marten kann `DocumentShare` als eigenständiges Dokument gespeichert werden. Die Aggregation
über ein Dokument erfolgt über die `DocumentId`.

### Gruppenverwaltung
Gruppen werden als eigenständiges Aggregat `ShareGroup` modelliert.

```csharp
public class ShareGroup
{
    public string Id { get; init; } = default!; // GroupId
    public string Name { get; set; } = string.Empty;
    public string OwnerUserId { get; init; } = default!;
    public HashSet<string> MemberUserIds { get; init; } = new();
}
```

- Gruppen gehören einem Benutzer (`OwnerUserId`) oder einem Tenant.
- Mitgliedschaften werden ausschließlich innerhalb des Aggregats gepflegt.

### Automatische Freigaben
Für automatische Freigaben wird ein neues Dokument `ShareAutomationRule` eingeführt.

```csharp
public class ShareAutomationRule
{
    public string Id { get; init; } = default!; // RuleId
    public string OwnerUserId { get; init; } = default!;
    public ShareTarget Target { get; init; } = default!;
    public ShareAutomationScope Scope { get; init; } = ShareAutomationScope.AllDocuments;
}

public enum ShareAutomationScope
{
    AllDocuments,
    FutureDocumentsOnly,
    Filtered // z. B. nach Tags oder Typ
}
```

Regeln werden bei Dokument-Uploads oder -Aktualisierungen ausgewertet. Ein Domain-Event wie
`DocumentUploaded` oder `DocumentProcessed` triggert eine Handler-Pipeline, die alle aktiven
Regeln des Besitzers liest und entsprechende `DocumentShare`-Einträge erzeugt.

## Zugriffskontrolle
### Leseseiten
Beim Abfragen von Dokumenten wird das bisherige Filterkriterium `doc.UserId == currentUserId`
um zusätzliche Bedingungen erweitert:

```sql
WHERE doc.UserId = :currentUserId
   OR EXISTS (
        SELECT 1 FROM document_shares ds
        WHERE ds.document_id = doc.id
          AND (
            (ds.target_type = 'User' AND ds.target_identifier = :currentUserId)
             OR (
                ds.target_type = 'Group'
                AND ds.target_identifier IN (
                    SELECT group_id FROM share_group_members WHERE user_id = :currentUserId
                )
             )
          )
   )
```

In Marten kann dies über `Any()`-Filter auf einer `Include`-Query oder mittels projektiertem
`DocumentAccess` View umgesetzt werden.

### Schreiboperationen
- Nur Besitzer (`OwnerUserId`) oder Benutzer mit einer speziellen Berechtigung dürfen neue
  Freigaben erzeugen oder löschen.
- Änderungen an Gruppen sind auf den Besitzer der Gruppe beschränkt, optional mit Adminrolle.

## API-Anpassungen
Neue Endpunkte im `DocumentsController` bzw. separatem `DocumentSharesController`:

- `POST /api/documents/{id}/shares` – Fügt einen ShareTarget hinzu.
- `DELETE /api/documents/{id}/shares/{targetType}/{identifier}` – Entfernt eine Freigabe.
- `GET /api/documents/{id}/shares` – Listet alle Freigaben für Anzeige im UI.

Für Automatisierung:

- `GET/POST/DELETE /api/share-automation-rules` zum Verwalten der Regeln eines Benutzers.
- `GET/POST/DELETE /api/share-groups` für Gruppenmanagement.

## UI-Überlegungen
- Dokumentdetailseite: Sektion "Freigaben" mit Übersicht, Buttons zum Hinzufügen/Löschen.
- Einstellungen: Bereich "Automatisches Teilen" für Regeln und Gruppen.
- Optional: Suggestionen für häufig verwendete Ziele (z. B. basierend auf letzten Shares).

## Hintergrundjobs
Ein Hangfire-Job kann beim Anlegen einer neuen Automatisierungsregel
`ShareAutomationScope.AllDocuments` alle bestehenden Dokumente des Benutzers iterieren und die
entsprechenden `DocumentShare`-Einträge erzeugen.

## Migration & Backfill
1. Tabellen/Dokumente für `DocumentShare`, `ShareGroup`, `ShareAutomationRule` anlegen.
2. Bestehende Dokumente behalten ihren Besitzer. Für Benutzer mit aktivem Auto-Share werden im
   Rahmen eines Jobs `DocumentShare`-Einträge erzeugt.
3. API-Clients sollten nach Deployment auf die neue Zugriffskontrolle migriert werden.

## Sicherheit & Audit
- Jeder Share sollte als Domain-Event (`DocumentShared`, `DocumentShareRevoked`) festgehalten
  werden, um eine Historie aufzubauen.
- Optionale Benachrichtigungen (SignalR) informieren beteiligte Benutzer über neue Freigaben.
- Prüfen, ob Dokumente mit vertraulichen Daten gesonderte Rollen oder zweistufige Freigaben
  benötigen.

## Nächste Schritte
1. Domänenmodelle in `ArquivoMate2.Domain` ergänzen und Marten-Mappings konfigurieren.
2. Commands/Handlers für Freigaben, Gruppen und Regeln implementieren (`ArquivoMate2.Application`).
3. API-Endpunkte und UI-Komponenten erweitern (`ArquivoMate2.API`, `ArquivoMate2.Ui`).
4. Integrationstests für Freigabe- und Automatisierungsflows hinzufügen.

