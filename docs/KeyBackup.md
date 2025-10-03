# Key Backup Process

This guide describes how ArquivoMate safeguards workspace encryption keys and ensures that administrators can recover data during an incident.

## Primary Protection

- **Hardware Security Module (HSM):** Workspace master keys are generated and stored inside the managed HSM cluster. Application services request short-lived data-encryption keys from the HSM rather than handling the master key directly.
- **Event store metadata:** Whenever a document is ingested, its data-encryption key (DEK) is wrapped with the workspace master key and persisted alongside the document metadata. Without these wrap records the DEK cannot be reconstructed.

## Recovery Kit

- **Kit contents:** During workspace provisioning, the platform generates an encrypted recovery kit that contains the wrapped master key, integrity metadata, and validation checksums.
- **Access controls:** The kit is protected by an administrator-defined passphrase and requires a secondary factor (hardware token or TOTP) to unlock.
- **Distribution:** Organizations are encouraged to store the kit in a dedicated secrets vault separate from the production environment to reduce the impact of compromise.

## Ongoing Validation

- **Scheduled drills:** Automated jobs attempt to decrypt the recovery kit on a recurring schedule. Failures trigger alerts so administrators can rotate credentials or reissue the kit.
- **Inventory tracking:** The platform records which administrators have confirmed receipt of the kit and prompts them for periodic acknowledgements.

## Restoration Workflow

1. A new administrator signs in and initiates the recovery flow.
2. The recovery kit is uploaded and unlocked with the passphrase and secondary factor.
3. The system rotates the workspace master key, unwraps the stored DEKs with the recovered key, and re-wraps them with the rotated key.
4. Services invalidate existing access tokens to ensure that only re-authenticated users can access restored content.

## Backup Strategy

Until a universal recovery key is introduced, ArquivoMate relies on comprehensive backups to guarantee recoverability:

- **Database backups:** Regular exports of the event store preserve all DEK wrap records required to decrypt documents.
- **Secure storage:** Master keys and recovery kits are stored separately from database backups, ideally in geographically distinct facilities.
- **Restoration tests:** Routine exercises confirm that a combination of backups and stored keys can decrypt sample documents, providing early warning of missing or corrupted data.

Following these practices ensures that encrypted content remains recoverable even if primary administrators lose access to the workspace.
