# Security policy

LnShelf is a self-hosted hobby project with one maintainer. Security reports are welcome and
taken seriously.

## Reporting a vulnerability

Report it privately through GitHub: **Security → Report a vulnerability** on this repository
(private vulnerability reporting). Please do not open a public issue, pull request or discussion
for anything that could be exploited.

Include what you found, how to reproduce it, and what it lets an attacker do. Expect an answer
within a week. There is no bug bounty.

## Scope

In scope:

- The code in this repository: the API (`LnShelf/LnShelf.Api`), the Blazor client
  (`LnShelf/LnShelf.Client`), the shared contracts, the Dockerfiles and the nginx
  configuration.
- The release and deploy path: the GitHub Actions workflows in `.github/workflows/`, the images
  published to `ghcr.io/drnolife/lnshelf-api` and `ghcr.io/drnolife/lnshelf-client`, and the
  server-side scripts and configuration in `ops/`.

Out of scope:

- Vulnerabilities in third-party services LnShelf reads from (AniList, MangaUpdates, Open
  Library, the OpenID Connect provider) or runs behind (Cloudflare). Report those to the
  vendor.
- Your own deployment's configuration, unless the defaults in this repository caused it.
- Findings that need an already compromised server, GitHub account or browser.
- Denial of service by volume, and missing hardening headers without a demonstrated impact.

Only the latest release is supported; fixes ship as a new release.
