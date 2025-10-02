# Recovery-Key-Unterstützung

## Ausgangssituation
- Für jedes Artefakt wird beim Speichern ein zufälliger Data Encryption Key (DEK) erzeugt.
- Der Artefaktinhalt wird ausschließlich mit diesem DEK verschlüsselt.
- Der DEK wird anschließend genau einmal mit dem konfigurierten Master-Key (`MasterKeyBase64`) per AES-GCM eingewickelt und als `DocumentEncryptionKeysAdded`-Event in der Datenbank gespeichert.

Ohne diese Eventdaten lässt sich der DEK nicht rekonstruieren; der Master-Key alleine genügt nicht, um Artefakte zu entschlüsseln.

### Reicht ein Recovery-Key alleine aus?
Nein. Selbst wenn ein zusätzlicher Recovery-Key konfiguriert wäre, benötigt man weiterhin die in der Datenbank gespeicherten Wrap-Informationen (eingewickelter DEK, Nonce, Tag). Ohne diese Metadaten gibt es nichts, was der Recovery-Key entschlüsseln könnte. Damit der Recovery-Key greift, muss der DEK zuvor beim Speichern auch mit diesem Schlüssel eingewickelt und mitsamt den dazugehörigen Parametern persistiert worden sein.

## Warum ein allgemeiner Recovery-Key heute nicht möglich ist
Ein universeller Recovery-Key müsste jeden eingewickelten DEK rekonstruieren können. Da aber nur der Master-Key existiert und die DEKs ausschließlich in der Datenbank abgelegt sind, fehlt die dafür notwendige zusätzliche Verpackung bzw. ein alternativer Speicherort.

## Voraussetzungen, damit ein Recovery-Key funktionieren kann
Damit ein Recovery-Key praktikabel wird, sind folgende Erweiterungen erforderlich:

1. **Konfiguration erweitern** – Ergänzung von `EncryptionSettings` um einen optionalen Recovery-Key (`RecoveryKeyBase64`).
2. **Key-Wraps verdoppeln** – Beim Speichern muss der DEK sowohl mit dem primären Master-Key als auch mit dem Recovery-Key eingewickelt werden. Beide Wraps benötigen eigene Nonces und Tags.
3. **Datenmodell anpassen** – `EncryptedArtifactKey` (bzw. die Event-Payload) muss mehrere Wrap-Einträge verwalten können, z. B. `(KeyId, WrappedDek, Nonce, Tag)`.
4. **Lesepfad erweitern** – Beim Entschlüsseln zunächst den primären Wrap verwenden; schlägt dies fehl oder ist der Master-Key verloren, muss der Recovery-Key anhand seiner Key-ID ausgewählt werden.
5. **Betriebliche Prozesse** – Sicherer Umgang mit dem Recovery-Key (Aufbewahrung, Rotation, Tests), damit er im Ernstfall verfügbar ist.

Erst wenn diese Punkte umgesetzt sind, kann ein Recovery-Key Artefakte wiederherstellen. Ohne die notwendigen Eventdaten oder zusätzlichen Wraps bleibt der Master-Key alleine wirkungslos.

## Notfall-Strategien für heutige Installationen
Bis alle oben genannten Erweiterungen implementiert sind, bleibt nur der Schutz der vorhandenen Wrap-Metadaten. Eine praktikable Notfall-Sicherung sieht deshalb so aus:

1. **Regelmäßige Backups der Event-Store-Datenbank** – Export der `DocumentEncryptionKeysAdded`-Events inklusive aller Wrap-Felder. Ohne diese Daten können weder Master- noch künftige Recovery-Keys wirken.
2. **Sichere Schlüsselverwahrung** – Den Master-Key (und spätere Recovery-Keys) getrennt von den Backups lagern, z. B. in einem Hardware-Sicherheitsmodul oder einem Secret-Management-System.
3. **Offsite-Kopie** – Mindestens eine verschlüsselte Kopie der Backups außerhalb der Produktionsumgebung lagern, um Katastrophenszenarien (Brand, Ransomware) abzudecken.
4. **Wiederherstellungsübungen** – Regelmäßig testen, ob sich aus Backup + Master-Key tatsächlich ein Artefakt entschlüsseln lässt. So fallen fehlende Events oder beschädigte Sicherungen frühzeitig auf.

Auf diese Weise existiert zwar kein universeller Recovery-Key, aber eine vollständige Notfall-Sicherung: Wer die gesicherten Events und den korrekt verwahrten Schlüssel besitzt, kann die DEKs rekonstruieren und damit die Artefakte wieder entschlüsseln.
