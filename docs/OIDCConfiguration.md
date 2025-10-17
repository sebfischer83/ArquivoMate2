# OIDC Configuration Guide

## Summary
ArquivoMate2 authenticates users through OpenID Connect (OIDC) and combines bearer-token APIs with hardened session cookies. This guide documents the required configuration and runtime behavior.

## Current Status
OIDC authentication with smart bearer/cookie selection is in production. SignalR access uses query-string tokens only for the documents hub; all other endpoints require headers or cookies.

## Key Components
- **Smart Authentication Policy:** Inspects each request. If an `Authorization` header exists, JWT bearer validation runs; otherwise the cookie handler is used.
- **Cookie Hardening:** Cookies are `HttpOnly`, `Secure`, `SameSite=None`, expire after eight hours, and slide on activity to support downloads across subdomains.
- **JWT Validation:** Uses the issuer and audience from configuration without altering incoming claim names (e.g., `name`, `roles`).

## Configuration
Set `Auth:Type` to `OIDC` and provide the following arguments via `appsettings.json`, environment variables (prefixed `AMate__`), or another provider:

| Key | Description |
| --- | --- |
| `Auth:Args:Authority` | Base authority URL for the identity provider (include realm/tenant). |
| `Auth:Args:Issuer` | Expected issuer claim for validation. |
| `Auth:Args:Audience` | API audience/resource identifier accepted for incoming tokens. |
| `Auth:Args:ClientId` | SPA client identifier (stored for reference). |
| `Auth:Args:CookieDomain` | Optional shared cookie domain (e.g., `.example.com`); leave empty to scope cookies to the API host. |

The configuration factory fails fast when `Auth:Type` or required arguments are missing.

### Environment Variables
Deployments can supply the configuration using:

```
AMate__Auth__Type=OIDC
AMate__Auth__Args__Authority=https://auth.example.com/realms/arquivomate
AMate__Auth__Args__Issuer=https://auth.example.com/realms/arquivomate
AMate__Auth__Args__Audience=arquivomate-api
AMate__Auth__Args__ClientId=arquivomate-spa
AMate__Auth__Args__CookieDomain=.example.com
```

Variables follow the `AMate__` prefix convention and mirror the structure of `appsettings.json`.

## Process Flow
1. The SPA authenticates with the OIDC provider via the authorization-code flow and obtains an access token scoped to the API audience.
2. The SPA calls `POST /api/users/login` with the access token in the `Authorization: Bearer` header. The endpoint issues an eight-hour session cookie containing the same claims.
3. Subsequent API calls may send the bearer token, while browser downloads (e.g., `GET /api/delivery/{id}`) rely on the session cookie.

## Operational Guidance
- **Cross-Subdomain Hosting:** Configure `Auth:Args:CookieDomain` with the shared parent domain when the UI and API reside on different subdomains. Leave it empty for isolated hosts to avoid sharing cookies across top-level domains.
- **SignalR:** Only the documents hub accepts access tokens via query string; all other hubs require headers or cookies.
- **Token Lifetimes:** Adjust cookie expiration and sliding settings in `AuthOptionsBuilder` if shorter sessions are needed.

## Example `appsettings.json`

```
"Auth": {
  "Type": "OIDC",
  "Args": {
    "Authority": "https://auth.example.com/realms/arquivomate",
    "Issuer": "https://auth.example.com/realms/arquivomate",
    "Audience": "arquivomate-api",
    "ClientId": "arquivomate-spa",
    "CookieDomain": ".example.com"
  }
}
```

Ensure the SPA uses the same authority and client identifier, and register the API with the configured audience.

## References
- `src/ArquivoMate2.API/Authentication/AuthExtensions.cs`
- `src/ArquivoMate2.API/Controllers/UsersController.cs`
