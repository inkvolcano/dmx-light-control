#!/bin/bash
# DMX Light Control on a Raspberry Pi (Raspberry Pi OS 64-bit) - install, update or remove.
#
#   sudo bash install.sh              install or update (your show stays)
#   sudo bash install.sh --uninstall  remove the program, the service and the udev rule (the show stays)
#   sudo bash install.sh --purge      remove everything, the show in /var/lib/dmx-light-control too
#
# Program: /opt/dmx-light-control   Show, settings and log: /var/lib/dmx-light-control
# Service: dmx-light-control (user dmxlicht)   Web page: http://<this Pi>:5150
set -euo pipefail

APP=/opt/dmx-light-control
DATA=/var/lib/dmx-light-control
SERVICE=dmx-light-control
UNIT=/etc/systemd/system/$SERVICE.service
RULE=/etc/udev/rules.d/60-udmx.rules
USER_NAME=dmxlicht
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(id -u)" -ne 0 ]; then
  echo "Start met sudo / run with sudo:  sudo bash $0 $*"
  exit 1
fi

if [ "${1:-}" = "--uninstall" ] || [ "${1:-}" = "--purge" ]; then
  systemctl disable --now "$SERVICE" 2>/dev/null || true
  rm -f "$UNIT" "$RULE"
  systemctl daemon-reload
  udevadm control --reload-rules 2>/dev/null || true
  rm -rf "$APP"
  if [ "$1" = "--purge" ]; then
    rm -rf "$DATA"
    userdel "$USER_NAME" 2>/dev/null || true
    echo "DMX Light Control is verwijderd, de show ook."
  else
    echo "DMX Light Control is verwijderd. De show staat nog in $DATA (weg ermee: sudo bash $0 --purge)."
  fi
  exit 0
fi

if [ ! -f "$HERE/DmxLicht" ] || [ ! -d "$HERE/wwwroot" ]; then
  echo "DmxLicht of wwwroot ontbreekt naast install.sh: pak eerst de hele tar.gz uit."
  exit 1
fi

case "$(uname -m)" in
  aarch64|arm64) ;;
  *) echo "Dit pakket is voor 64-bit ARM (Raspberry Pi OS 64-bit); deze computer is $(uname -m)."
     echo "Op een 32-bit Raspberry Pi OS werkt het niet: installeer de 64-bit versie."
     exit 1 ;;
esac

echo "[1/5] Benodigde pakketten..."
need=()
ldconfig -p | grep -q 'libusb-1.0.so.0' || need+=(libusb-1.0-0)
command -v arecord >/dev/null 2>&1 || need+=(alsa-utils)
if ! ldconfig -p | grep -q 'libicuuc.so'; then
  icu="$(apt-cache search --names-only '^libicu[0-9]+$' 2>/dev/null | awk '{print $1}' | sort -V | tail -n 1)"
  need+=("${icu:-libicu-dev}")
fi
if [ ${#need[@]} -gt 0 ]; then
  echo "      installeren: ${need[*]}"
  apt-get update -qq
  apt-get install -y -qq "${need[@]}"
fi

echo "[2/5] Gebruiker $USER_NAME..."
if ! id "$USER_NAME" >/dev/null 2>&1; then
  useradd --system --home-dir "$DATA" --no-create-home --shell /usr/sbin/nologin "$USER_NAME"
fi
for g in plugdev audio systemd-journal; do
  getent group "$g" >/dev/null && usermod -aG "$g" "$USER_NAME"
done

echo "[3/5] Programma naar $APP..."
systemctl stop "$SERVICE" 2>/dev/null || true
rm -rf "$APP.new" "$APP.old"
mkdir -p "$APP.new"
cp -a "$HERE/DmxLicht" "$HERE/wwwroot" "$APP.new/"
for f in LEESMIJ-Linux.txt install.sh 60-udmx.rules dmx-light-control.service; do
  [ -f "$HERE/$f" ] && cp "$HERE/$f" "$APP.new/"
done
# a tar.gz made on Windows has no Unix rights (everything 0666): set them here
chown -R root:root "$APP.new"
chmod -R u=rwX,go=rX "$APP.new"
chmod 755 "$APP.new/DmxLicht"
[ -d "$APP" ] && mv "$APP" "$APP.old"
mv "$APP.new" "$APP"
rm -rf "$APP.old"

# first install with a show from Windows next to install.sh (the folder "Instellingen"): take it over
mkdir -p "$DATA"
if [ ! -f "$DATA/show.json" ] && [ -f "$HERE/Instellingen/show.json" ]; then
  echo "      show uit $HERE/Instellingen overgenomen"
  cp -a "$HERE/Instellingen/." "$DATA/"
  rm -rf "$DATA/WebView2" "$DATA/Updates"
fi
chown -R "$USER_NAME:$USER_NAME" "$DATA"
chmod -R u=rwX,g=rX,o= "$DATA"

echo "[4/5] udev-regel voor de uDMX..."
install -m 644 "$HERE/60-udmx.rules" "$RULE"
udevadm control --reload-rules
udevadm trigger --subsystem-match=usb --attr-match=idVendor=16c0 || true

echo "[5/5] Service $SERVICE..."
install -m 644 "$HERE/dmx-light-control.service" "$UNIT"
systemctl daemon-reload
systemctl enable "$SERVICE" >/dev/null 2>&1
systemctl restart "$SERVICE"

sleep 4
echo
if systemctl is-active --quiet "$SERVICE"; then
  echo "DMX Light Control draait."
else
  echo "DMX Light Control start niet. Kijk in het logboek:  journalctl -u $SERVICE -n 50"
  exit 1
fi
port="$(grep -o '"port": *[0-9]*' "$DATA/show.json" 2>/dev/null | head -n 1 | grep -o '[0-9]*$' || true)"
port="${port:-5150}"
ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
echo "Open op je telefoon, tablet of pc (zelfde netwerk):"
[ -n "$ip" ] && echo "  http://$ip:$port"
echo "  http://$(hostname).local:$port"
if [ -f "$DATA/koppelcode.txt" ]; then
  echo "Koppelcode voor het eerste apparaat: $(cat "$DATA/koppelcode.txt")"
  echo "(later opnieuw:  sudo cat $DATA/koppelcode.txt)"
fi
