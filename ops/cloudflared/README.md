# Cloudflare Tunnel

The tunnel gives LnShelf its HTTPS hostname (Google only redirects logins to `https://`), and
gives the release workflow its way in to the deploy sshd. It is outbound only: nothing is opened
on the router.

It runs as its own compose project in `~/docker/cloudflared`, not inside the lnshelf project. A
deploy runs `docker compose up` on the lnshelf project. If the tunnel were part of that project,
the deploy could recreate it, cutting the SSH session the deploy arrived through.

To reach the client container, the tunnel joins the lnshelf project's network
(`lnshelf_default`) as an external network. It addresses the client as
`http://lnshelf-client-1:80`. The client runs `nginx-unprivileged` as uid 101 but still listens
on 80, which Docker allows unprivileged processes by default.

## Moving the tunnel out of the lnshelf project (owner, once)

No downtime: one tunnel can run several connectors at once, so the new one starts before the old
one stops.

1. **Create the project.** Take `compose.yml` from the reviewed commit, as for the deploy
   scripts (ops/sshd/README.md, step 2):

   ```bash
   mkdir -p ~/docker/cloudflared && cd ~/docker/cloudflared
   curl -fsSL --proto '=https' -o compose.yml \
       "https://raw.githubusercontent.com/DrNoLife/Book-Shelf/<sha>/ops/cloudflared/compose.yml"
   # Move the token over: the line from ~/docker/lnshelf/.env, not a copy of the whole file.
   (umask 077 && grep '^CLOUDFLARE_TUNNEL_TOKEN=' ~/docker/lnshelf/.env > .env)
   ```

2. **Point the app hostname at the container name.** In the Cloudflare dashboard, go to
   **Zero Trust → Networks → Tunnels →** your tunnel **→ Public hostnames**. Edit the LnShelf
   hostname to service type `HTTP`, URL `lnshelf-client-1:80`. The old connector sits on the
   same network, so it resolves the name too.

3. **Start the new connector, then stop the old one.**

   ```bash
   cd ~/docker/cloudflared && docker compose up -d
   docker compose logs --tail 20 tunnel        # expect "Registered tunnel connection" lines
   docker rm -f lnshelf-tunnel-1               # the old connector from the lnshelf project
   ```

   Then delete `COMPOSE_PROFILES=tunnel` and `CLOUDFLARE_TUNNEL_TOKEN` from
   `~/docker/lnshelf/.env`. The lnshelf compose file no longer has a `tunnel` service.

4. **Check the app** on its public hostname, and sign in once.

Keep in mind:

- The `lnshelf_default` network has to exist before this project starts. On a fresh host,
  start the lnshelf stack first.
- `docker compose down` on the lnshelf project cannot remove its network while the tunnel is
  attached. Use `stop`, or stop the tunnel first. Deploys only use `up`, so they are not
  affected.

## The deploy hostname (owner, once)

### 1. Create the service token

**Zero Trust → Access → Service auth → Service tokens → Create service token.**

- Name it `lnshelf-github-deploy` and give it a duration of one year.
- Copy the **Client ID** into the GitHub `production` environment secret `CF_ACCESS_CLIENT_ID`,
  and the **Client Secret** into `CF_ACCESS_CLIENT_SECRET`. The secret is shown only once.
- Put a reminder in your calendar a month before the token expires. An expired token fails the
  deploy job at the connection step. Renew it, then replace both secrets.

### 2. Create the Access application, before the hostname exists

**Zero Trust → Access → Applications → Add an application → Self-hosted.**

- **Application domain:** exactly `deploy-ssh.<your domain>`. No wildcard.
- **Policy:** one policy, action **Service Auth**, include **Service Token** =
  `lnshelf-github-deploy`. Nothing else: no Allow policy, no email or identity rule, no bypass.
  A browser login must never open this application.

### 3. Add the public hostname

Under the tunnel's **Public hostnames**:

- Hostname `deploy-ssh.<your domain>`.
- Service type `SSH`, URL `host.docker.internal:2222`.

The compose file maps `host.docker.internal` to the docker bridge gateway, where the deploy sshd
listens (ops/sshd/README.md). Keep the tunnel's catch-all rule at `http_status:404`. Only the
app hostname and this one may exist.

### 4. Check the audit log

**Zero Trust → Logs → Access** records every request to the application. After the first
deploy, there should be one allowed entry per SSH connection, all from the service token. Look
there first if a deploy ever runs that you did not start.

## What each stolen secret allows

| Stolen | Gives |
|---|---|
| The service token | A TCP connection to the deploy sshd. Useless without the key. |
| The deploy key | Nothing from outside the docker networks (`from=` and ufw). |
| Both | The gate's two commands only: deploy or roll back to a released commit on main with its published digests. At worst a downgrade or a restart. |
