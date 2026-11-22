#!/bin/bash
# DMX Light Control: update helper behind the "install update" button on a Raspberry Pi (Linux).
#
# install.sh puts this file at /usr/local/lib/dmx-light-control/update.sh (root, 0755) and writes
# /etc/sudoers.d/dmx-light-control, which lets the service user dmxlicht run ONLY this script as root:
#
#   sudo -n /usr/local/lib/dmx-light-control/update.sh <version> /var/lib/dmx-light-control/Updates/<package>.tar.gz
#
# It trusts nothing the app hands it. It takes only a bare version number and the package the app downloaded to its
# own Updates folder, copies that into a root-only folder, checks it against the .sha256 it fetches ITSELF from the
# GitHub release of that version, and only then unpacks it. Installing is the package's own install.sh: exactly
# what "sudo bash install.sh" does by hand, so a new version also brings a new helper.
#
# Step 1 (while the app waits for the answer): check and unpack; a problem stops here and changes nothing.
# Step 2 (in its own systemd unit "dmx-light-control-update", because install.sh stops the service and with it
# everything the service started): back up the program, run install.sh, and if the new version does not run, put
# the old one back and start it. How it went: /var/lib/dmx-light-control/update-result.txt (the app shows it) and
#   journalctl -u dmx-light-control-update
set -euo pipefail
umask 077
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
export LC_ALL=C

REPO=inkvolcano/dmx-light-control
APP=/opt/dmx-light-control
DATA=/var/lib/dmx-light-control
SERVICE=dmx-light-control
UNIT=/etc/systemd/system/$SERVICE.service
JOB=dmx-light-control-update
USER_NAME=dmxlicht
WORKROOT=/var/tmp

fail() { echo "$*" >&2; exit 1; }

valid_version() { [[ "$1" =~ ^[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,6}$ ]]; }

# "ok 2.1.44" or "failed 2.1.44: reason", for the app to show after its restart. The data folder belongs to
# dmxlicht: write a fresh file (mktemp never follows a link) and rename it into place.
write_result() {
  local tmp
  tmp="$(mktemp "$DATA/.update-result.XXXXXX")" || return 0
  printf '%s\n' "$1" > "$tmp"
  chown "$USER_NAME:$USER_NAME" "$tmp" 2>/dev/null || true
  chmod 640 "$tmp"
  mv -f -T "$tmp" "$DATA/update-result.txt" 2>/dev/null || rm -f "$tmp"
}

# ---------- step 1: check and unpack, then start step 2 ----------
prepare() {
  local version="$1" package="$2" arch name real expected actual url
  work=""   # global: the EXIT trap below removes it on any failure

  valid_version "$version" || fail "ongeldige versie: $version"
  case "$(uname -m)" in
    aarch64|arm64) arch=arm64 ;;
    x86_64|amd64)  arch=x64 ;;
    *) fail "onbekende processor: $(uname -m)" ;;
  esac
  name="DMX-Light-Control-$version-linux-$arch"

  # only the package the app downloaded for this version, in its own Updates folder, and not a link
  [ ! -L "$package" ] || fail "het pakket mag geen link zijn"
  real="$(readlink -f -- "$package" 2>/dev/null || true)"
  [ "$real" = "$DATA/Updates/$name.tar.gz" ] || fail "verwacht $DATA/Updates/$name.tar.gz, niet $package"
  [ -f "$real" ] || fail "pakket niet gevonden: $real"

  if systemctl is-active --quiet "$JOB" 2>/dev/null; then fail "er loopt al een update"; fi

  work="$(mktemp -d "$WORKROOT/$JOB.XXXXXX")"
  trap 'rm -rf "$work"' EXIT
  cp -- "$real" "$work/pkg.tar.gz"

  # the checksum straight from GitHub, not the one the app fetched
  url="https://github.com/$REPO/releases/download/v$version/$name.tar.gz.sha256"
  expected=""
  if command -v curl >/dev/null 2>&1; then
    expected="$(curl -fsSL --max-time 60 --proto '=https' --proto-redir '=https' "$url" 2>/dev/null | awk '{print tolower($1); exit}' || true)"
  elif command -v wget >/dev/null 2>&1; then
    expected="$(wget -qO- --timeout=60 --https-only "$url" 2>/dev/null | awk '{print tolower($1); exit}' || true)"
  else
    fail "curl ontbreekt (sudo apt install curl)"
  fi
  expected="${expected#\*}"
  [[ "$expected" =~ ^[0-9a-f]{64}$ ]] || fail "controlegetal niet op te halen van GitHub ($url)"
  actual="$(sha256sum "$work/pkg.tar.gz" | awk '{print $1}')"
  [ "$actual" = "$expected" ] || fail "controlegetal klopt niet: download beschadigd of niet echt"

  mkdir "$work/x"
  tar -xzf "$work/pkg.tar.gz" -C "$work/x" --no-same-owner --no-same-permissions || fail "uitpakken mislukt"
  [ -d "$work/x/$name" ] || fail "de map $name ontbreekt in het pakket"
  for f in DmxLicht install.sh; do [ -f "$work/x/$name/$f" ] || fail "$f ontbreekt in het pakket"; done
  [ -d "$work/x/$name/wwwroot" ] || fail "wwwroot ontbreekt in het pakket"
  mv "$work/x/$name" "$work/pkg"
  rm -rf "$work/x" "$work/pkg.tar.gz"

  # step 2 runs a copy of this script: install.sh replaces the original while it runs
  install -m 700 -- "${BASH_SOURCE[0]}" "$work/update.sh"

  systemd-run --unit="$JOB" --collect --quiet --description="DMX Light Control bijwerken naar $version" \
    /bin/bash "$work/update.sh" --apply "$work" "$version" || fail "kon het bijwerken niet starten (systemd-run)"
  trap - EXIT
  echo "bijwerken naar $version gestart"
}

# ---------- step 2: install, or put the old version back ----------
apply() {
  local work="$1" version="$2" reason=""

  # only as started by step 1: from systemd, not through sudo, on a root-owned work folder
  [ -z "${SUDO_USER:-}" ] && [ -n "${INVOCATION_ID:-}" ] || fail "--apply start alleen vanuit het script zelf"
  [[ "$work" =~ ^$WORKROOT/$JOB\.[A-Za-z0-9]+$ ]] && [ -d "$work" ] && [ ! -L "$work" ] || fail "onbekende werkmap: $work"
  [ "$(stat -c %u "$work")" = 0 ] || fail "werkmap is niet van root"
  valid_version "$version" || fail "ongeldige versie: $version"
  export DEBIAN_FRONTEND=noninteractive

  echo "Bijwerken naar $version..."
  mkdir -p "$work/backup"
  cp -a "$APP" "$work/backup/app"
  [ -f "$UNIT" ] && cp -a "$UNIT" "$work/backup/unit"

  if ! bash "$work/pkg/install.sh"; then
    reason="install.sh stopte met een fout"
  else
    # install.sh already waited a few seconds; a version that starts and then falls over shows after a while
    sleep 15
    systemctl is-active --quiet "$SERVICE" || reason="de nieuwe versie blijft niet draaien"
  fi

  if [ -z "$reason" ]; then
    write_result "ok $version"
    rm -rf "$work"
    echo "Bijgewerkt naar $version."
    exit 0
  fi

  echo "Mislukt ($reason): de vorige versie komt terug." >&2
  systemctl stop "$SERVICE" 2>/dev/null || true
  rm -rf "$APP.failed"
  [ -d "$APP" ] && mv "$APP" "$APP.failed"
  cp -a "$work/backup/app" "$APP"
  rm -rf "$APP.failed"
  if [ -f "$work/backup/unit" ]; then cp -a "$work/backup/unit" "$UNIT"; systemctl daemon-reload; fi
  systemctl restart "$SERVICE" || true
  write_result "failed $version: $reason"
  rm -rf "$work"
  exit 1
}

main() {
  [ "$(id -u)" -eq 0 ] || fail "start met sudo"
  if [ "${1:-}" = "--apply" ] && [ $# -eq 3 ]; then
    apply "$2" "$3"
  elif [ $# -eq 2 ] && [ "${1:0:1}" != "-" ]; then
    prepare "$1" "$2"
  else
    fail "gebruik: sudo $0 <versie> $DATA/Updates/DMX-Light-Control-<versie>-linux-<arch>.tar.gz"
  fi
}

# all of it is read before anything runs (a function), so replacing this file meanwhile cannot change what runs
main "$@"
exit $?
