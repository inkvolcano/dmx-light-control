# DMX Light Control

DMX lighting control for Windows: a portable app (nothing to install) with a web interface, for a uDMX USB
dongle, Art-Net or sACN. Interface in Dutch and English.

DMX Light Control was called **DmxLicht** until version 2.1.7 and **Glowsmith** until 2.1.14. Only the name you see
changed: the program file is still `DmxLicht.exe`, releases are `DMX-Light-Control-<version>.zip` since 2.1.17 (`DmxLicht-<version>.zip` before), and the code keeps the
`DmxLicht.*` project names, so existing installs, shows and the Companion connection carry on as before.

**Download:** the latest `DMX-Light-Control-<version>.zip` from [Releases](https://github.com/inkvolcano/dmx-light-control/releases).
Unzip anywhere and start `DmxLicht.exe` (Windows 10/11, x64). The app updates itself from Releases (Settings > Updates).

DMX Light Control grew from a colleague's single-file WinForms app *DMX Lichtbesturing v1.4*. The DMX output for v1.4's
features is proven identical by 2,520 recorded reference frames (`tests/DmxLicht.Tests/golden`). The original
source is not part of this public repository.

## What it does

- **Fixtures**: library from the manuals (LED PAR 18W×7, Expolite TourLED 42 CM, Stairville xBrick incl.
  4 segments, SGM P-2 incl. 3 panels and 16-bit modes) plus generic types; own types in an editor; Open Fixture
  Library import; mixed rigs; up to 8 universes (patch per universe; Art-Net/sACN send all, the dongle one).
- **Control**: colours, on/off, master, blackout, strobe (lamp strobe channels or software), stage view with
  live colours, drag to place, groups, scenes with fades, transition time, faders, test page / DMX monitor.
- **Effects**: 66 (40 from v1.4 + 26 new, 14 sound-reactive), on all fixtures or per group (layers), plus
  **own chases** from the chase editor (steps × columns, direction, spread, wave offset, fades, beat sync).
- **Timeline**: scene and effect tracks, drag and drop, beat grid, loop, songs (the app plays them and
  follows the song position).
- **Music**: tap tempo, BPM, beat/tempo detection from microphone or this PC's sound (loopback),
  brightness from volume/bass/beat, effects stepping on the beat.
- **Outputs**: uDMX (WinUSB, libusbK fallback; adaptive sending, auto-reconnect, diagnostics), Art-Net, sACN.
- **External control**: one command language over trigger URLs (`/api/cmd/...`, key-protected from other
  devices), OSC, MIDI (learn), and a Bitfocus Companion module (`companion-module/`). WhatsApp notifications
  via an Android automation app calling a trigger URL.
- **Operating**: NL/EN UI, help per page (F1) and ? explanations, simple operator view with PIN (enforced on the
  server), undo/redo, named shows, automatic backups and backup zips, portable self-contained build.
- **Phone / tablet**: easy mode (big buttons; also the locked operator view, sections configurable), full UI
  usable on phones, pairing card with QR code, connected-device list and a firewall rule (UAC, local subnet
  only), home-screen web app (manifest + icons); network access switches on without an app restart.
  Integrations page: action link builder with QR code, iPhone Shortcuts/Siri/automation and Android MacroDroid steps.
- **Schedule**: actions at times, on days, once on a date, or around sunrise/sunset (NOAA, own location); runs any
  command, optionally faded over seconds to hours (e.g. a 30-minute sunrise alarm); catch-up limited to 2 minutes
  after sleep.
- **Follow the screen** (ambilight): GDI-scaled capture of a monitor, columns left to right or the average, colour
  boost, smoothing, brightness following; as an effect, per group, by command (`effect screen`), in HA and Companion.
  Second source: a webcam pointed at a TV (FlashCap/DirectShow, ~320x180), with a draggable four-corner frame that
  straightens a TV filmed at an angle; camera only on while needed; the preview picture is served to this PC only.
  Layouts: columns, average, or real ambilight edges (lamps per side or automatic, edge depth, direction, start
  lamp, black-bar skipping) on a 32x18 grid from either source.
- **Home Assistant**: MQTT with discovery (light with colour/brightness/effects, group lights, scene/effect selects,
  master, blackout/strobe switches, buttons, dongle/BPM sensors, raw command text entity).
- **Diagnostics**: crash files (`Instellingen/Crashes`), rotating log, and a one-click problem report zip
  (log, crashes, app/Windows info, the dongle as Windows sees it, USB power settings, plug/unplug events).
- **Dongle robustness**: retries, adaptive block size, watchdog for hung transfers, safe mode after repeated
  drops, USB power-saving switch-off (UAC), Windows device restart.

## Licence and credits

GPL-3.0 (see `LICENSE`). Third-party parts keep their own licences: MQTTnet (MIT), FlashCap (Apache-2.0),
NAudio (MIT), Microsoft WebView2, React, dnd-kit, Vite, qrcode-generator (MIT), Bitfocus Companion module base (MIT).
Fixture definitions were written from the manufacturers' manuals and DMX charts (Expolite, Stairville, SGM);
the manuals themselves are not included — get them from the manufacturers.

Not tested with every dongle and fixture: use at your own risk, and send a problem report (Settings > Problem
report) when something goes wrong.

## Layout

| Path | What |
|---|---|
| `src/DmxLicht.Core` | Engine without UI: fixtures, show, effects, audio analysis, timeline, outputs, control, storage |
| `src/DmxLicht.App` | The app: engine + local web server (API + WebSocket) + window (WebView2), audio/MIDI I/O |
| `ui/` | Web interface (React + TypeScript + Vite); builds into `src/DmxLicht.App/wwwroot` |
| `tests/DmxLicht.Tests` | ~2,610 tests: v1.4 parity, fixtures, effects, dongle simulator, audio, scenes, timeline, network, control, undo |
| `tools/GoldenHarness` | Records reference frames from the original v1.4 source (needs that source, not public) |
| `tools/Publish-Public.ps1` | Publishes a clean copy to the public repo and creates the release used by auto-update |
| `companion-module/` | Bitfocus Companion module |
| `original_v1.4/` | The colleague's original app (private repo only; not published) |
| `Bouwen.bat` | Builds the portable zip in `publish/` |

## Build, run, ship

Needs the .NET 9 SDK and Node.js to **build**. The result needs nothing installed.

```powershell
.\Bouwen.bat          # UI + tests + self-contained publish + Companion module -> publish\DMX-Light-Control-<version>.zip
dotnet test
```

The version lives in `version.txt`; every `Bouwen.bat` run raises the last number (2.1.0 -> 2.1.1) and saves it
only when the build succeeds. A new version folder copies `Instellingen` (the show) from the newest previous one.

During development: `cd ui; npm install; npm run build`, then run `src/DmxLicht.App`
(`--browser` opens the UI in the default browser, `--headless` runs without a window,
`--allow-second` skips the single-instance guard). For live UI editing run the app and `npm run dev` in `ui/`.

Everything the app saves goes to `Instellingen/` next to the exe: `show.json` (autosaved), `Shows/`,
`Backups/` (copy on every start), `Fixtures/` (own types), `Audio/` (songs), `dmx-log.txt`, `WebView2/`.
On first start next to an old v1.4 `Instellingen` folder, its ini and custom colours are imported.

## Engine overview

- `Fixtures/`: `FixtureLibrary` (built-ins + JSON types), `OflImport`.
- `Show/`: `Rig` (patch resolved, heads = lamps or segments), `FrameBuilder` (renders a frame: dimmer handling,
  segments, HSV, colour folding, strobe, group layers, identify), `Scenes`, `Timeline`.
- `Effects/EffectLibrary`: effects as a table; append to add more (shows refer to them by index).
- `Engine/LightEngine`: owns the show, renders ~40 frames/s, crossfades, flash, test overrides, undo/redo;
  all changes go through `Change()` (lock + `Changed` event for autosave and UI push).
  `TimelinePlayer` applies timelines; `BeatClock` is the musical clock.
- `Audio/AudioAnalyzer`: bands, auto-gain, beat and tempo detection (no audio API; the app feeds samples).
- `Output/`: `UdmxOutput` (send loop) over `WinUsbTransport` / `LibusbKTransport`, `ArtNetOutput`, `SacnOutput`,
  `MultiOutput`.
- `Control/`: `Commands` (the command language), `OscServer`, `ControlSettings`.
- `Legacy/`: v1.4's layout as a rig (parity tests) and the ini import.

## Releasing

1. `.\Bouwen.bat` raises `version.txt`, builds `publish\DMX-Light-Control-<version>.zip` and its `.sha256`.
2. Commit, then `powershell -ExecutionPolicy Bypass -File tools\Publish-Public.ps1 -Release -Notes "what's new"`:
   copies HEAD (without the private files) to github.com/inkvolcano/dmx-light-control with a noreply author and
   creates release `v<version>` with the zip and checksum. Running apps offer the update within a day.

## Lamps (from the manuals)

| Lamp | Modes | Notes |
|---|---|---|
| LED PAR 18W×7 RGBWA+UV (`LED_Par_2026.pdf`) | 6ch d001 · 10ch A001 (Dim, RGBWA+UV, Strobe, Function, Speed) | 10ch: Function held at 0 (DMX control) |
| Expolite TourLED 42 CM (RGB) | TOUR 10ch · Arc.1 3ch · Ar1.d 4ch · Ar1.s 5ch · HSV 3ch | TOUR: macro/auto/ID held at 0; strobe 11-255 |
| Stairville xBrick Full-Colour 16×3W (RGB) | 5ch (RGB, Dim, Strobe) · 13ch (4 segments × RGB, Dim) | 13ch: 4 heads for effects |
| SGM P-2 (RGBW, 3 panels) (`SGM-P2.pdf` + DMX protocol rev. 6) | 3 · 4 · 6 CTC · 6 RAW · 9 · 10 · 10 CTC · 12 · 15 · 22ch | Shutter held open (0-7 = closed), strobe 151 slow → 16 fast; CTC held at "no correction"; 16-bit fine channels held at 0; panel modes = 3 heads |

## Not done / not possible here

- **Tested on hardware** (21 Nov 2026): uDMX with the WinUSB driver (Zadig) driving three LED PAR 18W×7 in 10ch mode
  (addresses 1, 11, 21): 16 frames/s, no errors; Stream Deck Mini through Companion 5.0.4 with the DmxLicht module.
- **Not tested on hardware**: uDMX with the libusbK driver, the other lamp types, real audio capture.
- **Code signing** (removes the SmartScreen warning) needs a purchased certificate.
- **WhatsApp on the PC**: WhatsApp offers no hook for desktop programs; reading Windows notifications needs a
  Store-packaged app. The working route is a phone automation (MacroDroid/Tasker) calling a trigger URL.
