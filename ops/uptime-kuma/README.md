# Uptime Kuma

Uptime Kuma watches LnShelf and alerts to Discord. It replaces Netdata. It runs as its own
compose project in `~/docker/uptime-kuma`, on the LAN only.

**It is never added to the Cloudflare tunnel.** Its dashboard holds the push tokens and the
Discord webhook, and nothing in it needs to be reachable from the internet.

**Its limit:** Kuma runs on the same host it watches, so a dead host, or a dead Docker, goes
unnoticed. An external watchdog is parked in `docs/backlog.md`.

## Install (owner, once)

```bash
mkdir -p ~/docker/uptime-kuma && cd ~/docker/uptime-kuma
curl -fsSL --proto '=https' -o compose.yml \
    "https://raw.githubusercontent.com/DrNoLife/Book-Shelf/<sha>/ops/uptime-kuma/compose.yml"
(umask 077 && echo 'KUMA_BIND_ADDRESS=<the server LAN address>' > .env)
docker compose up -d
```

Open `http://<the server LAN address>:3001` and create the admin account with a long, unique
password. The port is bound to the LAN address only. Docker's published ports bypass ufw, so
that bind is what keeps it off the other interfaces.

## Discord alerts

1. In Discord, open **Server settings → Integrations → Webhooks → New webhook**. Choose the
   channel and copy the webhook URL.
2. In Kuma, open **Settings → Notifications → Setup notification**. Pick **Discord** and paste
   the URL. Tick **Default enabled** and **Apply on all existing monitors**, then press **Test**.

The webhook URL is a secret: anyone holding it can post to the channel. It lives only in Kuma.

## Monitors

| Monitor | Type | Settings |
|---|---|---|
| LnShelf health | HTTP(s) - Keyword | URL `https://<app hostname>/api/health`, keyword `"database":"ok"`, interval 60 s, 2 retries |
| LnShelf nightly sync | Push | Heartbeat interval 90000 s (25 h), no retries |
| LnShelf backup | Push | Heartbeat interval 90000 s (25 h), no retries |

- **Health** goes through Cloudflare, so it covers the whole path: tunnel, nginx, the API and
  Postgres. A `database: unreachable` answer is a 503 without the keyword, so it alerts too.
- **The two push monitors** alert when a night passes without a ping. The sync runs at 04:00 and
  the backup at 03:00. The 25 hours leave an hour of slack over one day.

### Where the push URLs go

Kuma shows each push monitor's URL as
`http://<LAN address>:3001/api/push/<token>?status=up&msg=OK&ping=`. Copy it **without** the
query string: the sender adds its own status and message. Both lines go in
`~/docker/lnshelf/.env`, which holds the other secrets:

```bash
HEARTBEAT_NIGHTLY_SYNC_URL=http://<LAN address>:3001/api/push/<sync token>
BACKUP_HEARTBEAT_URL=http://<LAN address>:3001/api/push/<backup token>
```

- **`HEARTBEAT_NIGHTLY_SYNC_URL`** reaches the API as `Heartbeat__NightlySyncUrl`, through the
  production compose file. The API pings it after each nightly sync: up with the counts, or down
  if the run failed. The URL is read when the container is created, so it takes effect at the
  next deploy. To apply it now, re-deploy the live version:
  `gh workflow run release.yml --ref v<live version>`.
- **`BACKUP_HEARTBEAT_URL`** is read by `ops/backup/lnshelf-backup.sh` on every run. It pings
  only after a complete backup.

The token in a push URL is a secret, so the URLs stay in `.env` and never go in the repository.
Kuma receives counts only, never titles or user data.

## Retiring Netdata

Send one test alert first: stop the API for two minutes, or press **Test** on the Discord
notification, and check that it arrives in Discord. Only then, and only after you confirm,
remove Netdata.

## Updating

Dependabot raises the pinned tag and digest in `compose.yml`. On the server, fetch the new file
from the reviewed commit, then run `docker compose pull && docker compose up -d`. Monitors and
settings live in the `uptime-kuma-data` volume and survive the update.
