# Feature Sharing Overview

This document outlines how ArquivoMate enables secure sharing of documents across workspaces while preserving control over sensitive content.

## Sharing Model

- **Scoped invitations:** Owners can invite individual accounts or groups to access a document. Each invitation is tied to the parent workspace so that retention policies, legal holds, and geographic restrictions remain intact.
- **Permission tiers:** Sharing supports read-only, comment, and download permissions. The system automatically records who granted each capability and when it was last updated.
- **Auditable actions:** All changes to sharing state—new invitations, permission edits, and revocations—are written to the audit log. Administrators can filter the log by document, user, or action type.

## Secure Delivery

- **Link hardening:** Shared links are signed with the workspace key and include an expiry timestamp. Recipients must authenticate before a signed link can be redeemed.
- **Automatic revocation:** Workspace maintainers can revoke a share at any time. The platform immediately invalidates existing links and blocks the issuance of new access tokens for revoked users.
- **Recipient verification:** The service enforces email-domain allow lists and optional MFA requirements for external recipients to mitigate credential misuse.

## Governance and Lifecycle

- **Policy inheritance:** Shared documents inherit workspace-wide governance, including retention, classification labels, and export restrictions.
- **Usage insights:** Maintainers can review access analytics, such as last-viewed timestamps and download counts, to confirm that sharing aligns with compliance expectations.
- **Lifecycle hooks:** Webhooks inform downstream systems when a share is created or revoked so that ticketing and SIEM tooling stay synchronized with document access.

Together these controls allow teams to collaborate on stored content without losing visibility or compromising the security posture of the workspace.
