# Privacy record

The internal side of `LnShelf.Client/wwwroot/privacy.html`: the record of processing
(GDPR Art. 30), the breach routine (Art. 33/34) and what only the owner can do. When the code
changes what is stored, who sees it or how long it is kept, change this file and the page
together, and bump the date on the page.

Controller: Thor Christiansen, a private person in Denmark. Supervisory authority: Datatilsynet.

## Record of processing

| What | Where | Purpose | Basis | Kept |
|---|---|---|---|---|
| Account: issuer, subject, email, created, last sign-in | `app_user` | sign-in, keeping a shelf yours | 6(1)(b) | until the user deletes the account |
| Shelf: series, volumes, tracking and read/owned state, dates, prices, notes, preferences | `series`, `volume`, `*_user_state`, `user_preferences` | the service | 6(1)(b) | until the user deletes it or the account |
| Import: batch (file name, time), groups, rows | `import_*` | matching an export to series | 6(1)(b) | account lifetime; the file itself is never stored |
| Sync runs and discrepancies | `sync_run`, `sync_discrepancy*` | showing what a sync changed | 6(1)(b) | account lifetime |
| Uploaded covers (EXIF stripped, re-encoded) | covers volume, `override/volume/...` | showing the user's own covers | 6(1)(b) | until replaced, removed, the series or account is deleted |
| Avatar: a copy of the provider's profile picture and its source URL; or a picture the user uploaded (EXIF stripped, re-encoded) | `app_user` (`ProviderPictureUrl`, `ProviderAvatarObjectKey`, `AvatarOverrideObjectKey`), covers volume `avatar/...` | showing the user's avatar without the browser loading anything from Google | 6(1)(b) | the copy until the provider's picture changes or the account is deleted; an upload until replaced, removed or the account is deleted |
| Public link token | `shelf_share` | the link the user chose to create | 6(1)(b) | until made private, regenerated or the account is deleted |
| Session cookie, login cookies | browser | staying signed in; login protection | 6(1)(b), strictly necessary | session, at most 30 days idle |
| API console logs (user ids, titles, some emails) | Docker json-file | running, debugging, abuse | 6(1)(f) | rotated: 3 x 10 MB per service |
| nginx access log (IP, user agent, URL) | Docker json-file | security, faults, abuse | 6(1)(f) | rotated: 3 x 10 MB |
| Rate-limit partitions (IP, user id) | process memory | protecting the server and the free APIs | 6(1)(f) | about a minute |
| Backups (database dump, covers archive) | host, `backups/` (mode 700) | restoring after failure | 6(1)(f) | 14 nightly copies, plus the last 5 pre-deploy dumps (README, Backups) |

Special categories (Art. 9) are not collected on purpose; the free-text notes could hold
anything, and the page asks users to keep sensitive information out of them. No automated
decisions (Art. 22).

### Recipients and transfers

| Who | Role | Gets | Transfer |
|---|---|---|---|
| Google | independent controller (identity provider) | that the user signed in to LN Shelf; the server's own request for the profile picture (server address only, no user id) | US, EU-US Data Privacy Framework |
| Cloudflare (Tunnel, TLS) | processor | all traffic, decrypted at its edge | US, DPF and Standard Contractual Clauses in its DPA |
| AniList, MangaUpdates, Open Library, Apple Books | none (server-side requests) | titles, catalogue ids, ISBNs, search text; never a user id or email | not personal data |

Hosting (API, Postgres, the covers volume, backups) is the owner's server in Denmark.

## Rights, and where each is served

Access and portability: `GET /api/auth/account/export` (the profile's "Export my data"; the
avatar appears as its source URL and object keys), plus email for logs, uploaded cover images and
the avatar image. Erasure: `DELETE /api/auth/account`, immediate, ends every session. Rectification:
the user edits their own shelf; the email follows the Google account. Restriction and objection:
by email to the controller. Answer within one month.

## Breach routine

1. Contain it: rotate the affected secret (Google client secret, database password,
   tunnel token), take the stack down if the database or the session keys are exposed.
2. Write down what happened, when it was found, what data and whose, and what was done.
3. If it is likely to put people at risk, tell Datatilsynet within 72 hours of finding out
   (Art. 33); if the risk is high, tell the affected users too (Art. 34). The app sends no email,
   so users are told on the privacy page and in the changelog.
4. Keep the note, whether or not anyone was told.

## Owner actions outside the code

- Accept Cloudflare's data processing agreement and keep a copy.
- Point the Google OAuth consent screen's privacy policy link at `/privacy.html` on the public host.
- Keep Cloudflare Web Analytics, Zaraz and Rocket Loader off. Bot Fight Mode sets a `__cf_bm`
  cookie that the page would then have to name.
- Check that the server's `docker-compose.override.yml` and `daemon.json` do not override the
  log limits in `docker-compose.production.yml`.
- The backup policy is in place since 2026-10-10 (README, Backups). Find and delete the ad-hoc
  backups made on 2026-10-09/10 once they are outside the 14-day window.
- Run `cover-orphan-sweep` once after deploying, to remove uploads left by earlier series deletes.
- Check Datatilsynet's address on the page before relying on it.

## Known and accepted

- Accounts are kept until the user deletes them; there is no rule for dormant accounts.
- Import and sync history is kept for the account's lifetime.
- Port 8091 serves plain HTTP on the home network; the session cookie is then not marked Secure.
  Do not port-forward it.
- Log retention is by size, not by time, which is why the page promises rotation by size and
  no number of days.
- The export holds no image files: uploaded covers and the avatar are listed by key only, and a
  request for the images themselves, or for log entries, is answered by email.
- Sign-in asks for the `profile` scope to get the picture, so Google also sends the account's
  name. It is read nowhere and stored nowhere; only the picture claim is used.
- `cover-orphan-sweep` could delete an avatar written in the few milliseconds before its user
  row is saved. Accepted: the sweep is run by hand, and the copy is fetched again at the next
  sign-in (an upload is one click to repeat).
