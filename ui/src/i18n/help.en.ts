// Help texts and chase editor (English). Lines: "# " = heading, "- " = bullet, "1. " = numbered.
export const helpEn: Record<string, string> = {
  "help.button": "Help",
  "help.what": "What is this?",
  "help.title": "Help",
  "help.footer": "Tip: press F1 for help on the page you are on. The ? next to controls gives a short explanation.",
  "help.tab.start": "Quick start",
  "help.tab.trouble": "Troubleshooting",
  "help.tab.keys": "Shortcuts",
  "help.tab.glossary": "Glossary",
  "nav.operator": "Operator",

  "help.start.text": `# Light in 5 steps
1. Plug in the USB dongle (uDMX) and connect the DMX cable to the first fixture. Top right says "Connected" when it works.
2. Go to Fixtures and add your fixtures (drag or "Add"). Give each one the same DMX address and mode as on the fixture's display.
3. Click "Find" on Fixtures: the fixture flashes white, so you know which one is which.
4. Go to Control: click fixtures (or none = all) and pick a colour. The slider at the top sets the brightness.
5. Save what you like as a scene ("Save" next to the scenes). One click brings it back later.
# Next
- Effects: pick an effect (chase, rainbow, police...) or make your own chase.
- Timeline: put scenes and effects in a sequence, optionally to music.
- Music: make the light move to the beat (TAP or listening through the microphone).
- Settings: a PIN and lock for a simple operator view for others.
# Good to know
- Everything is saved automatically in the Instellingen folder next to DmxLicht.exe.
- Ctrl+Z undoes almost everything.`,

  "help.trouble.text": `# Fixtures do nothing
1. Look top right: does it say "Connected to uDMX"? If not, see the dongle section below.
2. Is Blackout on (top)? Is the brightness at 0?
3. Do the fixture's DMX address and mode match Fixtures? Check the fixture's display.
4. Use Test > "Fixture by fixture" to try each fixture on its own.
5. Is there a DMX cable from the dongle to the first fixture, and from fixture to fixture (DMX OUT to DMX IN)?
# The dongle drops out
1. Click the status top right: it tries to restore the connection.
2. Settings > Output > "Recover USB". If that fails, Windows can restart the dongle (asks for administrator rights).
3. Settings > Output > "Turn off USB power saving": Windows then no longer puts the USB port to sleep. Often helps on laptops.
4. Use a USB 2.0 port (black, not blue) or a powered USB 2.0 hub. The uDMX is sometimes unreliable on USB 3 ports.
5. Laptop on the charger, short USB cable, no extension cables.
6. Put a DMX terminator (120 Ω) in the last fixture. Noise on the DMX line can reset the dongle.
7. Run only one program with the dongle at a time (not the old v1.4 or QLC+ as well).
8. Keeps happening? Make a problem report below (right after it went wrong) and send the zip file.
# Empty window
- Start "DmxLicht.exe --browser": the controls open in your normal browser.
# Deleted something by accident
- Ctrl+Z (undo), or Settings > Shows > restore an automatic backup.`,

  "help.keys.text": `- F1: help for this page
- Ctrl+Z: undo · Ctrl+Y: redo
- Space bar (on Music): tap the tempo (TAP)
- Click a fixture: select · Ctrl/Shift+click: select more
- Strobe button at the top: hold it down
- Chase editor: click/drag = paint, right-click = off, Ctrl+Z/Ctrl+Y inside the editor, Esc = close
- Timeline: space = play/pause, Enter = GO, Home = stop, arrows = previous/next point`,

  "help.glossary.text": `- DMX: the signal that controls fixtures. One cable ("universe") has 512 channels.
- Address: the first channel of a fixture. A 7-channel fixture at address 1 uses 1-7; the next one starts at 8.
- Mode: how many channels a fixture uses (e.g. 6ch or 10ch). Must match the setting on the fixture itself.
- uDMX: the small USB dongle that connects the PC to the DMX cable.
- Scene: a saved lighting look (colours, effects) you recall with one click.
- Transition time: how long it takes to fade to a new colour or scene.
- Effect: a moving pattern (chase, blink, rainbow).
- Chase: an effect you make yourself, step by step.
- Group: a set of fixtures (e.g. "Front" and "Back") to control separately or give their own effect.
- Master/brightness: the overall brightness of all fixtures.
- Blackout: everything dark at once, without forgetting anything.
- Strobe: fast flashing.
- BPM: beats per minute, the tempo of the music.
- Art-Net / sACN: DMX over the network instead of the USB dongle.`,

  "help.page.live": `# Control
Turn fixtures on and off and pick colours here.
1. Click a fixture on the stage to select it (Ctrl/Shift = more). Nothing selected = all fixtures.
2. Click a colour. With the checkbox at the colours, a fixture that is off turns on too.
3. Scenes (top): click to recall, "Save" stores the current light. ⋯ changes name, colour and transition time; drag to reorder.
4. Groups: make groups of fixtures to select them in one go.
5. Transition time: how smoothly colours and scenes fade into each other.
# Tips
- Drag fixtures on the stage to match the real setup.
- "Examples" next to the scenes makes a set of scenes for your fixtures in one go.`,

  "help.page.effects": `# Effects
Pick an effect: it runs at once. Click again to stop.
1. Choose "For" all fixtures or one group: e.g. a rainbow at the back and steady white at the front.
2. Set colours and speed on the right. Grey settings are not used by this effect.
3. Effects with ♪ react to music: turn on listening or tap the tempo on Music.
# Own chases
- Choose "Own chases" and "New chase" to make your own chase or pattern.
- Your chases then appear among the effects, on the timeline, in scenes and in Companion.
# Follow the screen
- Choose "Screen" and "Follow the screen": the fixtures take over the PC screen's colours (like ambilight). Set spread, colour boost and calmness on the right. "Edges (ambilight)": fixtures around the TV take the colour of the nearest edge.
- Source "Camera on a TV": a webcam looks at a TV; drag the frame onto the corners of the TV picture. So the fixtures also follow a TV box or Netflix app.`,

  "help.page.timeline": `# Timeline
Put scenes and effects in a sequence, optionally to a song.
1. Make a timeline (+). Drag scenes to a scene track and effects to an effect track.
2. Drag blocks to move them; drag the edges to make them longer or shorter.
3. Add music: the timeline then follows the song exactly.
4. Start and end points and wait points: at a wait point the timeline stops until you press GO.
# Tips
- Turn on "Snap to beat" with the right BPM so blocks snap to the beats.
# Keys (when the timeline is open)
- Space = play/pause · Enter = GO · Home = stop · arrows = previous/next point`,

  "help.page.music": `# Music
Make the light move to the beat.
1. Tap the tempo with TAP (or the space bar), at least 4 times.
2. Or let the app listen: choose the microphone or "this PC's sound" and turn listening on.
3. Choose what the sound does: brightness on volume, bass or the beat; effects stepping on the beat.
4. Below are the sound effects (colours on the bass, VU meter, etc.).
# Tips
- Nothing happens? Check that the bars move. If not, choose another sound device or raise the sensitivity.`,

  "help.page.faders": `# Faders
Set every channel of the selected fixtures separately, like on a lighting desk.
- Select fixtures on Control first. Without a selection you see all fixtures.
- Moving a fader switches the output to "Faders" mode.`,

  "help.page.patch": `# Fixtures
Tell the app which fixtures there are and at which address.
1. Drag a fixture type to the list or click "Add".
2. Set the address and mode exactly as on the fixture's display.
3. Click "Find" to make a fixture flash white.
4. Overlapping addresses show a warning. "Renumber addresses" lines everything up.
# Own fixture type
- Fixture not listed? Make it with "New type" (channels from the manual) or import it from open-fixture-library.org.`,

  "help.page.test": `# Test
Check that everything arrives.
- The DMX monitor shows all 512 channels as they are sent.
- Set channels by hand to try something; "Release" gives them back to the app.
- "Fixture by fixture" lights each fixture in turn: handy for checking addresses.`,

  "help.page.integrations": `# Integrations
Control the light from other devices and programs.
- Links: every action has a link (e.g. start a scene). Paste it into Home Assistant, a Stream Deck or a phone automation.
- WhatsApp: with a phone app like MacroDroid an incoming message can open a link and make the light flash.
- OSC and MIDI: for lighting desks, controllers and other software. With MIDI learn you link a button by pressing it.
- Stream Deck: install the Companion module with one button.
- Home Assistant: over MQTT everything appears by itself (lights, scenes, effects, buttons). See the Home Assistant card.
- OSC: see the OSC card for all addresses with examples.`,

  "help.page.settings": `# Settings
- Language, saving and opening shows, backups.
- Output: dongle status, recovering when it drops out, the log and tips.
- Outputs: Art-Net and sACN for DMX over the network.
- Lock: set a PIN and lock. Others then only see a simple operator view.`,

  "help.page.operator": `# Easy controls
- ON / OFF: fixtures on or off (all fixtures, or the chosen group).
- Blackout: everything dark at once. Press again = back.
- Strobe: hold it down to flash.
- Brightness: the slider controls all fixtures together.
- Groups: choose "All fixtures" or a group at the top; colours and effects then apply only to it.
- Scenes: tap a button for a ready-made look.
- Colours: tap a colour; the fixtures turn on in that colour (an effect on them stops).
- Effects: tap to start, again to stop. The slider makes it faster or slower.
- Music: tap TAP to the beat; effects with ♪ then follow it.
- Timeline: ▶ start, ‖ pause, ■ stop, GO at a wait point.
# Switching
- "All" (top right) opens the full app. Its "Easy" button on the left brings you back.
- When the app is locked it says "Unlock": enter the PIN.
- On a phone: add the page to your home screen for an app icon (see Settings > Phone or tablet).`,

  "fxcat.own": "Own and screen",
  "fx.panelHelp": "This panel belongs to the chosen effect. Grey settings are not used by this effect. Changes show on the fixtures right away while the effect runs.",

  "ch.intro": "Make your own chases and patterns: each row is a step, each column a fixture. Click a chase's pencil to edit it.",
  "ch.new": "New chase",
  "ch.newHint": "Make your own pattern",
  "ch.newName": "My chase {n}",
  "ch.edit": "Edit",
  "ch.panelHint": "This is your own chase. Colours, steps and speed are set in the editor.",
  "ch.name": "Name",
  "ch.notSaved": "not saved",
  "ch.saved": "saved",
  "ch.undo": "Undo (Ctrl+Z)",
  "ch.redo": "Redo (Ctrl+Y)",
  "ch.close": "Close (Esc)",
  "ch.unsaved": "Your changes are not saved. Close anyway?",
  "ch.confirmDelete": "Delete chase \"{name}\"? Where it is used (scenes, timeline) it stops.",
  "ch.copySuffix": "(copy)",
  "ch.duplicated": "Copy made - you are now editing the copy.",
  "ch.running": "Running on the fixtures. Changes now show right away.",
  "ch.help.title": "How it works",
  "ch.help.1": "Each row is a step, each column a fixture. Steps play from top to bottom.",
  "ch.help.2": "Pick a colour (brush) and click or drag over the cells. Right-click or ✕ turns a cell off. Clicking the same colour again turns it off too.",
  "ch.help.3": "With more fixtures than columns the pattern repeats (or choose stretch or mirror under Spread).",
  "ch.help.4": "The preview at the top runs without the fixtures. The step that plays now lights up in the grid.",
  "ch.help.5": "Start quickly from an example under 'Start from', then adjust it.",
  "ch.help.6": "Done? 'On the fixtures' saves and starts the chase. It then appears under Effects > Own chases and can be used in scenes, on the timeline and in Companion.",
  "ch.play": "Play preview",
  "ch.pause": "Pause preview",
  "ch.previewHint": "Preview (the fixtures don't follow)",
  "ch.previewInfo": "Preview on {lamps} fixtures · {steps} steps · one round: {cycle}",
  "ch.onBeat": "on the beat",
  "ch.played": "{n} steps per round",
  "ch.brush": "Brush",
  "ch.eraser": "Off (eraser)",
  "ch.ownColor": "Pick your own colour",
  "ch.second": "Colour B",
  "ch.secondHint": "Second colour for examples like 'two colours' and for the background. Right-click a colour in the row to make it colour B.",
  "ch.brushHelp": "Click a colour and paint in the grid (click or drag). Right-click in the grid = off. Right-click a colour here = it becomes colour B.",
  "ch.time": "Time ms",
  "ch.level": "Level %",
  "ch.fade": "Fade %",
  "ch.timeHint": "Own duration of this step in milliseconds (empty = the general step time)",
  "ch.levelHint": "Brightness of this step (0-100%)",
  "ch.fadeHint": "Own fade into the next step (empty = the general value)",
  "ch.up": "Step up",
  "ch.down": "Step down",
  "ch.fillRow": "Whole row in the brush colour",
  "ch.dupStep": "Duplicate step",
  "ch.delStep": "Delete step",
  "ch.addStep": "Step",
  "ch.addEmpty": "Empty step",
  "ch.addHint": "'Step' copies the last step one fixture further - handy for chases.",
  "ch.tools": "Tools",
  "ch.toolsHelp": "Changes all steps at once. Everything can be undone with Ctrl+Z.",
  "ch.shift": "Shift",
  "ch.shiftHint": "All steps one fixture left/right (wrapping around)",
  "ch.mirror": "Mirror",
  "ch.reverseSteps": "Reverse steps",
  "ch.invert": "Invert (on ↔ off)",
  "ch.recolor": "All in brush colour",
  "ch.move": "Make it run",
  "ch.moveHint": "Makes copies of every step, each shifted one fixture: a fixed pattern starts moving",
  "ch.clear": "Clear all",
  "ch.templates": "Start from",
  "ch.templatesHelp": "Replaces the steps with an example in the brush colour (and colour B). The number of columns stays. You can change everything afterwards.",
  "ch.replaceConfirm": "This replaces the current steps. Continue? (Ctrl+Z undoes it)",
  "ch.tpl.runner": "Chase",
  "ch.tpl.bounce": "Back and forth",
  "ch.tpl.alternate": "Alternate",
  "ch.tpl.buildUp": "Build up",
  "ch.tpl.buildDown": "Build down",
  "ch.tpl.outsideIn": "Outside in",
  "ch.tpl.colorCycle": "Colour cycle",
  "ch.tpl.rainbow": "Rainbow",
  "ch.tpl.random": "Random",
  "ch.tpl.blink": "Blink",
  "ch.tpl.comet": "Comet",
  "ch.tpl.twoColors": "Two colours",
  "ch.columns": "Columns",
  "ch.columnsHelp": "The number of fixtures in the pattern. Set it to your number of fixtures for full control, or smaller for a repeating pattern.",
  "ch.asLamps": "= {n} fixtures",
  "ch.sync": "To the beat of the music",
  "ch.syncHelp": "The chase steps on the beat instead of by time. Works with TAP (tap tempo) or listening (on the right, under Sound).",
  "ch.beatsPerStep": "Beats per step",
  "ch.syncNoBeat": "No tempo yet: tap the tempo or turn on listening on Music. The preview uses 120 BPM for now.",
  "ch.stepTime": "Step time",
  "ch.stepTimeHelp": "How long each step lasts. You can override it per step in the 'Time' column.",
  "ch.speed": "Speed",
  "ch.speedHelp": "Makes the whole chase faster or slower without changing the steps (100% = as designed).",
  "ch.fadeAll": "Fade",
  "ch.fadeHelp": "0% = hard steps. Higher = each step fades into the next at its end. 100% = continuously smooth.",
  "ch.direction": "Direction",
  "ch.directionHelp": "Forward, reverse, back and forth (1-2-3-2-1) or in random order (different every round, never the same step twice in a row).",
  "ch.dir.forward": "Forward",
  "ch.dir.reverse": "Reverse",
  "ch.dir.bounce": "Back & forth",
  "ch.dir.random": "Random",
  "ch.mapping": "Spread over fixtures",
  "ch.mappingHelp": "How the columns are spread over your fixtures when there are more fixtures than columns.",
  "ch.map.repeat": "Repeat",
  "ch.map.stretch": "Stretch",
  "ch.map.mirror": "Mirror",
  "ch.map.once": "Once",
  "ch.map.repeat.hint": "The pattern repeats: 1 2 3 4 1 2 3 4 ...",
  "ch.map.stretch.hint": "Each column covers several neighbouring fixtures: 1 1 2 2 3 3 4 4",
  "ch.map.mirror.hint": "From both sides towards the middle: 1 2 3 4 4 3 2 1",
  "ch.map.once.hint": "Only the first fixtures take part, the rest stays off.",
  "ch.wave": "Wave",
  "ch.waveHelp": "Each repeat of the pattern runs this many steps later (or earlier when negative). Makes waves over many fixtures.",
  "ch.steps": "steps",
  "ch.background": "Background colour",
  "ch.backgroundHelp": "Empty cells get this colour instead of off. For example: a blue background with a white chase.",
  "ch.audio": "Brightness follows the sound",
  "ch.audioHelp": "The louder the music, the brighter. Works when listening is on (on the right, under Sound).",
  "ch.runOn": "On",
  "ch.runNow": "On the fixtures",
  "ch.update": "Update on the fixtures",
  "ch.liveHint": "This chase is running: every change reaches the fixtures a moment later.",
  "ch.save": "Save",
  "ch.savedMsg": "Saved.",
  "ch.duplicate": "Make a copy",
  "ch.delete": "Delete",

  "usb.powerButton": "Turn off USB power saving",
  "usb.powerHelp": "Windows sometimes puts USB ports to sleep to save power; the dongle then drops out for a moment. This button turns that off for the dongle and the USB port it is in (and in the power plan), and restarts the dongle. Windows asks for administrator rights. Do it once per PC (and again if you use another USB port).",
  "usb.powerConfirm": "Turn off USB power saving for the dongle? Windows will ask for administrator rights. The light may stutter for a moment.",
  "usb.powerBusy": "Working... confirm the Windows prompt.",
  "usb.power.ok": "Done: Windows no longer puts the dongle's USB port to sleep.",
  "usb.power.cancelled": "Cancelled: nothing was changed.",
  "usb.power.notfound": "No dongle found. Plug it in and try again.",
  "usb.power.failed": "That did not work: {error}",
  "tip.usb2": "Use a USB 2.0 port (usually black) or a powered USB 2.0 hub. The uDMX is sometimes unreliable on USB 3 ports (blue).",
  "report.title": "Problem report",
  "report.help": "The report contains: the app's log, any crash reports, the Windows and app version, how Windows sees the dongle (driver, USB port, power saving, when it was plugged in and out) and the show. The PIN and the link key are removed.",
  "report.hint": "Something went wrong (dongle dropped out, crash, fixtures act strangely)? Make a report and send the file to whoever helps you (mail, WhatsApp). Preferably right after it happened.",
  "report.crashes": "The app crashed {n} times (last: {when}). Make a report and send it.",
  "report.note": "What happened? (optional)",
  "report.notePlaceholder": "E.g.: at 21:15 the dongle dropped out during the Rainbow effect, fixtures froze.",
  "report.make": "Make report",
  "report.busy": "Working (up to 30 s)...",
  "report.download": "Download",
  "report.done": "Done. The file is on the desktop and the folder is open. Send this file:",
  "easy.button": "Easy",
  "easy.switchHint": "Easy controls with big buttons (also for phones)",
  "easy.full": "All",
  "easy.noDongle": "No dongle",
  "easy.all": "All fixtures",
  "easy.slow": "Slow",
  "easy.fast": "Fast",
  "easy.speed": "Effect speed",
  "easy.tapHint": "Tap at least 4 times to the beat; effects with ♪ then follow it.",
  "easy.tlPlaying": "Playing: {name}",
  "easy.settingsTitle": "Easy controls",
  "easy.settingsHelp": "Easy controls have big buttons for phones and tablets, and are also what others see when the app is locked. On/off, brightness, blackout and scenes are always there; choose what else to show here.",
  "easy.settingsHint": "Shown besides on/off, brightness and scenes:",
  "easy.show.colors": "Colours",
  "easy.show.effects": "Effects (and own chases)",
  "easy.show.groups": "Choose groups",
  "easy.show.music": "Music: TAP and BPM",
  "easy.show.timelines": "Start and stop timelines",
  "easy.show.strobe": "Strobe button",
  "easy.openHere": "Open easy controls",

  "phone.title": "Phone or tablet",
  "phone.help": "You control the light from the browser on your phone or tablet (Safari, Chrome). Nothing needs installing. Phone and PC must be on the same Wi-Fi network. Phones open the easy controls; 'All' gives the full app.",
  "phone.enable": "Control from phone or tablet",
  "phone.offHint": "Turn this on and a QR code to scan appears here.",
  "phone.starting": "Turning on... one moment.",
  "phone.noNetwork": "This PC has no network connection. Connect it to the same Wi-Fi network as the phone.",
  "phone.scan": "Scan this code with your phone's camera (or type the address in the browser):",
  "phone.otherNetwork": "Other network of this PC",
  "phone.connected": "Connected: {list}",
  "phone.noneYet": "No phone or tablet connected yet.",
  "phone.fwOk": "Windows Firewall lets phones in.",
  "phone.fwMissing": "Can't connect? Windows Firewall is probably blocking it.",
  "phone.fwButton": "Open firewall",
  "phone.fwHelp": "Adds a Windows Firewall rule that only lets devices from your own network in, on DMX Light Control's port. Windows asks for administrator rights. Did Windows Firewall already show a message when you turned this on? Choose 'Allow' there.",
  "phone.fwBusy": "Confirm the Windows prompt...",
  "phone.fw.ok": "Done: phones on your network can connect now.",
  "phone.fw.cancelled": "Cancelled: nothing was changed.",
  "phone.fw.failed": "That did not work: {error}",
  "phone.iphone.1": "Make sure the iPhone is on the same Wi-Fi network as this PC.",
  "phone.iphone.2": "Open the Camera and point it at the QR code. Tap the yellow link: Safari opens DMX Light Control.",
  "phone.iphone.3": "As an app on your home screen: in Safari tap Share (square with arrow) > 'Add to Home Screen' > Add.",
  "phone.iphone.4": "From then on open the DMX Light Control icon. Stopped working? Check that the app runs on the PC and whether the address above changed.",
  "phone.android.1": "Make sure the phone is on the same Wi-Fi network as this PC.",
  "phone.android.2": "Open the Camera (or Google Lens) and point it at the QR code. Tap the link: Chrome opens DMX Light Control.",
  "phone.android.3": "As an app on your home screen: in Chrome tap ⋮ > 'Add to Home screen' (or 'Install app').",
  "phone.android.4": "From then on open the DMX Light Control icon. Stopped working? Check that the app runs on the PC and whether the address above changed.",
  "phone.trouble": "Not working? Guest Wi-Fi and some hotspots don't let devices talk to each other: use the normal network. The address can change when the PC reconnects; give the PC a fixed address in the router.",

  "int.phoneAuto": "Phone: buttons, Siri and automatic",
  "int.phoneAutoHelp": "Every action has its own link. When the phone opens that link (from a button, Siri or an automation), DMX Light Control does the action. The key in the link makes sure only your devices can.",
  "int.phoneAutoHint": "Choose what should happen; the link below does exactly that. Use it in the Shortcuts app (iPhone) or MacroDroid (Android).",
  "int.whatShould": "What should happen?",
  "int.qrTry": "Try it: scan the code with the phone and open the link.",
  "int.act.scene": "Scene: {name}",
  "int.act.flash": "Flash green (5 s), e.g. for a notification",
  "int.act.flashRed": "Flash red (10 s)",
  "int.act.on": "All fixtures on",
  "int.act.off": "All fixtures off",
  "int.act.blackout": "Blackout on/off",
  "int.act.color": "Everything blue",
  "int.act.effect": "Effect: rainbow",
  "int.act.stop": "Stop effects",
  "int.act.timeline": "Start timeline",
  "int.androidTitle": "Android: MacroDroid (also on WhatsApp messages)",
  "int.wa4": "Paste the link from above.",
  "int.wa5": "Phone and PC must be on the same Wi-Fi network. Test the macro with the 'Test' button in MacroDroid.",
  "int.androidMore": "Other triggers in MacroDroid: a home-screen button (shortcut), a time, joining a Wi-Fi network, an NFC tag, or an SMS.",
  "int.iosTitle": "iPhone: a button or Siri (Shortcuts app)",
  "int.ios1": "Open the Shortcuts app (it comes with the iPhone) and tap + to make a new shortcut.",
  "int.ios2": "Tap 'Add Action', search for 'URL' and choose 'Get Contents of URL'.",
  "int.ios3": "Tap 'URL' in the action and paste the link from above. Give the shortcut a name at the top, e.g. 'Light red'.",
  "int.ios4": "Tap ▶ to test. The first time the iPhone asks for access to the local network: choose 'Allow'.",
  "int.ios5": "Use it: tap the shortcut, put it on your home screen as a widget or icon (Share > Add to Home Screen), or say 'Hey Siri, light red'.",
  "int.iosAutoTitle": "iPhone: automatic (e.g. on a message)",
  "int.iosAuto1": "In Shortcuts: 'Automation' tab > + (or 'New Automation').",
  "int.iosAuto2": "Choose 'Message' (iOS 17 or later): on a message from a certain person, or containing certain text. Works for iMessage and SMS.",
  "int.iosAuto3": "Choose 'Run Immediately' (and turn off 'Notify When Run' if you don't want a notification).",
  "int.iosAuto4": "Add the action 'Get Contents of URL' with the link from above, or 'Run Shortcut' with your shortcut from the previous block.",
  "int.iosTriggers": "Other automatic triggers on the iPhone: a time of day, an alarm, arriving at or leaving a place, joining a Wi-Fi network, an NFC tag (tap your phone on it), a Focus mode, or opening an app.",
  "int.iosWhatsapp": "WhatsApp on iPhone: Apple does not let apps react to WhatsApp messages. What does work: have senders send an iMessage or SMS (trigger 'Message'), or use an Android phone with MacroDroid to receive the notifications.",
  "easy.moreColors": "More colours ({n})",
  "easy.lessColors": "Fewer colours",
  "nav.schedule": "Schedule",
  "sch.title": "Schedule",
  "sch.help": "Let the light do things by itself at set times: e.g. on at sunset every evening, off at 23:30, or a scene at 08:00 on weekdays. The app (and the PC) must be running.",
  "sch.enabled": "Schedule active",
  "sch.disabledNote": "The schedule is off: nothing happens automatically.",
  "sch.empty": "Nothing planned yet. Add something, or pick an example below.",
  "sch.itemOn": "On/off for this item",
  "sch.namePlaceholder": "Name (optional)",
  "sch.test": "Run now",
  "sch.testHint": "Runs the action right away, to try it",
  "sch.ran": "done",
  "sch.kind.time": "Time",
  "sch.kind.sunset": "Sunset",
  "sch.kind.sunrise": "Sunrise",
  "sch.minutes": "minutes (minus = before)",
  "sch.once": "Once, on",
  "sch.everyDay": "every day",
  "sch.day.1": "Mon", "sch.day.2": "Tue", "sch.day.3": "Wed", "sch.day.4": "Thu", "sch.day.5": "Fri", "sch.day.6": "Sat", "sch.day.7": "Sun",
  "sch.then": "Then:",
  "sch.custom": "Own command...",
  "sch.commandOk": "Command is valid.",
  "sch.grp.basic": "General",
  "sch.act.blackoutOn": "Blackout on",
  "sch.act.blackoutOff": "Blackout off",
  "sch.act.master": "Brightness {n}%",
  "sch.act.tlPlay": "Start timeline: {name}",
  "sch.act.tlStop": "Stop timeline",
  "sch.add": "Add",
  "sch.examples": "Examples:",
  "sch.ex.sunset": "On at sunset",
  "sch.ex.sunsetName": "Evening: on",
  "sch.ex.night": "Off at 23:30",
  "sch.ex.nightName": "Night: off",
  "sch.ex.work": "Weekdays 08:00 on",
  "sch.ex.workName": "Morning: on",
  "sch.upcoming": "Coming up",
  "sch.nothing": "Nothing planned.",
  "sch.last": "Last: {when} - {what} ({result})",
  "sch.sun": "Sun",
  "sch.sunToday": "Today: rises {rise}, sets {set}",
  "sch.place": "Place (for sunrise/sunset)",
  "sch.placeOther": "Other (see coordinates)",
  "sch.lat": "Latitude",
  "sch.lon": "Longitude",
  "sch.myPlace": "Use my location",
  "sch.pcOn": "The schedule only works while DMX Light Control runs. Turn on 'keep PC awake' in Settings. If the PC was asleep, only what should have happened in the last 2 minutes still runs.",

  "fx.screen": "Follow the screen",
  "fxcat.screen": "Screen",
  "ss.hint": "The fixtures take over the PC screen's colours, left to right (like ambilight). Nice with a film, a presentation or visuals.",
  "ss.previewHint": "What the app sees on the screen now",
  "ss.notRunning": "No picture coming in (yet). Start the effect, or choose another screen.",
  "ss.mode": "Spread",
  "ss.modeHelp": "Columns: the screen is spread left to right over the fixtures (the fixture order on Fixtures counts). Average: all fixtures get the average colour of the whole screen.",
  "ss.zones": "Columns (left → right)",
  "ss.average": "Average colour",
  "ss.monitor": "Screen",
  "ss.saturation": "Colour boost: {n}%",
  "ss.smoothing": "Calmer: {n}%",
  "ss.brightness": "Dark picture = dark fixtures",
  "ss.mirror": "Swap left and right (fixtures behind the screen)",
  "ss.drm": "Note: video from streaming services (Netflix etc.) in the browser can come through black because of copy protection.",

  "ha.status.off": "off",
  "ha.status.connecting": "connecting...",
  "ha.status.connected": "connected",
  "ha.status.error": "not connected",
  "ha.help": "Over MQTT, DMX Light Control appears in Home Assistant by itself as a device with lights, scenes, effects and buttons. So it works in automations, dashboards and voice (Google, Alexa, Assist) through Home Assistant.",
  "ha.intro": "Control the light from Home Assistant: everything appears by itself (MQTT discovery).",
  "ha.enable": "Connect to Home Assistant (MQTT)",
  "ha.host": "Address of Home Assistant / MQTT broker",
  "ha.user": "User name",
  "ha.password": "Password",
  "ha.passwordSaved": "•••••• (saved)",
  "ha.advanced": "Advanced",
  "ha.name": "Device name",
  "ha.prefix": "Topic prefix",
  "ha.discovery": "Discovery prefix",
  "ha.stepsTitle": "How to connect",
  "ha.step1": "In Home Assistant: Settings > Add-ons > Add-on store > install and start 'Mosquitto broker'.",
  "ha.step2": "Settings > Devices & services: add 'MQTT' (usually discovered by itself) and confirm.",
  "ha.step3": "Make a user for DMX Light Control: Settings > People > Users > Add user (e.g. 'dmxlicht').",
  "ha.step4": "Enter Home Assistant's address above (e.g. homeassistant.local or 192.168.1.50), port 1883, and that user with password. Turn 'Connect' on.",
  "ha.step5": "Under Devices & services > MQTT you now find the device 'DMX Light Control' with all entities.",
  "ha.whatTitle": "What you get in Home Assistant",
  "ha.what1": "Light 'Licht': on/off, brightness, colour and effect (also your own chases and Follow the screen).",
  "ha.what2": "A light per group (on/off and colour).",
  "ha.what3": "Selects 'Scène' and 'Effect', slider 'Helderheid', switches 'Blackout' and 'Strobo'.",
  "ha.what4": "Buttons: stop effects, timeline start/GO, timeline stop, tap tempo.",
  "ha.what5": "Sensors: dongle connected, BPM.",
  "ha.what6": "Text field 'Opdracht': any command of the command language (see Commands), e.g. 'flash green 5'.",
  "ha.exampleTitle": "Examples for automations",
  "ha.exampleHint": "In an automation or script (YAML mode). Entity names may differ slightly on your system.",
  "ha.ex1": "Choose a scene",
  "ha.ex2": "Send a command (e.g. on the doorbell)",
  "ha.ex3": "All fixtures orange at 60%",

  "osc.help": "OSC (Open Sound Control) is how apps, lighting desks and show software send each other messages over the network. Each message has an address (like /dmxlicht/scene/1) and optional values.",
  "osc.whatTitle": "What is OSC?",
  "osc.what": "With OSC you control DMX Light Control from apps like TouchOSC (phone/tablet), QLab, Bitfocus Companion, Resolume, Open Stage Control or Max. Each message is an address plus values. The address is simply a command from the command language, with / in between.",
  "osc.setupTitle": "Setting up",
  "osc.setup1": "Turn OSC on above (default port 8000).",
  "osc.setup2": "In the other app set the target: host {host}, port {port}, protocol UDP. If that app runs on this PC, use 127.0.0.1.",
  "osc.setup3": "Nothing arrives? Settings > Phone or tablet > 'Open firewall' also opens the OSC port (only for your own network).",
  "osc.setup4": "Check: 'last received' above shows the latest message.",
  "osc.rulesTitle": "How an address works",
  "osc.rule1": "/dmxlicht/ in front is allowed but optional: /scene/1 works too.",
  "osc.rule2": "Parts of the command go in the address (/color/red) or as a value (/color with the text \"red\"); both are fine.",
  "osc.rule3": "Buttons: value 1 = press, 0 = release. For scene, effect, colour, flash, tap, on/off and timeline the release is ignored, so a normal button works well.",
  "osc.rule4": "Faders: /dmxlicht/master with a float 0.0-1.0 (0 to 100%), or a whole number 0-100.",
  "osc.rule5": "Send names with spaces as a text value: /dmxlicht/scene with \"Red light\". Scenes also work by number: /scene/2 = the second scene.",
  "osc.examplesTitle": "Examples",
  "osc.address": "Address",
  "osc.args": "Value(s)",
  "osc.does": "Does",
  "osc.ex.scenePos": "First scene",
  "osc.ex.sceneName": "Scene by name",
  "osc.ex.blackout": "Blackout on / off / toggle",
  "osc.ex.masterFloat": "Brightness from a fader (0.0-1.0)",
  "osc.ex.masterInt": "Brightness in percent",
  "osc.ex.on": "All fixtures on",
  "osc.ex.offGroup": "A group off",
  "osc.ex.color": "Everything red",
  "osc.ex.colorHex": "A group in a custom colour",
  "osc.ex.effect": "Fire effect on all fixtures",
  "osc.ex.effectGroup": "Effect on one group",
  "osc.ex.chase": "Your own chase",
  "osc.ex.screen": "Follow the screen",
  "osc.ex.stop": "Stop effects",
  "osc.ex.strobe": "Strobe on (1) / off (0): ideal for a button you hold down",
  "osc.ex.flash": "Flash green for 5 seconds",
  "osc.ex.flashSolid": "Solid red for 10 seconds",
  "osc.ex.tap": "Tap the tempo",
  "osc.ex.bpm": "Set the tempo",
  "osc.ex.fade": "Transition time 2.5 s",
  "osc.ex.tlPlay": "Start a timeline",
  "osc.ex.tlGo": "GO (continue after a wait point)",
  "osc.ex.tlPause": "Pause the timeline",
  "osc.ex.tlStop": "Stop the timeline",
  "osc.ex.tlNext": "To the next point",
  "osc.ex.tlSeek": "To 30 seconds",
  "osc.ex.recover": "Recover the USB connection",
  "osc.appsTitle": "Tips per app",
  "osc.app1": "TouchOSC: give a button the OSC address (e.g. /dmxlicht/scene/1); it sends 1 on press and 0 on release by itself. A fader on /dmxlicht/master sends 0.0-1.0.",
  "osc.app2": "QLab: Network cue, type OSC, destination = this PC and port; message e.g. /dmxlicht/scene \"Break\".",
  "osc.app3": "Bitfocus Companion: preferably use the DMX Light Control module (Integrations > Stream Deck); the generic OSC module also works.",
  "osc.app4": "Resolume / Open Stage Control / Max: send to the same host and port; the addresses are the same.",
  "osc.names": "All effect names and colours",
  "osc.namesHint": "Use these names after /dmxlicht/effect/ (hover for the full name). Your own chases go by their name.",
  "osc.colorNames": "Colours (Dutch names and #RRGGBB work too)",

  "help.page.schedule": `# Schedule
Let the light do things by itself at set moments.
1. Click 'Add' (or pick an example).
2. Choose when: a time, or sunset/sunrise with minutes before or after.
3. Choose the days (all on = every day), or 'Once' on a date.
4. Choose what should happen: on, off, a scene, an effect, a timeline... or your own command.
5. 'Fade over': let the light change very gradually, e.g. 30 minutes for an alarm (sunrise).
6. 'Run now' tries it right away.
# Good to know
- On the right you see what happens in the coming days.
- For sunrise/sunset: choose your place.
- The app must be running. Turn on 'keep PC awake' in Settings.`,
  "cam.source": "Source",
  "cam.sourceHelp": "This PC's screen: what is on this PC's screen. Camera on a TV: a webcam looks at a TV (or projector screen); you mark with a frame where the TV is in the picture. So it also works with a TV box, Netflix app or game console.",
  "cam.fromScreen": "This PC's screen",
  "cam.fromCamera": "Camera on a TV",
  "cam.hint": "A webcam looks at the TV; only what is inside the frame counts. The colours go left to right over the fixtures, just like with the PC screen.",
  "cam.device": "Webcam",
  "cam.auto": "Automatic (first real webcam)",
  "cam.none": "No webcam found. Connect a USB webcam.",
  "cam.onlyHere": "The camera picture (and the frame) can only be seen on this PC, not on a phone or tablet.",
  "cam.starting": "Starting webcam...",
  "cam.tl": "Top left", "cam.tr": "Top right", "cam.br": "Bottom right", "cam.bl": "Bottom left",
  "cam.reset": "Reset frame",
  "cam.whole": "Whole picture",
  "cam.dragHint": "Drag dots 1-4 onto the corners of the TV (1 = top left, clockwise).",
  "cam.step1": "Put the webcam down firmly with the whole TV in view (straight in front works best, at an angle is fine).",
  "cam.step2": "Drag the four corners of the frame onto the corners of the TV picture (not the TV's bezel).",
  "cam.step3": "Start Follow the screen. At the top you see which colours the app takes from the TV.",
  "cam.step4": "Too dull or too busy? Raise 'Colour boost' and 'Calmer' a bit. Trouble in a dark room: turn off the webcam's automatic exposure in the Windows Camera app.",
  "edge.mode": "Edges (ambilight)",
  "edge.hint": "Real ambilight: each fixture takes the colour of the edge of the picture nearest to it. The dots are your {n} fixtures in the order of the list on Fixtures.",
  "edge.auto": "Spread the fixtures over left, top and right by themselves",
  "edge.edgeLeft": "Left",
  "edge.edgeTop": "Top",
  "edge.edgeRight": "Right",
  "edge.edgeBottom": "Bottom",
  "edge.depth": "Edge depth: {n}%",
  "edge.reverse": "Anti-clockwise",
  "edge.start": "Shift first fixture",
  "edge.order": "Order: left from bottom to top, then top from left to right, right from top to bottom, bottom from right to left. Doesn't match your setup? Use 'Anti-clockwise' and 'Shift first fixture', or reorder the fixtures on Fixtures.",
  "edge.bars": "Skip black bars (films)",
  "sch.fade": "Fade over",
  "sch.seconds": "seconds",
  "sch.minutesUnit": "minutes",
  "sch.fadeHint": "the light moves very gradually to the new look",
  "sch.fadeNone": "0 = the normal transition time",
  "sch.ex.wake": "Alarm: sunrise",
  "sch.ex.wakeName": "Alarm (30 min brighter)",
  "sch.ex.dim": "Slowly off at 23:00",
  "sch.ex.dimName": "Evening: slowly off",
  "cam.findTv": "Find TV automatically",
  "cam.searching": "Searching...",
  "cam.found": "Found. Check the corners and adjust if needed.",
  "cam.notFound": "No TV found. Put something bright on the TV (e.g. a white or colourful picture), make the room a bit darker and try again.",
  "cam.keysHint": "Click a dot and use the arrow keys to move it precisely (Shift = bigger steps).",
  "cam.keyV": "Vertical keystone",
  "cam.keyH": "Horizontal keystone",
  "cam.rotate": "Rotate",
  "cam.size": "Size",
  "cam.moveX": "Left / right",
  "cam.moveY": "Up / down",
  "pair.title": "Pair this phone",
  "pair.hint": "This device may not control the light yet. Enter the pairing code shown on the PC.",
  "pair.where": "The code is on the PC under Settings > Phone or tablet. Scanning the QR code there pairs automatically.",
  "pair.button": "Pair",
  "pair.wrong": "This code is not (or no longer) valid. Check the PC for the current code.",
  "pair.tooMany": "Too many attempts. Wait a minute.",
  "phone.fixedName": "Fixed name on the network",
  "phone.fixedNameOff": "fixed name not active",
  "phone.fixedNameHelp": "With the fixed name (e.g. dmxlicht.local) the address on your phone keeps working, even when the PC's IP address changes. Works on iPhone, iPad, Mac, Windows and most Android phones (Android 12+). Not working? Choose the IP address.",
  "phone.qrName": "QR with fixed name",
  "phone.qrIp": "QR with IP address",
  "phone.pairing": "Pair phones first",
  "phone.pairingHelp": "Only paired phones and tablets may control the light. Pairing happens automatically with the QR code, or with the 6-digit code. Each code works once. Trigger links (with key), Companion, OSC and Home Assistant keep working.",
  "phone.code": "Pairing code",
  "phone.newCode": "New code",
  "phone.noPairingWarn": "Anyone on this Wi-Fi network can now control the light.",
  "phone.devices": "Paired devices",
  "phone.lastSeen": "last {when}",
  "phone.remove": "Remove",
  "patch.universeCol": "Universe",
  "patch.universeHelp": "One DMX cable has 512 channels: one 'universe'. With more fixtures (or more cables / Art-Net nodes), put them in universe 2, 3, ... Each universe has addresses 1 to 512 again. 'Renumber addresses' moves on to the next universe when one is full.",
  "patch.universeN": "Universe {n}",
  "out.udmxUniverse": "USB dongle sends",
  "out.multiHint": "This show uses {n} universes. Art-Net and sACN send all of them: the first on the number above, the next ones on +1, +2, ...",
  "out.multiHelp": "Example: Art-Net universe 0 above means show universe 1 on Art-Net 0, universe 2 on Art-Net 1, etc. The USB dongle has one cable and so sends one universe; choose which above.",
  "upd.title": "Updates",
  "upd.help": "New versions come from GitHub (github.com/inkvolcano/dmx-light-control). Updating downloads the new version, verifies it (checksum) and the app replaces itself, then restarts. Your show and settings (the Instellingen folder) stay.",
  "upd.current": "This version: {v}",
  "upd.available": "New version available: {v}",
  "upd.upToDate": "You have the latest version.",
  "upd.install": "Update and restart",
  "upd.installing": "Updating... the app closes and restarts in a moment.",
  "upd.onlyHere": "Updating is only possible on the PC itself.",
  "upd.check": "Check now",
  "upd.lastCheck": "last checked: {when}",
  "upd.auto": "Check for new versions automatically (you install them yourself)",
  "upd.confirm": "Update to version {v}? The app closes briefly and restarts; the light pauses for a few seconds.",
  "upd.chip": "Update {v}",
  "upd.chipHint": "A new version is out: click to go to Settings",
  "nav.screen": "Screen",
  "ss.openPage": "Set up with the gear at Screen and video (on the right)",
  "ss.start": "Start following the screen",
  "ss.lampsNow": "What the lamps do now",
  "ss.isOn": "following the screen",
  "ss.isOff": "off",
  "ss.cardSource": "Picture",
  "ss.cardCamera": "Camera, frame and keystone",
  "ss.cardSpread": "Spread over the lamps",
  "ss.pageHelp": "Here the lamps follow the colours of a screen: this PC's screen, or a TV through a webcam. On the left you pick the source (and for a camera the frame around the TV, with keystone correction), on the right how the colours go over the lamps. Start and stop here or at Effects > Follow the screen.",
  "help.page.screen": `# Screen (ambilight)
The lamps take over the colours of a picture, like ambilight on a TV.

# Source
- This PC's screen: what is on this PC's screen (film, presentation, visuals). With more screens, pick one.
- Camera on a TV: a webcam looks at a TV or projector screen. This also works with a TV box, streaming app or game console.

# Frame and keystone (camera only)
1. Put the webcam down firmly with the whole TV in view.
2. Click "Find TV automatically" (TV on, with a bright picture), or drag dots 1-4 onto the TV's corners.
3. Fine-tune: pick a corner and nudge it with the arrow keys, or use the keystone buttons (vertical, horizontal, rotate, size), like on a projector.
4. The grid shows how the app straightens the picture.

# Spread
- Columns: the left of the picture goes to the left of the row of lamps, and so on.
- Average: all lamps the same colour.
- Edges: lamps around the TV; see the numbered preview.
- Markers: you put a dot on the picture for each lamp; that lamp takes the colour of exactly that spot.
- Calmer softens fast changes; colour boost makes pale pictures brighter.`,
  "pts.mode": "Markers",
  "pts.title": "Markers: where each lamp takes its colour",
  "pts.hint": "Each numbered dot is a lamp ({n} in total, in the order of the Lamps page). Drag it to the spot on the picture where that lamp should take its colour; the small square is the area that is measured. The dot shows what the lamp does now.",
  "pts.dragHint": "Drag a dot, or click one and then click on the picture to move it there.",
  "pts.selectedHint": "Lamp {n} selected: click on the picture to move it, or use the arrow keys (Shift = bigger steps). Esc = let go.",
  "pts.lamp": "Lamp {n}",
  "pts.size": "Measured area: {n}% of the width",
  "pts.reset": "In a row",
  "pts.around": "Around the picture",
  "pts.noPicture": "No picture yet. On this PC the screen or TV shows here; elsewhere the colours once follow-the-screen runs.",
  "pts.onlyHere": "The real picture is only shown on the PC itself; here you see the colours the app measures (once follow-the-screen runs).",
  "pts.below": "You place the markers on the picture below.",
  "tabs.title": "Tabs in the menu",
  "tabs.hint": "Untick what you don't use; it disappears from the menu on the left. Nothing is lost: tick it again and it is back.",
  "tabs.help": "Handy to make the app simpler, for example for a colleague who only needs the controls and effects. This applies to all devices (PC, phone, tablet). Settings always stays visible, otherwise you could not switch the tabs back on.",
  "tabs.always": "always visible",
  "tabs.settingsAlways": "Settings always stays in the menu, so you can always switch tabs back on",
};
