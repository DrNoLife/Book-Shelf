#!/usr/bin/env bash
# Installs the deploy sshd, the deploy gate and the deploy script as root, in one go
# (ops/sshd/README.md, steps 3 to 5). Run on the server from the folder holding the reviewed
# files, with the deploy key's public half as the only argument:
#
#   sudo ./install.sh /home/neko/.lnshelf-deploy-key/id.pub
#
# Safe to run again: it reinstalls the files, keeps an existing host key (so the pin in GitHub
# stays valid), and rewrites the authorised key line.
set -euo pipefail
export LC_ALL=C

[[ $EUID -eq 0 ]] || { echo "Run with sudo."; exit 1; }
public_key_file="${1:?Usage: sudo ./install.sh <deploy key .pub>}"
here="$(cd "$(dirname "$0")" && pwd)"
deploy_dir="$here/../deploy"

public_key="$(cat "$public_key_file")"
[[ "$public_key" =~ ^ssh-ed25519\ [A-Za-z0-9+/=]+(\ .*)?$ ]] || { echo "Not an ed25519 public key."; exit 1; }

echo "==> Scripts"
install -o root -g root -m 0755 "$deploy_dir/lnshelf-deploy-gate" /usr/local/sbin/lnshelf-deploy-gate
install -o root -g root -m 0755 "$deploy_dir/lnshelf-deploy" /usr/local/sbin/lnshelf-deploy

echo "==> Host key"
# A host key of its own, so the key GitHub pins is used for nothing else.
if [[ ! -f /etc/ssh/lnshelf_deploy_host_ed25519_key ]]; then
    ssh-keygen -q -t ed25519 -N '' -C lnshelf-deploy-host -f /etc/ssh/lnshelf_deploy_host_ed25519_key
fi

echo "==> Authorised key"
# restrict: no pty and no forwarding. from=: only the docker networks, so the key is useless
# from the LAN. command=: the gate again, should the config ever lose its ForceCommand.
printf 'restrict,from="172.16.0.0/12",command="/usr/local/sbin/lnshelf-deploy-gate" %s\n' "$public_key" \
    > /etc/ssh/lnshelf_deploy_keys
chown root:root /etc/ssh/lnshelf_deploy_keys
chmod 0644 /etc/ssh/lnshelf_deploy_keys

echo "==> sshd config and unit"
install -o root -g root -m 0644 "$here/sshd_lnshelf_deploy.conf" /etc/ssh/sshd_lnshelf_deploy.conf
install -o root -g root -m 0644 "$here/lnshelf-deploy-sshd.service" /etc/systemd/system/lnshelf-deploy-sshd.service
install -d -m 0755 /run/sshd
sshd -t -f /etc/ssh/sshd_lnshelf_deploy.conf

echo "==> Firewall"
if ufw status | grep -q '^Status: active'; then
    ufw allow proto tcp from 172.16.0.0/12 to 172.17.0.1 port 2222 comment 'LnShelf deploy sshd: docker networks only'
else
    echo "    ufw is not active; nothing to open."
fi

echo "==> Start"
systemctl daemon-reload
systemctl enable --now lnshelf-deploy-sshd
systemctl restart lnshelf-deploy-sshd
sleep 1
ss -ltn | grep ':2222 ' || { echo "Nothing listens on 2222."; exit 1; }

# UsePAM no refuses an account whose password is locked; the second field must be P.
if [[ "$(passwd -S neko | awk '{print $2}')" != P ]]; then
    echo "WARNING: neko's password is locked or unset; the deploy sshd will refuse the account."
fi

echo "==> Done. Host key line for DEPLOY_KNOWN_HOSTS:"
cut -d' ' -f1,2 /etc/ssh/lnshelf_deploy_host_ed25519_key.pub
