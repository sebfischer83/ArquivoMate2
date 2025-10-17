# OIDC Configuration Guide

This document explains how to configure ArquivoMate2 to authenticate users through an OpenID Connect (OIDC) identity provider and how the backend uses bearer tokens and session cookies together.

## Authentication Architecture

The API registers a "Smart" authentication policy that inspects every request: if an `Authorization` header is present the request is handled through JWT bearer validation, otherwise the cookie authentication handler is selected. Cookie authentication is hardened with `HttpOnly`, `Secure`, `SameSite=None`, eight hour expiration, and sliding renewal so browser downloads can reuse the session safely across subdomains.

JWT bearer validation uses the issuer and API audience from the OIDC settings. Claims are mapped without the default Microsoft transformations so the original `name` and `roles` claims are kept for authorization decisions. A SignalR exception allows access tokens via query string for the documents hub only.

## Required Configuration Values

Set `Auth:Type` to `OIDC` and populate the OIDC arguments either through `appsettings.json`, environment variables (prefixed with `AMate__`), or another configuration provider. The available options are:

| Key | Description |
| --- | --- |
| `Auth:Args:Authority` | Base authority URL of the identity provider (should end with the realm/tenant path). |
| `Auth:Args:Issuer` | Expected issuer string inside tokens; required for validation. |
| `Auth:Args:Audience` | API audience (resource identifier) accepted for incoming tokens. |
| `Auth:Args:ClientId` | Client identifier used by the SPA when requesting tokens (stored for reference). |
| `Auth:Args:CookieDomain` | Optional cookie domain shared by the UI and API subdomains (for example `.example.com`). Leave empty to scope cookies to the API host. |

The configuration factory binds these values and fails fast if `Auth:Type` or the OIDC arguments are missing.

### Environment Variable Names

When deploying via containers you can provide the OIDC settings through variables such as:

```
AMate__Auth__Type=OIDC
AMate__Auth__Args__Authority=https://auth.example.com/realms/arquivomate
AMate__Auth__Args__Issuer=https://auth.example.com/realms/arquivomate
AMate__Auth__Args__Audience=arquivomate-api
AMate__Auth__Args__ClientId=arquivomate-spa
AMate__Auth__Args__CookieDomain=.example.com
```

These environment variables mirror the structure of `appsettings.json` and are automatically picked up because the application loads variables with the `AMate__` prefix.

## Login and Session Flow

1. The SPA authenticates against the OIDC provider using the authorization code flow and receives an access token scoped to the API audience.
2. Immediately after login the SPA calls `POST /api/users/login` with the access token in the `Authorization: Bearer` header. The endpoint requires bearer authentication and issues an eight-hour persistent cookie session with the same claims as the token.
3. Subsequent API calls may continue sending the bearer token, while browser-managed downloads such as `GET /api/delivery/{id}` can rely on the session cookie for authentication.

## Cross-Subdomain Support

If the UI and API are hosted on different subdomains, configure `Auth:Args:CookieDomain` with their shared parent domain (for example `.example.com`). This allows the secure, `SameSite=None` cookie to be sent to both hosts, enabling seamless document downloads from the UI domain without embedding access tokens in URLs. If the UI runs on a completely different top-level domain, leave the value empty to avoid exposing the session cookie outside the API host.

## Example `appsettings.json` Snippet

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

Adjust the URLs and identifiers to match your identity provider configuration. The SPA must use the same authority and client identifier when negotiating tokens, and the API must be registered with the configured audience.
