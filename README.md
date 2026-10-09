# LN Shelf

A self-hosted tracker for a light novel and manga collection. It is built around series
and their volumes, and it tracks two things per volume, independently: whether you have
read it and whether you own it. From those it works out what to buy next, what is waiting
on the shelf unread, and which series you have fallen behind on.

Metadata comes from public sources (AniList, MangaUpdates, Open Library and the Apple
Books catalogue) and is refreshed every night. Your own reading and ownership state lives
in a local Postgres database, and sync never touches it.

It is a single-user hobby project, run on a home server. One person signs in, with
Google or any other OpenID Connect provider.

## Features

- **Library** of every tracked series, with what to buy and what is unread counted per
  series, and filters such as *Behind*, *Backlog* and *Up to date*.
- **Series page** with one tile per volume. Each tile shows read and owned state, and a
  click opens a menu for read status, ownership, edition and cover. A list view is
  available too.
- **Search and add** from AniList, with MangaUpdates as a fallback when AniList is
  unavailable. The light novel and the manga of one title are separate series.
- **Nightly sync**: volume counts and publication status from MangaUpdates; per-volume
  release dates, covers, subtitles and blurbs from Apple Books and Open Library (by ISBN).
  Disagreements between sources are recorded for review rather than silently resolved.
- **Covers** are cached locally, so the shelf renders without the source CDNs. Any cover
  can be replaced by dropping an image on it, and your own cover always wins over synced
  art.
- **Import** from a StoryGraph or Goodreads CSV export, with a review queue for the
  series it could not match with confidence.
- **Upcoming releases**, a **stats** page, and a changelog in the app.

## Stack

.NET 10 and C# 14 throughout: an ASP.NET Core API with EF Core on Postgres, and a Blazor
WebAssembly client. In production nginx serves the client bundle and proxies `/api/*` to
the API, so the browser sees one origin. Covers are stored in S3-compatible storage
(MinIO) behind a small `ICoverStore` seam.

```
browser ──► nginx (Blazor WASM bundle, /api/* proxy) ──► API ──► Postgres
                                                          ├──► MinIO (covers)
                                                          └──► AniList, MangaUpdates,
                                                               Open Library, Apple Books
```

The full design is in [`docs/feature-spec.md`](docs/feature-spec.md), and the visual
design in [`docs/design.md`](docs/design.md).

## Running it locally

Prerequisites: the .NET 10 SDK and Docker.

```bash
docker compose up -d                                   # Postgres on :5433, MinIO on :9000
dotnet tool restore
dotnet run --project LnShelf/LnShelf.Api               # API, applies migrations on start
dotnet run --project LnShelf/LnShelf.Client            # WASM dev server on :5081
```

In development you are signed in automatically (`Auth:DevelopmentBypass` in
`appsettings.Development.json`, refused outside Development). To get something to look at
straight away, start the API with
`Shelf__SeedDevelopmentFixture=true`: it then seeds a 24-series fixture library into an
empty database. The nightly sync is off unless `Sync__Enabled=true`.

> The pinned MinIO image (`minio/minio:RELEASE.2025-09-07T16-13-09Z`) is no longer
> published by MinIO, so `docker compose up` only works where it is already cached.
> Any S3-compatible store works behind `ICoverStore`; see `docs/backlog.md`.

## Tests

```bash
dotnet test LnShelf/LnShelf.slnx                       # unit tests, no Docker needed
```

The browser journeys in `LnShelf.EndToEnd.Tests` run the real API and the published
client in Chromium against a throwaway Postgres. External sources are replaced by fakes
that answer from captured real responses, so no test ever calls AniList or the others.
They are kept out of the run above, and need Docker running:

```bash
dotnet publish LnShelf/LnShelf.Client -c Release
dotnet build LnShelf/LnShelf.EndToEnd.Tests -c Release
pwsh LnShelf/LnShelf.EndToEnd.Tests/bin/Release/net10.0/playwright.ps1 install chromium   # once
dotnet test LnShelf/LnShelf.EndToEnd.Tests -c Release --no-build -p:RunEndToEnd=true
```

Set `LNSHELF_HEADED=true` to watch the browser. A Playwright trace of every test is
written to `bin/Release/net10.0/playwright-traces/`.

## Deploying

`deploy.ps1` builds both images locally, copies them to the server over SSH and restarts
the stack there with [`docker-compose.production.yml`](docker-compose.production.yml).

### Login

The API runs the OpenID Connect login itself and keeps the session in a cookie; only the
address in `AUTH_ALLOWED_EMAIL` gets in. Google only allows sign-in redirects to `https://`
addresses (or `http://localhost`), so the app needs a hostname with TLS in front of it.

The production compose file includes an optional Cloudflare Tunnel for that:

1. Add your domain to Cloudflare.
2. In the Cloudflare dashboard, under *Zero Trust → Networks → Tunnels*, create a tunnel
   (type *Cloudflared*) and copy its token.
3. Give the tunnel a *public hostname*, for example `shelf.example.com`, with the service
   `http://client:80`.
4. Put the token in the server's `.env` as `CLOUDFLARE_TUNNEL_TOKEN` and set
   `COMPOSE_PROFILES=tunnel`.

The tunnel only makes outbound connections, so no port is opened on your router. Any other
TLS reverse proxy forwarding to port 8091 works too.

To set up Google:

1. In the [Google Cloud Console](https://console.cloud.google.com/), create a project and
   configure the OAuth consent screen (External, in testing, with yourself as a test user).
2. Under *Credentials*, create an *OAuth client ID* of type *Web application*.
3. Add the authorised redirect URI `https://<your host>/api/auth/callback`.
4. Put the client id and secret in the server's `.env` (below).

Any other OpenID Connect provider works the same way; set `AUTH_AUTHORITY` to its issuer.

### Server configuration

On the server, put a `.env` next to the compose file. The stack refuses to start without
everything that is not marked optional:

```dotenv
POSTGRES_PASSWORD=change-me
MINIO_ROOT_PASSWORD=change-me-too
AUTH_CLIENT_ID=your-client-id.apps.googleusercontent.com
AUTH_CLIENT_SECRET=your-client-secret
AUTH_ALLOWED_EMAIL=you@example.com
# Optional: HTTPS through a Cloudflare Tunnel (see Login above).
# CLOUDFLARE_TUNNEL_TOKEN=
# COMPOSE_PROFILES=tunnel
# Optional: another OpenID Connect issuer than Google.
# AUTH_AUTHORITY=https://accounts.google.com
# Optional: Open Library asks API users for a contact address in the User-Agent.
OPENLIBRARY_CONTACT_EMAIL=
```

Then, from your machine:

```powershell
./deploy.ps1 -Server <server address> -User <ssh user> -Url https://<your host>
# or set LNSHELF_DEPLOY_SERVER, LNSHELF_DEPLOY_USER and LNSHELF_DEPLOY_URL once
```

The client is published on port 8091 for the TLS proxy to forward to. Migrations run
when the API starts, and the deploy checks `/api/health`, the one endpoint open without
signing in.

## Configuration

The API reads standard ASP.NET Core configuration, so every setting can be given as an
environment variable with `__` for the section separator (`Sync__Hour=4`). The ones you
are most likely to change:

| Setting | Default | Purpose |
|---|---|---|
| `ConnectionStrings__Db` | (none) | Postgres connection string |
| `Auth__ClientId`, `Auth__ClientSecret`, `Auth__AllowedEmail` | (none) | Login: the OpenID Connect client and the one address allowed in |
| `Auth__Authority`, `Auth__SessionLifetime` | Google, `30.00:00:00` | The provider, and how long an unused session lasts |
| `Covers__Endpoint`, `Covers__AccessKey`, `Covers__SecretKey`, `Covers__Bucket` | `localhost:9000`, empty, empty, `lnshelf-covers` | Cover storage (S3/MinIO) |
| `Sync__Enabled`, `Sync__Hour` | `false`, `4` | The nightly sync and its local hour |
| `OpenLibrary__ContactEmail` | empty | Contact address sent to Open Library |
| `Shelf__SeedDevelopmentFixture` | `false` | Seed the fixture library on an empty database |

Rate limits and timeouts for each metadata source are in
[`appsettings.json`](LnShelf/LnShelf.Api/appsettings.json). They are set to stay well
inside what these free community APIs ask for. Please keep them that way.

## Project layout

```
LnShelf/
  LnShelf.Api/              API, EF Core, metadata clients, nightly sync (feature slices)
  LnShelf.Client/           Blazor WebAssembly client
  LnShelf.Shared/           request and response contracts shared by both
  LnShelf.Api.Tests/        unit tests, mirroring the API's layout
  LnShelf.EndToEnd.Tests/   browser journeys (Playwright)
docs/                       feature spec, design, phase log, backlog
```

`CLAUDE.md` holds the coding conventions the project follows.

## Data sources

LN Shelf reads public data from [AniList](https://anilist.co),
[MangaUpdates](https://www.mangaupdates.com), [Open Library](https://openlibrary.org)
and the Apple Books search API. It is not affiliated with any of them. All requests are
rate limited and cached, and the nightly sync spends a fixed lookup budget.

## License

[MIT](LICENSE)
