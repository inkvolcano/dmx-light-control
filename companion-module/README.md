# Glowsmith Companion module

Controls Glowsmith from Bitfocus Companion (Stream Deck): scenes, blackout, master, colours,
effects, flash, tap tempo, strobe and timelines. Buttons show the active scene, blackout and
whether the DMX output is connected.

## Install

1. Easiest: in Glowsmith, Koppelingen > Stream Deck (Companion) > "Install in Companion". By hand: run `npm install --omit=dev` here, then copy the whole folder (including `node_modules`) to
   `%APPDATA%\companion\modules\dmxlicht-2.1.0`, the same place as other locally installed modules.
2. Restart Companion and add the connection "Glowsmith".
3. Host `127.0.0.1` and port `5150` when Companion runs on the same PC. From another PC, use that PC's
   address, turn on "Control from tablet or phone" in Glowsmith, and paste the key from its
   Integrations page.

The preset tab has a button for each scene (with its colour), plus Blackout, Tap, Strobe (hold),
Stop and Flash buttons.
