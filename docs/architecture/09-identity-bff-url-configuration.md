# Identity & BFF URL Configuration

## Overview

The platform uses a **BFF (Backend for Frontend) as the public facade** for IdentityServer. All browser traffic authenticates through the BFF URL. Internal services (APIs) communicate with IdentityServer directly.

---

## Component Map

```
Browser
  └─► BackOfficeApp (Blazor Server)
        └─► BFF / YARP Reverse Proxy  ──────────► IdentityServer (internal)
              │
              └─► AccountsAPI (internal)

AccountsAPI   ──────────────────────────────────► IdentityServer (direct, server-to-server)
Notifications ──────────────────────────────────► IdentityServer (direct, server-to-server)
```

The BFF proxies **all** IdentityServer routes via YARP:

| Route pattern | Proxied to |
|---|---|
| `/.well-known/**` | `identity` |
| `/connect/**` | `identity` |
| `/Account/**` | `identity` |
| `/Consent/**`, `/Device/**`, `/Grants/**`, … | `identity` |

---

## Correct URL Configuration

| Component | Config key | Value | Reason |
|---|---|---|---|
| `BackOfficeApp` | `Identity:Authority` | `bffEndpoint` | Browser OIDC flows go through the BFF proxy |
| `BackOfficeApp` | `Identity:Url` | `bffEndpoint` | HttpClient for Identity calls goes through the BFF |
| `IdentityServer` | `IssuerUri` | `bffEndpoint` | Tokens must carry the BFF as the public issuer (`iss`) |
| `AccountsAPI` | `Identity:Url` | `identityEndpoint` | Server-to-server — no need to route through BFF |
| `Notifications` | `Identity:Url` | `identityEndpoint` | Server-to-server — no need to route through BFF |

> **Rule of thumb:** anything that runs in the browser uses `bffEndpoint`. Anything server-to-server uses `identityEndpoint` directly.

---

## Why IssuerUri = bffEndpoint

IdentityServer embeds the `iss` claim in every token. Clients validate `iss` against the `issuer` field in the discovery document (`/.well-known/openid-configuration`).

Since `/.well-known/**` is proxied through the BFF:

1. Browser fetches discovery from `bffEndpoint/.well-known/openid-configuration` → proxied to Identity.
2. Identity returns `"issuer": "bffEndpoint"` (because `IssuerUri = bffEndpoint`).
3. Tokens carry `iss = bffEndpoint`.
4. OIDC middleware validates: `iss == issuer_from_discovery` ✅

If `IssuerUri` were `identityEndpoint`, the `iss` in tokens would not match what the browser sees through the BFF, breaking token validation.

---

## Authentication Flow (Browser)

```
Browser
  │
  ├─1─► GET bffEndpoint/.well-known/openid-configuration
  │       └─ BFF proxies ──► Identity returns { issuer: "bffEndpoint", ... }
  │
  ├─2─► Redirect bffEndpoint/connect/authorize?...
  │       └─ BFF proxies ──► Identity renders login page
  │
  ├─3─► POST credentials ──► Identity issues auth code
  │
  ├─4─► BackOfficeApp exchanges code at bffEndpoint/connect/token
  │       └─ BFF proxies ──► Identity returns token { iss: "bffEndpoint", ... }
  │
  └─5─► Token validated: iss matches issuer from discovery ✅
```

---

## AppHost.cs Reference (Development)

`AppHost.cs` is the source of truth for URL wiring in local/Aspire environments:

```csharp
// ✅ Correct configuration
backOfficeApp.WithEnvironment("Identity__Url", bffEndpoint);
backOfficeApp.WithEnvironment("Identity__Authority", bffEndpoint);   // browser traffic → BFF
identity.WithEnvironment("IssuerUri", bffEndpoint);                  // tokens issued as BFF

// Internal services connect directly (no BFF hop needed)
accountApiService.WithEnvironment("Identity__Url", identityEndpoint);
notifications.WithEnvironment("Identity__Url", identityEndpoint);
```

---

> ✅ `AppHost.cs` correctly sets `Identity__Authority = bffEndpoint`. Configuration is consistent.
