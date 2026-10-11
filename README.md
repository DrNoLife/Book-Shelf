# LN Shelf

A self-hosted tracker for a light novel and manga collection. It is built around series
and their volumes, and it tracks two things per volume, independently: whether you have
read it and whether you own it. From those it works out what to buy next, what is waiting
on the shelf unread, and which series you have fallen behind on.

Metadata comes from public sources (AniList, MangaUpdates, Open Library and the Apple
Books catalogue) and is refreshed every night. Your own reading and ownership state lives
in a local Postgres database, and sync never touches it.

It is a hobby project, run on a home server. Anyone can sign in with Google (or another
OpenID Connect provider) and gets a shelf of their own; nobody can see anyone else's.

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
- **Covers** are cached on the server's own disk, so the shelf renders without the source CDNs. Any cover
  can be replaced by dropping an image on it, and your own cover always wins over synced
  art.
- **Import** from a StoryGraph or Goodreads CSV export, with a review queue for the
  series it could not match with confidence.
- **Upcoming releases**, a **stats** page, and a changelog in the app.

## Stack

.NET 10 and C# 14 throughout: an ASP.NET Core API with EF Core on Postgres, and a Blazor
WebAssembly client. In production nginx serves the client bundle and proxies `/api/*` to
the API, so the browser sees one origin. Covers are files in a Docker volume, behind a small
`ICoverStore` seam.

```
browser ──► nginx (Blazor WASM bundle, /api/* proxy) ──► API ──► Postgres
                                                          ├──► covers volume (files)
                                                          └──► AniList, MangaUpdates,
                                                               Open Library, Apple Books
```

The full design is in [`docs/feature-spec.md`](docs/feature-spec.md), and the visual
design in [`docs/design.md`](docs/design.md).

## Running it locally

Prerequisites: the .NET 10 SDK and Docker.

```bash
docker compose up -d                                   # Postgres on :5433
dotnet tool restore
dotnet run --project LnShelf/LnShelf.Api               # API, applies migrations on start
dotnet run --project LnShelf/LnShelf.Client            # WASM dev server on :5081
```

In development you are signed in automatically (`Auth:DevelopmentBypass` in
`appsettings.Development.json`, refused outside Development). To get something to look at
straight away, start the API with
`Shelf__SeedDevelopmentFixture=true`: it then seeds a 24-series fixture library into an
empty database. The nightly sync is off unless `Sync__Enabled=true`. Covers go to
`LnShelf/LnShelf.Api/.data/covers` (git-ignored).

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

A release is a version tag. Bump `<Version>` in `LnShelf/Directory.Build.props`, add the
changelog entry (`ChangelogCatalog.cs`; a test keeps the two equal), commit, then:

```bash
git tag v0.22.0 && git push --follow-tags
```

[`release.yml`](.github/workflows/release.yml) then:

1. **verify**: checks that the tag matches `<Version>` and that the commit is on `main`;
2. **ci**: runs the full CI, browser journeys included;
3. **scan**: builds both images and fails on a fixable critical vulnerability (Trivy);
4. **publish**: pushes `ghcr.io/drnolife/lnshelf-api` and `lnshelf-client`, attests their
   provenance, and attaches their digests to a GitHub Release as `digests.txt`;
5. **deploy**: waits for the owner's approval (the `production` environment), then asks
   the server, through the Cloudflare Tunnel, to deploy exactly that version, commit and
   digests.

On the server, the deploy gate ([`ops/deploy/lnshelf-deploy-gate`](ops/deploy/lnshelf-deploy-gate))
accepts only that one command and checks it against GitHub on its own: the commit is on
`main`, the tag points at it, the digests are the published ones. It then fetches the compose
file of that commit and runs [`lnshelf-deploy`](ops/deploy/lnshelf-deploy) detached:

1. take a pre-deploy `pg_dump`, keeping five;
2. pull by digest and start the new release;
3. wait for `/api/health` to report the new version with the database up.

If the health check fails and the release ran no migration, it goes back to the previous
release on its own. If a migration ran, it stops and names the dump instead (see
[Rolling back](#rolling-back)).

### What the deploy path trusts

The path is built so that no single stolen secret can run a command on the server:

| Stolen | What it allows |
|---|---|
| The Cloudflare service token | Reaching the deploy sshd, which still wants the key. |
| The deploy SSH key | Nothing from outside the Docker network the tunnel lives on. |
| Both | Deploying a version that was released from `main`, with its published digests: at worst a downgrade or a restart. |
| `SIXLABORS_LICENSE_KEY` | Building ImageSharp somewhere else. |
| The owner's GitHub account | Everything, which is why it has 2FA and every deploy needs its approval. |

Migrations stay backward compatible for one release (add first, remove a release later), so
going back one image is always safe.

### Setting it up (owner, once)

1. **GitHub.** The repository is public, which gives Actions, secret scanning, Dependabot
   and CodeQL for free. Rulesets protect `main` (CI required, no force push) and `v*` tags
   (only the owner creates them). The `production` environment allows `v*` tags only, needs
   the owner's approval, and holds the deploy secrets: `DEPLOY_SSH_KEY`,
   `DEPLOY_KNOWN_HOSTS`, `DEPLOY_SSH_HOST`, `CF_ACCESS_CLIENT_ID` and
   `CF_ACCESS_CLIENT_SECRET`. `SIXLABORS_LICENSE_KEY` is a repository secret and a Dependabot
   secret. The GHCR packages are public, so the server pulls without a token.
2. **The tunnel in its own project:** [`ops/cloudflared/README.md`](ops/cloudflared/README.md).
3. **The deploy sshd, key and gate:** [`ops/sshd/README.md`](ops/sshd/README.md).
4. **Monitoring:** [`ops/uptime-kuma/README.md`](ops/uptime-kuma/README.md).

0.22.0 was the first release deployed this way (2026-10-10); the old `deploy.ps1`, which built
on a laptop and shipped tarballs over SSH, is gone.

### Rolling back

To go back to an earlier release, run its tag's workflow again. Nothing is rebuilt: the deploy
job reads the digests that release published.

```bash
gh workflow run release.yml --ref v0.21.0
```

If a release ran a migration and then failed, nothing rolls back on its own, because the old
image may not understand the new schema. Restore the dump the deploy named, on the server, then
roll back as above:

```bash
cd ~/docker/lnshelf
docker exec -i lnshelf-postgres-1 pg_restore -U lnshelf -d lnshelf --clean --if-exists < backups/pre-deploy-<version>-<stamp>.dump
```

If GitHub is unreachable, the owner can run `lnshelf-deploy <version> <sha> <api digest>
<client digest>` on the server with their normal key.

### Six Labors license key

Cover handling uses ImageSharp 4, which refuses to compile without a Six Labors license
key (a free community license, applied for at sixlabors.com). The key is read from the
`SixLaborsLicenseKey` environment variable, set once as a user environment variable on
the machine that builds, and is never committed (`sixlabors.lic` is git-ignored as a
safety net). The release workflow hands it to `docker build` as a BuildKit secret, so it
does not end up in an image layer; it is the repository secret `SIXLABORS_LICENSE_KEY`, and a
Dependabot secret of the same name. A local `docker compose -f docker-compose.production.yml
build` needs the variable set too.

### Login

The API runs the OpenID Connect login itself and keeps the session in a cookie. Every
verified Google account that signs in becomes a user with its own shelf, within per-user
limits (see Configuration). Google only allows sign-in redirects to `https://`
addresses (or `http://localhost`), so the app needs a hostname with TLS in front of it.

A Cloudflare Tunnel provides that. It runs as a compose project of its own beside the stack,
so a deploy can never restart it ([`ops/cloudflared/README.md`](ops/cloudflared/README.md)):

1. Add your domain to Cloudflare.
2. In the Cloudflare dashboard, under *Zero Trust → Networks → Tunnels*, create a tunnel
   (type *Cloudflared*) and copy its token.
3. Give the tunnel a *public hostname*, for example `shelf.example.com`, with the service
   `http://lnshelf-client-1:80`.
4. Put the token in `~/docker/cloudflared/.env` as `CLOUDFLARE_TUNNEL_TOKEN` and start that
   project.

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
AUTH_CLIENT_ID=your-client-id.apps.googleusercontent.com
AUTH_CLIENT_SECRET=your-client-secret
# Optional: the address that takes over a library created before there were users.
AUTH_INITIAL_OWNER_EMAIL=you@example.com
# Optional: another OpenID Connect issuer than Google.
# AUTH_AUTHORITY=https://accounts.google.com
# Optional: Open Library asks API users for a contact address in the User-Agent.
OPENLIBRARY_CONTACT_EMAIL=
# Optional: Uptime Kuma push URLs (README, Monitoring).
# HEARTBEAT_NIGHTLY_SYNC_URL=http://<server>:3001/api/push/<token>
# BACKUP_HEARTBEAT_URL=http://<server>:3001/api/push/<token>
# Optional: Web Push for release notifications (README, Notifications). All three or none.
# WEBPUSH_PUBLIC_KEY=
# WEBPUSH_PRIVATE_KEY=
# WEBPUSH_SUBJECT=mailto:you@example.com
```

The app's Privacy page (`/privacy.html`) says what is stored and why. Each user can download a
copy of their data, and delete their account and all of it, from their profile. Use
the page's address as the privacy policy link on the Google consent screen. The record of
processing, the breach routine and the owner's checklist are in
[`docs/privacy-record.md`](docs/privacy-record.md).

Container logs are rotated by the compose file (three files of 10 MB per service). Check that
the server's `docker-compose.override.yml` and `/etc/docker/daemon.json` do not replace that
setting.

#### Backups

[`ops/backup/lnshelf-backup.sh`](ops/backup/lnshelf-backup.sh) runs from the server
account's crontab at 03:00, an hour before the nightly sync. It writes a custom-format
`pg_dump` (checked with `pg_restore --list` before it counts) and a tar of the covers volume to
`backups/nightly/`, keeps the newest 14 of each, and pings `BACKUP_HEARTBEAT_URL` from the
server's `.env` when it succeeds, so a missed night shows up in Uptime Kuma. The privacy page
promises exactly this, so keep to it or change the page. Copies off the server are not set up
yet (`docs/backlog.md`).

```bash
# once, on the server, with the scripts copied to ~/docker/lnshelf/ops/backup/
crontab -e   # 0 3 * * * /home/neko/docker/lnshelf/ops/backup/lnshelf-backup.sh >> /home/neko/docker/lnshelf/backups/backup.log 2>&1
ops/backup/restore-test.sh   # restores the newest backup into a throwaway Postgres and checks it
```

Run the restore test after any change to the backup, and now and then besides. If a backup is
ever restored, delete again every account that was deleted after it was taken.

On top of that, every deploy takes a `pg_dump` into `backups/` first, because migrations
apply on boot and cannot be undone; the last five are kept ([Rolling back](#rolling-back)).

#### Monitoring

[Uptime Kuma](ops/uptime-kuma/README.md) runs beside the stack on the server, on the LAN only.
It checks `/api/health` through the public address, and it has two push monitors that expect a
beat every 25 hours: the nightly sync (`HEARTBEAT_NIGHTLY_SYNC_URL` in the `.env`, which the API
reads as `Heartbeat__Monitors__NightlySync`) and the nightly backup (`BACKUP_HEARTBEAT_URL`). A
down beat, a missing beat or a failed health check sends a message to Discord. Beats carry
counts only. Kuma shares the host, so it cannot report the host itself going down; an outside
watchdog is in the backlog.

#### Notifications

After each nightly sync and each "Sync now", readers who turned notifications on for a device
get one push: the volumes that came out and the release dates that changed. They need Web Push
keys in the `.env`, made once:

```bash
docker compose -f docker-compose.production.yml exec api dotnet LnShelf.Api.dll vapid-keys
```

Put the three printed lines in the `.env`, set `WEBPUSH_SUBJECT` to a real address (Apple
refuses pushes without one) and redeploy. Leave all three empty to switch notifications off.
A new key pair turns notifications off on every device until each turns them on again. On an
iPhone or iPad, the app has to be added to the Home Screen first.

#### Uploaded covers left behind

Deleting a series, a cover or an account removes the uploaded cover files. Series deleted
before that was true left theirs behind. One-off cleanup, safe to re-run (add `--dry-run` to
only count):

```bash
docker compose -f docker-compose.production.yml exec api dotnet LnShelf.Api.dll cover-orphan-sweep
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
| `Auth__ClientId`, `Auth__ClientSecret` | (none) | Login: the OpenID Connect client |
| `Auth__AllowedEmails__0`, `__1`, … | empty (anyone) | Restrict sign-up to these addresses |
| `Auth__InitialOwnerEmail` | empty | Who adopts the library from before users existed |
| `Limits__MaximumSeriesPerUser`, `Limits__MaximumCoverOverridesPerUser` | `500`, `300` | Per-user storage limits |
| `Limits__ExternalRequestsPerMinute`, `Limits__SyncRunsPerHour` | `20`, `2` | Per-user rate limits on calls that reach the metadata sources |
| `Limits__AuthorWorksRequestsPerMinute` | `60` | Per-user rate limit on a series page's "More by {author}" row |
| `Heartbeat__Monitors__NightlySync` | empty (no beat) | Uptime Kuma push URL the nightly sync reports to |
| `Auth__Authority`, `Auth__SessionLifetime` | Google, `30.00:00:00` | The provider, and how long an unused session lasts |
| `Covers__FileSystem__RootPath` | `/data/covers` | The cover folder (the API image mounts its volume there) |
| `Sync__Enabled`, `Sync__Hour` | `false`, `4` | The nightly sync and its local hour |
| `WebPush__PublicKey`, `WebPush__PrivateKey`, `WebPush__Subject` | empty (off) | Release notifications by Web Push (`vapid-keys` makes the pair) |
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
ops/                        server-side scripts: backup, deploy gate, sshd, tunnel, Uptime Kuma
```

`CLAUDE.md` holds the coding conventions the project follows.

## Data sources

LN Shelf reads public data from [AniList](https://anilist.co),
[MangaUpdates](https://www.mangaupdates.com), [Open Library](https://openlibrary.org)
and the Apple Books search API. It is not affiliated with any of them. All requests are
rate limited and cached, and the nightly sync spends a fixed lookup budget.

## License

[MIT](LICENSE)
