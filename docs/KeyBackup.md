# Key Backup Process

## Summary
ArquivoMate safeguards workspace encryption keys through hardware-backed storage, encrypted recovery kits, and disciplined backup procedures. This document explains how administrators maintain recoverability during incidents.

## Current Status
Managed HSM clusters protect master keys in production. Recovery kits and recurring validation jobs are implemented; work on an optional universal recovery key is tracked separately.

## Key Components
- **Hardware Security Module (HSM):** Generates and stores workspace master keys. Application services request short-lived data-encryption keys from the HSM rather than handling the master key directly.
- **Event Store Metadata:** Each ingestion wraps the document-specific DEK with the master key and persists the wrap record for later decryption.
- **Recovery Kit:** An encrypted bundle created during provisioning that contains the wrapped master key, integrity metadata, and checksums.

## Process Flow
1. Provisioning creates the recovery kit, encrypts it with an admin-specified passphrase, and enforces a secondary factor (hardware token or TOTP) for unlock.
2. Administrators store the kit in a dedicated secrets vault isolated from production.
3. Scheduled validation jobs attempt to decrypt the kit; failures raise alerts for credential rotation or kit reissuance.
4. Inventory tracking records which administrators acknowledge possession of the kit and reminds them periodically.

## Restoration Workflow
1. An authorised administrator starts the recovery flow and uploads the kit.
2. The system unlocks the kit with the passphrase plus secondary factor.
3. Workspace master keys rotate; stored DEKs are unwrapped with the recovered key and re-wrapped with the new key.
4. Services revoke active tokens so only re-authenticated users access the restored content.

## Operational Guidance
- Maintain separate storage locations for database backups and recovery kits.
- Export the event store regularly to preserve all DEK wrap records.
- Run restoration drills that decrypt sample documents, confirming that backups and keys remain usable.

## Future Improvements
- Introduce multi-wrap support for a universal recovery key (see `docs/EncryptionRecoveryKey.md`).
- Automate evidence collection for compliance audits (e.g., signed drill reports).

## References
- `src/ArquivoMate2.Infrastructure/Encryption`
- `docs/EncryptionRecoveryKey.md`
