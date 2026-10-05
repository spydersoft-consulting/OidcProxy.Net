# Upgrading from OidcProxy.Net 5.4.1 to Spydersoft.OidcProxy.Net

This guide covers the breaking and behavioural changes between the last upstream release of
[OidcProxy.Net](https://github.com/oidcproxydotnet/OidcProxy.Net) (5.4.1) and
`Spydersoft.OidcProxy.Net`. Work through the sections in order; the first two are required for every consumer.

- [1. Replace the packages](#1-replace-the-packages)
- [2. Endpoint changes (breaking)](#2-endpoint-changes-breaking)
- [3. Behavioural changes](#3-behavioural-changes)
- [4. Configuration and API changes](#4-configuration-and-api-changes)
- [5. Frameworks and dependencies](#5-frameworks-and-dependencies)
- [Upgrade checklist](#upgrade-checklist)

## 1. Replace the packages

The NuGet package IDs were renamed. **Namespaces did not change**, so no `using` statements need to be touched.

| 5.4.1 | Spydersoft |
|---|---|
| `OidcProxy.Net` | `Spydersoft.OidcProxy.Net` |
| `OidcProxy.Net.OpenIdConnect` | `Spydersoft.OidcProxy.Net.OpenIdConnect` |
| `OidcProxy.Net.Auth0` | `Spydersoft.OidcProxy.Net.Auth0` |
| `OidcProxy.Net.EntraId` | `Spydersoft.OidcProxy.Net.EntraId` |

```bash
dotnet remove package OidcProxy.Net.OpenIdConnect
dotnet add package Spydersoft.OidcProxy.Net.OpenIdConnect
```

Remove the old package before adding the new one: both contain the same namespaces and types, so referencing both
causes ambiguity errors.

## 2. Endpoint changes (breaking)

The authentication endpoints were renamed to match the [oauth2-proxy](https://oauth2-proxy.github.io/oauth2-proxy/)
conventions, and the **default endpoint name changed from `.auth` to `oauth2`**.

| Purpose | 5.4.1 | Spydersoft |
|---|---|---|
| Start login | `GET /.auth/login` | `GET /oauth2/sign_in` |
| Identity provider callback | `GET /.auth/login/callback` | `GET /oauth2/callback` |
| Login error page | `GET /.auth/login/callback/error` | `GET /oauth2/error` |
| Current user (id_token claims) | `GET /.auth/me` | `GET /oauth2/userinfo` |
| Sign out / revoke tokens | `GET /.auth/end-session` | `GET /oauth2/sign_out` |
| Complete login (new) | n/a | `GET /oauth2/session-complete` |
| Reverse proxy auth check (new) | n/a | `ANY /oauth2/auth` |

### What you need to do

1. **Update the redirect URI registered at your identity provider.** The callback moved from
   `https://<host>/.auth/login/callback` to `https://<host>/oauth2/callback`. If you do not update it, login fails
   with a redirect URI mismatch. Keep both registered during a rolling upgrade.
2. **Update every client of these endpoints:** SPA code (login links, `fetch('/.auth/me')`, sign-out buttons),
   health checks, Kubernetes/Ingress rules, and any reverse proxy or WAF rules that match on `/.auth`.
3. **Or keep the old prefix.** Only the *prefix* is configurable; the sub-paths (`sign_in`, `callback`, `userinfo`,
   `sign_out`) are fixed. To keep `/.auth/...` as the prefix, set the endpoint name explicitly:

   ```json
   { "OidcProxy": { "EndpointName": ".auth" } }
   ```

   or `options.EndpointName = ".auth";` in code. Note that `/.auth/login`, `/.auth/me` and `/.auth/end-session`
   still need to be renamed to `/.auth/sign_in`, `/.auth/userinfo` and `/.auth/sign_out`, and the callback is now
   `/.auth/callback`.

### New endpoints

- **`/oauth2/session-complete`** is part of login and is only reached through redirects from `/oauth2/callback`.
  Nothing needs to call it, but it must be routable (not blocked by an Ingress or WAF rule). See
  [Session fixation protection](#session-fixation-protection).
- **`/oauth2/auth`** is an NGINX `auth_request`-compatible endpoint (any HTTP method). It returns `200` for an
  authenticated session (renewing the access token if it has expired), `401` for an invalid bearer token and
  `403` when there is no usable session. It also honours `SkipJwtBearerTokens` (see below).

### Changed endpoint behaviour

- **`/oauth2/userinfo`** now renews an expired access token before answering, and returns `401` if renewal fails.
- **`/oauth2/sign_out`** with no active session now redirects to the base address instead of returning `400 Bad
  Request`. The session cookie is deleted using the configured cookie domain, secure and SameSite settings.
- The error page route is now `/oauth2/error`.

## 3. Behavioural changes

### Session fixation protection

Login now regenerates the session. `/oauth2/sign_in` clears the existing session and cookie before redirecting to
the identity provider. `/oauth2/callback` does not write tokens into the session; it stores them in a short-lived
(1 minute) pending store, discards the session cookie and redirects to `/oauth2/session-complete`, which issues a new
session and saves the tokens into it.

Implications:

- The pending store uses `IDistributedCache`. With the default in-memory cache, `/oauth2/callback` and
  `/oauth2/session-complete` must be served by the same instance (the same constraint session state already had).
  With Redis configured, any instance can serve either request.
- Tokens are single-use and expire after one minute. A replayed or stale `session-complete` URL redirects to the
  error page.
- An `IAuthenticationCallbackHandler.OnAuthenticated` implementation is now invoked from `session-complete`, not
  from the callback. Its signature is unchanged.

### Token renewal failure

When a token cannot be renewed while proxying a request, 5.4.1 answered with `401` and the body
`{ "reason": "token_renewal_failed" }`. It now **redirects the browser to `/oauth2/sign_out`**. SPAs that parsed the
`401` body to detect an expired session must handle a redirect (or an opaque redirect from `fetch`) instead.

### Anonymous access

With `AllowAnonymousAccess = false`:

- Requests that already carry a valid authenticated user (including a bearer token, see below) pass through.
- Routes matching `SkipAuthRoutes` bypass authentication and token renewal.
- Routes matching `ApiRoutes` return `401` instead of redirecting the browser to the identity provider.

### Authentication schemes and bearer tokens

The default authentication scheme is now a policy scheme (`Cookie_OR_Bearer`): requests with an
`Authorization: Bearer ...` header are authenticated by the bearer handler, everything else by the session cookie
handler. If your application relied on the cookie handler being the only scheme, check your authorization policies.

### Middleware order

`UseOidcProxy` now calls `UseRouting` and `UseRequestTimeouts`, and registers the bootstraps in this order: session,
authorization, anonymous access, authentication endpoints, reverse proxy. Previously the reverse proxy was
registered first. Any middleware you register between `UseOidcProxy` and your own endpoints should be reviewed. The
reverse proxy is now mapped through `UseEndpoints`.

### Identity provider changes

- **Scopes are no longer lower-cased.** Scope names are sent exactly as configured.
- **`offline_access`** is requested by default, as before, but can now be turned off with
  `RequestOfflineAccessScope = false` (see below). Disable it for identity providers that reject the scope.
- **PKCE:** the `code_verifier` is only sent when one was generated.
- **Issuer validation** can be relaxed with `SkipIssuerNameValidation` (see below).
- A failed `PrepareLoginAsync` now throws an `InvalidOperationException` that includes the provider's error and
  description, instead of building an invalid authorize URL.
- A discovery document without a `jwks_uri` now throws a descriptive `ApplicationException`.

## 4. Configuration and API changes

### New settings

All settings can be set in the `OidcProxy` section of `appsettings.json` or on `ProxyOptions`.

| Setting | Default | Description |
|---|---|---|
| `EndpointName` | `oauth2` (was `.auth`) | Prefix of the authentication endpoints. |
| `SkipAuthRoutes` | empty | Routes that bypass authentication and token renewal. |
| `ApiRoutes` | empty | Routes that answer `401` instead of redirecting to the identity provider. |
| `SkipJwtBearerTokens` | `false` | Accept bearer tokens with a valid signature without requiring a session cookie. |
| `CookieMaxAge` | not set | Fixed lifetime of the session cookie. When set, the cookie persists across browser restarts. |
| `CookieSameSite` | not set | `SameSite` mode of the session cookie. |
| `CookieSecure` | not set | Always issue the cookie with the `Secure` attribute. |
| `CookieDomain` | not set | Domain of the session cookie. |

`SkipAuthRoutes` and `ApiRoutes` are lists of rules, each an optional HTTP method followed by a path regex, for example `GET=/health` or
`/api/.*`. A rule can also be a comma-separated list. The path is matched as a regular expression.

Settings added to `OpenIdConnectConfig` (and therefore `Auth0` and `EntraId` configurations):

| Setting | Default | Description |
|---|---|---|
| `SkipIssuerNameValidation` | `false` | Skip issuer name validation of the discovery document and tokens. |
| `RequestOfflineAccessScope` | `true` | Add the `offline_access` scope to the authorize and refresh requests. |

### API changes for code that extends the library

- `OpenIdConnectIdentityProvider`'s constructor takes `IHttpClientFactory` instead of `HttpClient`. The same applies
  to `Auth0IdentityProvider` and `EntraIdIdentityProvider`. Classes that derive from them must pass the factory to
  the base constructor.
- `EntraIdIdentityProvider` is now a regular class with an explicit constructor rather than a primary constructor;
  the public surface is otherwise unchanged.
- `Scopes` has a second constructor parameter, `requestOfflineAccessScope` (default `true`).
- `Auth0ProxyConfig.Auth0` is now `required`.
- `ProxyOptions.RegisterIdentityProvider(..., endpointName)` defaults `endpointName` to `"oauth2"`.
- `Auth0EndSessionUrlBuilder.WithRedirectUrl` and `WithIdTokenHint` accept `null`.
- New public types: `ISkipAuthRoutes`, `IApiRoutes`, `SkipAuthRoutes` (in
  `OidcProxy.Net.ModuleInitializers.Configuration`).
- `IDistributedLockFactory` is registered as a singleton (it was transient) when Redis is configured.

### appsettings.json example

```json
{
  "OidcProxy": {
    "EndpointName": "oauth2",
    "AllowAnonymousAccess": false,
    "SkipAuthRoutes": [ "GET=/health", "/public/.*" ],
    "ApiRoutes": [ "/api/.*" ],
    "CookieSameSite": "Lax",
    "CookieSecure": true
  }
}
```

## 5. Frameworks and dependencies

- Target frameworks changed from `net8.0` to **`net8.0`, `net9.0` and `net10.0`**. No action is needed on .NET 8 or
  later. Earlier runtimes are not supported.
- Package versions are now managed centrally (Central Package Management). If you pin transitive versions of
  `Yarp.ReverseProxy`, `Duende.IdentityModel.OidcClient`, `Microsoft.Identity.Client`, `jose-jwt` or
  `System.IdentityModel.Tokens.Jwt`, check them against the versions in
  [Directory.Packages.props](../Directory.Packages.props).
- Releases are versioned with GitVersion from this repository's tags, not the 5.x line. The package license is
  unchanged (LGPL-3.0-only).

## Upgrade checklist

- [ ] Swapped the four package references to the `Spydersoft.` IDs.
- [ ] Registered `https://<host>/oauth2/callback` as a redirect URI at the identity provider (or set
      `EndpointName` to `.auth` and registered `/.auth/callback`).
- [ ] Updated SPA, health check, Ingress and WAF references to `/sign_in`, `/userinfo`, `/sign_out`.
- [ ] Made sure `/oauth2/session-complete` and `/oauth2/callback` are routable and, without Redis, served by the same
      instance.
- [ ] Updated clients that depended on the `401 token_renewal_failed` response.
- [ ] Reviewed custom `IIdentityProvider` subclasses for the `IHttpClientFactory` constructor change.
- [ ] Reviewed scopes (no lower-casing) and `RequestOfflineAccessScope` for your identity provider.
- [ ] Reviewed middleware that runs before or after `UseOidcProxy`.
