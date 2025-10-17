# Feature Sharing Overview

## Summary
ArquivoMate provides secure document sharing across workspaces while keeping ownership, governance, and auditability intact. This guide describes the sharing model, enforcement mechanisms, and lifecycle controls.

## Current Status
User and group sharing with permission tiers is available in production. Delivery hardening and revocation are enforced server-side; workspace governance policies automatically apply to shared artifacts.

## Key Components
- **DocumentShare aggregate:** Tracks the document ID, owner, share target (user or group), and granted permissions.
- **DocumentAccessView projection:** Consolidates direct and inherited permissions for quick lookup by the API layer.
- **Audit logging:** Emits events for share creation, updates, and revocations so administrators can review activity.

## Process Flow
1. An owner issues a share invitation scoped to the workspace.
2. The platform records the chosen permission tier (read, comment, download) and writes an audit event.
3. Recipients authenticate and redeem signed delivery links that carry expiry metadata.
4. Maintainers can revoke access at any time, which invalidates existing tokens and updates the audit log.

## Operational Guidance
- **Link Hardening:** Signed delivery links require authentication and expire automatically.
- **Revocation:** Revoking a share prevents new access tokens and immediately blocks existing ones.
- **Recipient Controls:** Email-domain allow lists and optional MFA requirements protect against credential abuse.
- **Governance:** Retention, classification labels, export restrictions, and usage analytics flow from the parent workspace without additional configuration.
- **Integrations:** Webhooks notify downstream systems when shares are created or revoked, keeping ticketing and SIEM tooling current.

## Future Improvements
- Expand permission tiers with granular edit/delete distinctions for external collaborators.
- Provide admin dashboards that surface anomalous access patterns.
- Automate periodic reviews for long-lived shares.

## References
- `src/ArquivoMate2.Domain/Sharing`
- `src/ArquivoMate2.Infrastructure/Services/Sharing`
- `src/ArquivoMate2.API/Controllers/ShareGroupsController.cs`
