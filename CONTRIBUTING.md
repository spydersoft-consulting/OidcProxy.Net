# Contributing to OidcProxy.Net

Thanks for your interest in contributing! This repository is
[Spydersoft Consulting](https://github.com/spydersoft-consulting)'s fork of
[OidcProxy.Net](https://github.com/oidcproxydotnet/OidcProxy.Net), maintained
and published independently under the `Spydersoft.OidcProxy.Net` package
name. See [NOTICE.md](NOTICE.md) for background on why this fork exists.

This guide covers how to get set up, make a change, and submit it for review.

## Code of Conduct

This project follows the guidelines in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
By participating, you're expected to uphold it.

## Getting started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) matching the version(s)
  in [global.json](global.json) (currently .NET 8, with the solution
  multi-targeting net8.0/9.0/10.0)
- Git

### Clone and build

```bash
git clone https://github.com/spydersoft-consulting/OidcProxy.Net.git
cd OidcProxy.Net
dotnet build OidcProxy.Net.slnx
```

### Solution layout

- `src/OidcProxy.Net` – core proxy library
- `src/OidcProxy.Net.OpenIdConnect` – generic OpenID Connect support
- `src/OidcProxy.Net.Auth0` – Auth0-specific integration
- `src/OidcProxy.Net.EntraId` – Microsoft Entra ID-specific integration
- `unittests/` – unit test projects, one per library above
- `integrationtests/` – integration tests
- `templates/` – `dotnet new` project templates
- `docs/` – documentation and demo projects

### Running tests

Unit tests run in CI via:

```bash
dotnet test --filter 'FullyQualifiedName~.UnitTests'
```

Run the full suite (unit + integration) locally with:

```bash
dotnet test
```

Please make sure tests pass locally before opening a pull request.

## Making a change

### Who branches where

Write access to this repository is restricted to its maintainer(s). This
means:

- **Maintainers** (anyone with push access) cut a branch directly in this
  repository off the latest `main`.
- **Everyone else** [forks the repository](https://github.com/spydersoft-consulting/OidcProxy.Net/fork)
  and branches off `main` in their fork. GitHub only allows pushing branches
  directly to this repo if you have write access, so this is the required
  path for external contributions — open your pull request from your fork's
  branch back to this repo's `main`.

Either way, the rest of the workflow is the same:

1. Branch off the latest `main` in this fork
   (`spydersoft-consulting/OidcProxy.Net`), not from the upstream project.
   Use a short, descriptive branch name, e.g. `fix/short-description` or
   `feature/short-description`.
2. **Make focused changes.** Keep pull requests scoped to a single fix or
   feature — this makes them easier to review and release.
3. **Add or update tests** for any behavior you change or add.
4. **Update documentation** (README, XML doc comments, demos) if your change
   affects public behavior or configuration.
5. **Follow existing code style.** Match the conventions already used in the
   surrounding file (naming, formatting, patterns) rather than introducing new
   ones.

### Versioning

Package versions are computed automatically from Git history using
[GitVersion](https://gitversion.net/) (see [GitVersion.yml](GitVersion.yml)).
You don't need to manually bump version numbers.

## Submitting a pull request

1. Push your branch (in this repo if you're a maintainer, or in your fork
   otherwise) and open a pull request against this repository's `main` branch
   (**not** `oidcproxydotnet/OidcProxy.Net`).
2. Describe what the change does and why, and link any related issues.
3. Ensure the CI workflow passes (build + unit tests).
4. A maintainer will review your PR and may request changes before merging.

### A note on upstream

This fork has diverged substantially from `oidcproxydotnet/OidcProxy.Net`
(rebranded package name, multi-targeting, additional fixes) and is not
intended as a staging ground for upstream contributions. If you're fixing a
bug that also affects the upstream project, feel free to mention it in your
PR description, but please target your PR at this fork's `main` branch.

## Reporting issues

If you find a bug or want to request a feature, please
[open an issue](https://github.com/spydersoft-consulting/OidcProxy.Net/issues)
with:

- A clear description of the problem or request
- Steps to reproduce (for bugs), including relevant configuration
- What you expected to happen vs. what actually happened

## Security issues

Please do not open public issues for security vulnerabilities. Instead,
report them privately to the maintainers so a fix can be prepared before
disclosure.
