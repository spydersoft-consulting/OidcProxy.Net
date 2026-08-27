# Notice

This project is a fork of [OidcProxy.Net](https://github.com/oidcproxydotnet/OidcProxy.Net) by
Albert Starreveld, licensed under the GNU Lesser General Public License v3.0 (LGPL-3.0-only).
See [license.txt](license.txt) for the full license text.

Spydersoft Consulting forked this project on 2026-08-26 because upstream activity had slowed
significantly (only 1 of the last 7 pull requests was from a human contributor; most recent
commits were dependency bumps). This fork is published and maintained separately as
`Spydersoft.OidcProxy.Net` and related packages.

## Changes made in this fork

- Renamed NuGet packages from `OidcProxy.Net*` to `Spydersoft.OidcProxy.Net*` (source namespaces
  are unchanged).
- Incorporated the following fixes/features originally developed by
  [Volodymyr Oliinyk](https://github.com/volodymyr-oliinyk) in his fork
  ([volodymyr-oliinyk/OidcProxy.Net](https://github.com/volodymyr-oliinyk/OidcProxy.Net)),
  cherry-picked with attribution preserved in commit history:
  - Session regeneration on login to prevent session fixation
  - Cookie clearing on login and sign-out
  - Token renewal fix
  - `nginx_auth_request`-compatible endpoint
  - Redirect-to-baseAddress fix on logout without a token
  - Memory leak fixes
  - Signature key resolution fix and ability to skip issuer name verification
  - `ApiRoutes` returning 401 instead of redirecting for unauthenticated API requests, plus
    `SkipJwtBearerTokens` support
  - OAuth2-Proxy compatible endpoints

As required by the LGPL-3.0, this fork remains licensed under LGPL-3.0-only, and the original
copyright notices are preserved throughout the source.
