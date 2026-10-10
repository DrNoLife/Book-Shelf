# Deploy sshd and the deploy gate

The release workflow (`.github/workflows/release.yml`) deploys over SSH through the Cloudflare
tunnel. On the server, that path ends at a dedicated sshd that can run exactly one program,
`lnshelf-deploy-gate`. The gate accepts two commands (`deploy …` and `status …`). Before it
starts `lnshelf-deploy`, it checks with GitHub that the commit is on main, that the tag points at
that commit, and that the image digests are the ones that release published.

| Piece | Installed at | Owner |
|---|---|---|
| `ops/deploy/lnshelf-deploy-gate` | `/usr/local/sbin/lnshelf-deploy-gate` | root, 0755 |
| `ops/deploy/lnshelf-deploy` | `/usr/local/sbin/lnshelf-deploy` | root, 0755 |
| `ops/sshd/sshd_lnshelf_deploy.conf` | `/etc/ssh/sshd_lnshelf_deploy.conf` | root, 0644 |
| `ops/sshd/lnshelf-deploy-sshd.service` | `/etc/systemd/system/lnshelf-deploy-sshd.service` | root, 0644 |
| deploy host key | `/etc/ssh/lnshelf_deploy_host_ed25519_key` | root, 0600 |
| deploy public key | `/etc/ssh/lnshelf_deploy_keys` | root, 0644 |

Everything is root-owned, so the deploy account (`neko`) cannot change what the key runs.
Nothing here updates itself. A change is reviewed in the repository, then installed again by
hand with the steps below.

Deploy state lives outside the deploy directories, in `~/.local/state/lnshelf-deploy/`: the
lock, one `<version>.status` and one `<version>.log` per deploy. The live release is recorded in
`~/docker/lnshelf/.deployed`, and each release's compose file in
`~/docker/lnshelf/releases/<version>/`.

## Install (owner, once)

Run these on the server as `neko`; `sudo` asks for the password where root is needed.

### 1. Check the prerequisites

```bash
sudo apt install --no-install-recommends curl jq    # flock and setsid come with util-linux
ip -4 addr show docker0                              # expect inet 172.17.0.1/16
docker network inspect lnshelf_default --format '{{(index .IPAM.Config 0).Subnet}}'   # expect 172.16.0.0/12 (172.16–172.31)
```

If `docker0` is not `172.17.0.1`, change `ListenAddress` in `sshd_lnshelf_deploy.conf` before
installing. If a docker network sits outside `172.16.0.0/12`, widen the `from=` and ufw ranges
below to match.

`UsePAM no` means sshd refuses an account whose password is locked. Check that the second field
of `sudo passwd -S neko` is `P`.

### 2. Fetch the reviewed files

Take them from the exact commit you reviewed on main, and read them before installing them as
root. While the repository is still private, `scp` them from your checkout instead.

```bash
sha=<the reviewed commit on main, 40 hex characters>
work="$(mktemp -d)"
for file in ops/deploy/lnshelf-deploy-gate ops/deploy/lnshelf-deploy \
            ops/sshd/sshd_lnshelf_deploy.conf ops/sshd/lnshelf-deploy-sshd.service; do
    curl -fsSL --proto '=https' -o "$work/${file##*/}" \
        "https://raw.githubusercontent.com/DrNoLife/Book-Shelf/$sha/$file"
done
less "$work"/*
```

### 3. to 5., in one go

[`install.sh`](install.sh) does steps 3 to 5 below (scripts, host key, authorised key, config,
unit, ufw rule, start) and prints the host key line for `DEPLOY_KNOWN_HOSTS`. Generate the key
of step 4 first, as `neko`, then:

```bash
sudo ./install.sh ~/.lnshelf-deploy-key/id.pub
```

The steps it runs, for reading or for doing by hand:

### 3. Install the scripts, the host key, the config and the unit

```bash
sudo install -o root -g root -m 0755 "$work/lnshelf-deploy-gate" /usr/local/sbin/lnshelf-deploy-gate
sudo install -o root -g root -m 0755 "$work/lnshelf-deploy"      /usr/local/sbin/lnshelf-deploy

# A host key of its own, so the key GitHub pins is used for nothing else.
sudo ssh-keygen -q -t ed25519 -N '' -C lnshelf-deploy-host -f /etc/ssh/lnshelf_deploy_host_ed25519_key

sudo install -o root -g root -m 0644 "$work/sshd_lnshelf_deploy.conf"     /etc/ssh/sshd_lnshelf_deploy.conf
sudo install -o root -g root -m 0644 "$work/lnshelf-deploy-sshd.service"  /etc/systemd/system/lnshelf-deploy-sshd.service
sudo sshd -t -f /etc/ssh/sshd_lnshelf_deploy.conf
```

### 4. Create the deploy key

The key is generated on the server, so it never travels by mail or chat. Its private half goes
into GitHub once, and is then destroyed here.

```bash
key="$(mktemp -d)"
ssh-keygen -q -t ed25519 -N '' -C lnshelf-github-deploy -f "$key/id"

# restrict: no pty, no forwarding of any kind. from=: only from the docker networks, so the key
# is useless from the LAN or anywhere else. command=: the gate again, should the sshd config
# ever lose its ForceCommand.
printf 'restrict,from="172.16.0.0/12",command="/usr/local/sbin/lnshelf-deploy-gate" %s\n' "$(cat "$key/id.pub")" \
    | sudo tee /etc/ssh/lnshelf_deploy_keys > /dev/null
sudo chown root:root /etc/ssh/lnshelf_deploy_keys
sudo chmod 0644 /etc/ssh/lnshelf_deploy_keys
```

### 5. Open the port to the docker networks only, and start the daemon

```bash
# Only if ufw is active (sudo ufw status). The tunnel container's traffic comes from its
# docker network, never from the LAN.
sudo ufw allow proto tcp from 172.16.0.0/12 to 172.17.0.1 port 2222 comment 'LnShelf deploy sshd: docker networks only'

sudo systemctl daemon-reload
sudo systemctl enable --now lnshelf-deploy-sshd
sudo ss -ltnp | grep 2222          # expect 172.17.0.1:2222 and nothing else
```

### 6. Test it from the server itself

Connections from the server to `172.17.0.1` come from `172.17.0.1`, which is inside the `from=`
range, so the server can test the whole path except the tunnel.

```bash
deploy_ssh() { ssh -i "$key/id" -p 2222 -o IdentitiesOnly=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null neko@172.17.0.1 "$@"; }
deploy_ssh status 0.0.0           # expect: unknown
deploy_ssh ls                     # expect: refused: unrecognised command
deploy_ssh 'status 0.0.0; id'     # expect: refused: unrecognised command
deploy_ssh                        # expect: PTY allocation request failed, then refused (no shell)
ssh -p 2222 neko@172.17.0.1       # your normal key; expect: Permission denied (publickey)
```

From another machine on the LAN, `ssh -p 2222 <server LAN address>` must not connect at all.

### 7. Hand the secrets to GitHub, then destroy the private key

Do this only after the repository rulesets and the `production` environment exist (the
production-readiness plan, step 6). Put these under **Settings → Environments → production →
Environment secrets**:

| Secret | Value |
|---|---|
| `DEPLOY_SSH_KEY` | the whole of `cat "$key/id"` |
| `DEPLOY_SSH_HOST` | the deploy hostname, `deploy-ssh.<your domain>` (ops/cloudflared/README.md) |
| `DEPLOY_KNOWN_HOSTS` | the output of the command below |
| `CF_ACCESS_CLIENT_ID`, `CF_ACCESS_CLIENT_SECRET` | the Access service token (ops/cloudflared/README.md) |

```bash
# The workflow connects with HostKeyAlias set to DEPLOY_SSH_HOST, so the line names that host.
printf '%s %s\n' 'deploy-ssh.<your domain>' "$(cut -d' ' -f1,2 /etc/ssh/lnshelf_deploy_host_ed25519_key.pub)"

shred -u "$key/id" && rm -rf "$key" "$work"
```

## Updating the scripts

Repeat steps 2 and 3 for the scripts only, from the new reviewed commit. A running deploy keeps
the version it started with. To change the sshd config, install it, then run
`sudo sshd -t -f /etc/ssh/sshd_lnshelf_deploy.conf && sudo systemctl reload lnshelf-deploy-sshd`.
A restart of the unit does not interrupt a deploy in progress (`KillMode=process`).

## Revoking access

- **At once:** `sudo systemctl stop lnshelf-deploy-sshd`, and revoke the service token in
  Cloudflare. Either one alone closes the path.
- **To rotate the key:** repeat step 4, test it with step 6, and replace `DEPLOY_SSH_KEY`. The
  old line is gone as soon as `tee` overwrites the file.
- **To rotate the host key:** generate a new one, restart the unit, and replace
  `DEPLOY_KNOWN_HOSTS`.

## Deploying by hand (emergency)

When GitHub or the tunnel is down, SSH in over the LAN with your normal key and run the deploy as
`neko`, never with `sudo`:

```bash
lnshelf-deploy <version> <commit sha> sha256:<api digest> sha256:<client digest>
```

The digests are in that release's `digests.txt` on GitHub, and in `~/docker/lnshelf/.deployed`
for the version that is live. For a version deployed before, `releases/<version>/` is still
there, so this works offline. For a new one, fetch `docker-compose.production.yml` for its commit
into `~/docker/lnshelf/releases/<version>/` first. Run by hand, the script trusts its arguments;
the GitHub checks are the gate's job.
