# Recovery-Key Support

## Summary
Each stored document artifact receives a unique data-encryption key (DEK). The artifact content is encrypted solely with this DEK, which is wrapped once with the configured master key (`MasterKeyBase64`) using AES-GCM and recorded in a `DocumentEncryptionKeysAdded` event. Without these event records, the DEK cannot be reconstructed, so the master key alone is insufficient for decrypting artifacts.

## Current Status
ArquivoMate2 does not currently support a universal recovery key. Only the master key is available, and DEK wraps are persisted exclusively inside the event store. Consequently, a recovery key cannot decrypt artifacts unless corresponding wrap metadata is stored alongside each DEK.

## Key Concepts
- **Wrap Metadata:** Every DEK wrap requires the encrypted DEK, the nonce, the authentication tag, and a key identifier. These values reside inside the event payload.
- **Multiple Wraps:** Supporting an additional recovery key means storing multiple wrap entries per artifact so that either key can decrypt the DEK.
- **Operational Discipline:** Recovery keys must be protected and rotated with the same care as the master key, and disaster-recovery rehearsals are mandatory.

## Implementation Requirements
1. **Configuration:** Extend `EncryptionSettings` with an optional `RecoveryKeyBase64` value.
2. **Write Path:** When persisting an artifact, wrap the DEK with both the primary master key and the recovery key, generating separate nonces and tags.
3. **Data Model:** Update `EncryptedArtifactKey` (and the event payload) to hold multiple entries such as `(KeyId, WrappedDek, Nonce, Tag)`.
4. **Read Path:** Attempt decryption with the primary wrap first; if it fails or the master key is unavailable, select the recovery-key entry by its key identifier.
5. **Operations:** Define secure storage, rotation, and validation procedures so the recovery key is available and trustworthy during incidents.

## Operational Guidance for Current Installations
Until the multi-wrap design is implemented, installations must safeguard the existing metadata:
1. **Back Up the Event Store:** Regularly export all `DocumentEncryptionKeysAdded` events, including wrap fields. Neither the master key nor a future recovery key can operate without these records.
2. **Protect Secrets:** Store the master key (and any recovery key once supported) separately from backups in an HSM or secret-management system.
3. **Maintain Off-site Copies:** Keep at least one encrypted backup outside the production environment to cover disaster scenarios such as fire or ransomware.
4. **Perform Recovery Drills:** Test decrypting artifacts using the backup plus the master key so missing events or corrupt backups are detected early.

## References
- `src/ArquivoMate2.Domain/Documents/Events/DocumentEncryptionKeysAdded.cs`
- `src/ArquivoMate2.Infrastructure/Encryption`
