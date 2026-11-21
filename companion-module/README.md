# DMX Light Control Companion module

Controls DMX Light Control from Bitfocus Companion (Stream Deck): scenes, blackout, master, colours,
effects, flash, tap tempo, strobe and timelines. Buttons show the active scene, blackout and
whether the DMX output is connected.

## Install

1. Easiest: in DMX Light Control, Koppelingen > Stream Deck (Companion) > "Install in Companion". By hand: run `npm install --omit=dev` here, then copy the whole folder (including `node_modules`) to
   `%APPDATA%\companion\modules\dmxlicht-2.1.0`, the same place as other locally installed modules.
2. Restart Companion and add the connection "DMX Light Control".
3. Host `127.0.0.1` and port `5150` when Companion runs on the same PC. From another PC, use that PC's
   address, turn on "Control from tablet or phone" in DMX Light Control, and paste the key from its
   Integrations page.

The preset tab has a button for each scene (with its colour), plus Blackout, Tap, Strobe (hold),
Stop and Flash buttons.
