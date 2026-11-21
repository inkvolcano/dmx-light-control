// Hulpteksten en chase-editor (Nederlands). Regels: "# " = kopje, "- " = opsomming, "1. " = genummerd.
export const helpNl: Record<string, string> = {
  "help.button": "Hulp",
  "help.what": "Wat is dit?",
  "help.title": "Hulp en uitleg",
  "help.footer": "Tip: druk F1 voor hulp bij de pagina waar je bent. Achter de ? bij knoppen staat korte uitleg.",
  "help.tab.start": "Snel beginnen",
  "help.tab.trouble": "Problemen oplossen",
  "help.tab.keys": "Sneltoetsen",
  "help.tab.glossary": "Begrippen",
  "nav.operator": "Bediening",

  "help.start.text": `# In 5 stappen licht
1. Steek de USB-dongle (uDMX) in en sluit de DMX-kabel aan op de eerste lamp. Rechtsboven staat "Verbonden" als het goed is.
2. Ga naar Lampen en voeg je lampen toe (slepen of "Toevoegen"). Zet bij elke lamp hetzelfde DMX-adres en dezelfde modus als op het display van de lamp.
3. Klik bij Lampen op "Zoek": de lamp knippert wit, zo weet je welke welke is.
4. Ga naar Bediening: klik lampen aan (of niets = alle lampen) en kies een kleur. Met de schuif bovenin regel je de helderheid.
5. Sla wat je mooi vindt op als scène (knop "Opslaan" bij de scènes). Een scène haal je later met één klik terug.
# Daarna
- Effecten: kies een effect (looplicht, regenboog, politie...) of maak je eigen chase.
- Tijdlijn: zet scenes en effecten achter elkaar, eventueel op muziek.
- Muziek: laat het licht op de maat bewegen (TAP of luisteren via de microfoon).
- Instellingen: PIN-code en vergrendelen voor een eenvoudige bediening voor anderen.
# Belangrijk
- Alles wordt automatisch bewaard in de map Instellingen naast DmxLicht.exe.
- Ctrl+Z maakt bijna alles ongedaan.`,

  "help.trouble.text": `# Lampen doen niets
1. Kijk rechtsboven: staat er "Verbonden met uDMX"? Zo niet, zie hieronder bij de dongle.
2. Staat Blackout aan (bovenin)? Staat de helderheid op 0?
3. Klopt het DMX-adres en de modus van de lamp met wat bij Lampen staat? Kijk op het display van de lamp.
4. Gebruik de pagina Test > "Lamp voor lamp" om elke lamp los te proberen.
5. Zit er een DMX-kabel tussen dongle en de eerste lamp, en van lamp naar lamp (DMX OUT naar DMX IN)?
# De dongle valt weg
1. Klik rechtsboven op de status: dat probeert de verbinding te herstellen.
2. Instellingen > Uitgang > "USB herstellen". Lukt het niet, dan kan Windows de dongle herstarten (vraagt om beheerdersrechten).
3. Instellingen > Uitgang > "USB-stroombesparing uitzetten": Windows zet de USB-poort dan niet meer in slaap. Dit helpt vaak op laptops.
4. Gebruik een USB 2.0-poort (zwart, niet blauw) of een USB 2.0-hub met eigen voeding. De uDMX werkt soms slecht op USB 3-poorten.
5. Laptop aan de lader, korte USB-kabel, geen verlengkabels.
6. Zet een DMX-terminator (eindweerstand 120 Ω) in de laatste lamp. Storing op de DMX-lijn kan de dongle laten resetten.
7. Draai maar één programma met de dongle tegelijk (niet ook de oude v1.4 of QLC+).
8. Blijft het gebeuren? Maak hieronder een probleemrapport (direct nadat het misging) en stuur het zip-bestand op.
# Venster blijft leeg
- Start "DmxLicht.exe --browser": de bediening opent in je gewone browser.
# Iets per ongeluk gewist
- Ctrl+Z (ongedaan maken), of Instellingen > Shows > een automatische back-up terugzetten.`,

  "help.keys.text": `- F1: hulp bij deze pagina
- Ctrl+Z: ongedaan maken · Ctrl+Y: opnieuw
- Spatiebalk (op Muziek): tempo tikken (TAP)
- Klik op lamp: kiezen · Ctrl/Shift+klik: meer lampen kiezen
- Strobo-knop bovenin: ingedrukt houden
- Chase-editor: klik/sleep = kleuren, rechtsklik = uit, Ctrl+Z/Ctrl+Y binnen de editor, Esc = sluiten
- Tijdlijn: spatiebalk = afspelen/pauze, Enter = GO, Home = stop, pijltjes = vorig/volgend punt`,

  "help.glossary.text": `- DMX: het signaal dat lampen aanstuurt. Eén kabel ("universe") heeft 512 kanalen.
- Adres: het eerste kanaal van een lamp. Een lamp met 7 kanalen op adres 1 gebruikt 1 t/m 7; de volgende begint dan op 8.
- Modus: hoeveel kanalen een lamp gebruikt (bijv. 6ch of 10ch). Moet gelijk zijn aan de instelling op de lamp zelf.
- uDMX: het USB-dongeltje dat de pc met de DMX-kabel verbindt.
- Scène: een opgeslagen lichtstand (kleuren, effecten) die je met één klik terugroept.
- Overgangstijd: hoe lang het duurt om naar een nieuwe kleur of scene over te vloeien.
- Effect: een bewegend patroon (looplicht, knipperen, regenboog).
- Chase: een effect dat je zelf maakt, stap voor stap.
- Groep: een aantal lampen samen (bijv. "Voor" en "Achter") om ze apart te bedienen of een eigen effect te geven.
- Master/helderheid: de totale helderheid van alle lampen.
- Blackout: alles direct donker, zonder iets te vergeten.
- Strobo: snel flitsen.
- BPM: tellen per minuut, het tempo van de muziek.
- Art-Net / sACN: DMX via het netwerk in plaats van de USB-dongle.`,

  "help.page.live": `# Bediening
Hier zet je lampen aan en uit en kies je kleuren.
1. Klik een lamp op het podium om hem te kiezen (Ctrl/Shift = meer). Niets gekozen = alle lampen.
2. Klik een kleur. Met het vinkje bij de kleuren gaat een lamp die uit staat meteen aan.
3. Scènes (bovenaan): klik om terug te halen, "Opslaan" bewaart het huidige licht. Met ⋯ verander je naam, kleur en overgangstijd; slepen verandert de volgorde.
4. Groepen: maak groepen van lampen om ze in één keer te kiezen.
5. Overgangstijd: hoe zacht kleuren en scenes in elkaar overlopen.
# Tips
- Sleep lampen op het podium zodat het lijkt op de echte opstelling.
- "Voorbeelden" naast de scènes maakt in één keer een set scènes voor jouw lampen.`,

  "help.page.effects": `# Effecten
Kies een effect: het loopt meteen. Klik nog eens om te stoppen.
1. Kies boven "Voor" alle lampen of één groep: zo kan bijvoorbeeld achter een regenboog lopen en voor vast wit.
2. Rechts stel je kleuren en snelheid in. Grijze instellingen gebruikt dit effect niet.
3. Effecten met ♪ reageren op muziek: zet dan bij Muziek het luisteren aan of tik het tempo.
# Eigen chases
- Kies "Eigen chases" en "Nieuwe chase" om zelf een looplicht of patroon te maken.
- Je eigen chases staan daarna tussen de effecten, ook op de tijdlijn, in scenes en in Companion.
# Scherm volgen
- Kies "Scherm" en "Scherm volgen": de lampen nemen de kleuren van het pc-scherm over (zoals ambilight). Rechts stel je verdeling, kleurversterking en rust in. "Randen (ambilight)": lampen rond de tv nemen de kleur van de dichtstbijzijnde rand.
- Bron "Camera op een tv": een webcam kijkt naar een tv; sleep het kader op de hoeken van het tv-beeld. Zo volgen de lampen ook een tv-kastje of Netflix-app.`,

  "help.page.timeline": `# Tijdlijn
Zet scenes en effecten achter elkaar in de tijd, eventueel op een liedje.
1. Maak een tijdlijn (+). Sleep scenes naar een scènespoor en effecten naar een effectspoor.
2. Sleep blokken om ze te verplaatsen; trek aan de randen om ze langer of korter te maken.
3. Voeg muziek toe: de tijdlijn loopt dan precies mee met het liedje.
4. Start- en eindpunt en wachtpunten: bij een wachtpunt stopt de tijdlijn tot je op GO drukt.
# Tips
- Zet "Op de tel" aan met het juiste BPM, dan klikken blokken vast op de tellen.
# Toetsen (als de tijdlijn open is)
- Spatiebalk = afspelen/pauze · Enter = GO · Home = stop · pijltjes = vorig/volgend punt`,

  "help.page.music": `# Muziek
Laat het licht op de maat bewegen.
1. Tik het tempo met TAP (of de spatiebalk), minstens 4 keer.
2. Of laat de app luisteren: kies de microfoon of "geluid van deze pc" en zet luisteren aan.
3. Kies wat het geluid doet: helderheid op volume, bas of de tel; effecten op de maat laten stappen.
4. Hieronder staan de geluidseffecten (kleuren op de bas, VU-meter, enz.).
# Tips
- Niets te zien? Kijk of de balkjes bewegen. Zo niet: ander geluidsapparaat kiezen of gevoeligheid omhoog.`,

  "help.page.faders": `# Faders
Elk kanaal van de gekozen lampen los regelen, zoals op een lichttafel.
- Kies eerst lampen op Bediening. Zonder keuze zie je alle lampen.
- Faders werken alleen als de modus "Faders" actief is (dat gaat vanzelf als je er een beweegt).`,

  "help.page.patch": `# Lampen
Hier vertel je de app welke lampen er zijn en op welk adres ze staan.
1. Sleep een lamptype naar de lijst of klik "Toevoegen".
2. Stel het adres en de modus in, precies zoals op het display van de lamp.
3. Klik "Zoek" om een lamp wit te laten knipperen.
4. Overlappen adressen, dan zie je een waarschuwing. "Adressen opnieuw nummeren" zet alles netjes achter elkaar.
# Eigen lamptype
- Staat je lamp er niet bij? Maak hem met "Nieuw type" (kanalen uit de handleiding) of importeer hem van open-fixture-library.org.`,

  "help.page.test": `# Test
Controleren of alles aankomt.
- De DMX-monitor laat alle 512 kanalen zien zoals ze verstuurd worden.
- Zet kanalen met de hand om iets uit te proberen; "Loslaten" geeft ze terug aan de app.
- "Lamp voor lamp" laat elke lamp om de beurt branden: handig om adressen te controleren.`,

  "help.page.integrations": `# Koppelingen
Het licht bedienen vanaf andere apparaten en programma's.
- Links: elke actie heeft een link (bijv. scene starten). Plak die in Home Assistant, een Stream Deck of een telefoon-automatisering.
- WhatsApp: met een telefoon-app zoals MacroDroid kan een binnenkomend bericht een link openen, en zo het licht laten knipperen.
- OSC en MIDI: voor lichttafels, controllers en andere software. Met MIDI-leren koppel je een knop door hem in te drukken.
- Stream Deck: installeer de Companion-module met één knop.
- Home Assistant: via MQTT verschijnt alles vanzelf (lampen, scènes, effecten, knoppen). Zie de kaart Home Assistant.
- OSC: zie de kaart OSC voor alle adressen met voorbeelden.`,

  "help.page.settings": `# Instellingen
- Taal, opslaan en openen van shows, back-ups.
- Uitgang: de status van de dongle, herstellen als hij wegvalt, het logboek en tips.
- Uitgangen: Art-Net en sACN voor DMX via het netwerk.
- Vergrendelen: stel een PIN in en vergrendel. Anderen zien dan alleen een eenvoudige bediening.`,

  "help.page.operator": `# Eenvoudige bediening
- AAN / UIT: lampen aan of uit (alle lampen, of de gekozen groep).
- Blackout: alles direct donker. Nog eens drukken = terug.
- Strobo: ingedrukt houden om te flitsen.
- Helderheid: de schuif regelt alle lampen tegelijk.
- Groepen: kies bovenaan "Alle lampen" of een groep; kleuren en effecten gelden dan alleen daarvoor.
- Scènes: tik een knop voor een kant-en-klare lichtstand.
- Kleuren: tik een kleur; de lampen gaan aan in die kleur (een effect op die lampen stopt).
- Effecten: tik om te starten, nog eens om te stoppen. Met de schuif maak je het sneller of langzamer.
- Muziek: tik TAP op de maat; effecten met ♪ lopen dan mee.
- Tijdlijn: ▶ starten, ‖ pauze, ■ stoppen, GO bij een wachtpunt.
# Wisselen
- "Alles" (rechtsboven) opent de volledige app. Daar zit links de knop "Eenvoudig" om terug te gaan.
- Is de app vergrendeld, dan staat er "Ontgrendelen": vul de PIN in.
- Op de telefoon: zet de pagina op je beginscherm voor een eigen app-icoon (zie Instellingen > Telefoon of tablet).`,

  "fxcat.own": "Eigen chases",
  "fx.panelHelp": "Dit paneel hoort bij het gekozen effect. Grijze instellingen gebruikt dit effect niet. Wijzigingen zie je meteen op de lampen als het effect loopt.",

  // ---------------------------------------------------------------- chase-editor
  "ch.intro": "Maak je eigen looplichten en patronen: elke rij is een stap, elke kolom een lamp. Klik op het potloodje van een chase om hem te bewerken.",
  "ch.new": "Nieuwe chase",
  "ch.newHint": "Zelf een patroon maken",
  "ch.newName": "Mijn chase {n}",
  "ch.edit": "Bewerken",
  "ch.panelHint": "Dit is een eigen chase. Kleuren, stappen en snelheid stel je in de editor in.",
  "ch.name": "Naam",
  "ch.notSaved": "niet opgeslagen",
  "ch.saved": "opgeslagen",
  "ch.undo": "Ongedaan maken (Ctrl+Z)",
  "ch.redo": "Opnieuw (Ctrl+Y)",
  "ch.close": "Sluiten (Esc)",
  "ch.unsaved": "Je wijzigingen zijn niet opgeslagen. Toch sluiten?",
  "ch.confirmDelete": "Chase \"{name}\" verwijderen? Waar hij gebruikt wordt (scenes, tijdlijn) stopt hij.",
  "ch.copySuffix": "(kopie)",
  "ch.duplicated": "Kopie gemaakt - je bewerkt nu de kopie.",
  "ch.running": "Loopt op de lampen. Wijzigingen zie je nu meteen.",
  "ch.help.title": "Zo werkt het",
  "ch.help.1": "Elke rij is een stap, elke kolom een lamp. De stappen lopen van boven naar beneden.",
  "ch.help.2": "Kies een kleur (penseel) en klik of sleep over de vakjes. Rechtsklik of ✕ maakt een vakje uit. Nog eens klikken op dezelfde kleur zet hem ook uit.",
  "ch.help.3": "Heb je meer lampen dan kolommen, dan herhaalt het patroon zich (of kies bij Verdeling: uitrekken of spiegelen).",
  "ch.help.4": "Bovenaan zie je een voorbeeld, zonder dat de lampen meedoen. De stap die nu speelt licht op in het raster.",
  "ch.help.5": "Begin snel met een voorbeeld onder 'Beginnen met', en pas het daarna aan.",
  "ch.help.6": "Klaar? 'Op de lampen' slaat op en start de chase. Daarna staat hij bij Effecten > Eigen chases, en is hij te gebruiken in scenes, op de tijdlijn en in Companion.",
  "ch.play": "Voorbeeld afspelen",
  "ch.pause": "Voorbeeld pauzeren",
  "ch.previewHint": "Voorbeeld (de lampen doen niet mee)",
  "ch.previewInfo": "Voorbeeld op {lamps} lampen · {steps} stappen · één ronde: {cycle}",
  "ch.onBeat": "op de maat",
  "ch.played": "{n} stappen per ronde",
  "ch.brush": "Penseel",
  "ch.eraser": "Uit (gum)",
  "ch.ownColor": "Eigen kleur kiezen",
  "ch.second": "Kleur B",
  "ch.secondHint": "Tweede kleur voor voorbeelden zoals 'twee kleuren' en voor de achtergrond. Rechtsklik op een kleur in de rij maakt hem kleur B.",
  "ch.brushHelp": "Klik een kleur en schilder in het raster (klikken of slepen). Rechtsklik in het raster = uit. Rechtsklik op een kleur hier = die kleur wordt kleur B.",
  "ch.time": "Tijd ms",
  "ch.level": "Helder %",
  "ch.fade": "Vloei %",
  "ch.timeHint": "Eigen duur van deze stap in milliseconden (leeg = de algemene staptijd)",
  "ch.levelHint": "Helderheid van deze stap (0-100%)",
  "ch.fadeHint": "Eigen overvloeiing naar de volgende stap (leeg = de algemene waarde)",
  "ch.up": "Stap omhoog",
  "ch.down": "Stap omlaag",
  "ch.fillRow": "Hele rij in de penseelkleur",
  "ch.dupStep": "Stap dupliceren",
  "ch.delStep": "Stap verwijderen",
  "ch.addStep": "Stap",
  "ch.addEmpty": "Lege stap",
  "ch.addHint": "'Stap' kopieert de laatste stap één lamp verder - handig voor looplichten.",
  "ch.tools": "Gereedschap",
  "ch.toolsHelp": "Bewerkt alle stappen tegelijk. Alles is ongedaan te maken met Ctrl+Z.",
  "ch.shift": "Opschuiven",
  "ch.shiftHint": "Alle stappen één lamp naar links/rechts (rondom)",
  "ch.mirror": "Spiegelen",
  "ch.reverseSteps": "Stappen omdraaien",
  "ch.invert": "Omkeren (aan ↔ uit)",
  "ch.recolor": "Alles in penseelkleur",
  "ch.move": "Laten lopen",
  "ch.moveHint": "Maakt van elke stap kopieën die telkens één lamp opschuiven: een vast patroon gaat zo lopen",
  "ch.clear": "Alles wissen",
  "ch.templates": "Beginnen met",
  "ch.templatesHelp": "Vervangt de stappen door een voorbeeld in de penseelkleur (en kleur B). Het aantal kolommen blijft gelijk. Daarna kun je alles aanpassen.",
  "ch.replaceConfirm": "Dit vervangt de huidige stappen. Doorgaan? (Ctrl+Z maakt het ongedaan)",
  "ch.tpl.runner": "Looplicht",
  "ch.tpl.bounce": "Heen en weer",
  "ch.tpl.alternate": "Om en om",
  "ch.tpl.buildUp": "Opbouwen",
  "ch.tpl.buildDown": "Afbouwen",
  "ch.tpl.outsideIn": "Buiten naar binnen",
  "ch.tpl.colorCycle": "Kleurwissel",
  "ch.tpl.rainbow": "Regenboog",
  "ch.tpl.random": "Willekeurig",
  "ch.tpl.blink": "Knipperen",
  "ch.tpl.comet": "Komeet",
  "ch.tpl.twoColors": "Twee kleuren",
  "ch.columns": "Kolommen",
  "ch.columnsHelp": "Het aantal lampen in het patroon. Zet dit gelijk aan je aantal lampen voor volledige controle, of kleiner voor een patroon dat zich herhaalt.",
  "ch.asLamps": "= {n} lampen",
  "ch.sync": "Op de maat van de muziek",
  "ch.syncHelp": "De chase stapt mee op de tel in plaats van op tijd. Werkt met TAP (tempo tikken) of luisteren op de pagina Muziek.",
  "ch.beatsPerStep": "Tellen per stap",
  "ch.syncNoBeat": "Er is nog geen tempo: tik het tempo of zet luisteren aan bij Muziek. Het voorbeeld gebruikt nu 120 BPM.",
  "ch.stepTime": "Staptijd",
  "ch.stepTimeHelp": "Hoe lang elke stap duurt. Per stap kun je dit in de kolom 'Tijd' nog aanpassen.",
  "ch.speed": "Snelheid",
  "ch.speedHelp": "Maakt de hele chase sneller of langzamer zonder de stappen te veranderen (100% = zoals ontworpen).",
  "ch.fadeAll": "Overvloeien",
  "ch.fadeHelp": "0% = harde stappen. Hoger = elke stap vloeit aan het eind zacht over in de volgende. 100% = continu vloeiend.",
  "ch.direction": "Richting",
  "ch.directionHelp": "Vooruit, achteruit, heen en weer (1-2-3-2-1) of in willekeurige volgorde (elke ronde anders, nooit twee keer dezelfde stap achter elkaar).",
  "ch.dir.forward": "Vooruit",
  "ch.dir.reverse": "Achteruit",
  "ch.dir.bounce": "Heen en weer",
  "ch.dir.random": "Willekeurig",
  "ch.mapping": "Verdeling over lampen",
  "ch.mappingHelp": "Hoe de kolommen over je lampen verdeeld worden als je meer lampen dan kolommen hebt.",
  "ch.map.repeat": "Herhalen",
  "ch.map.stretch": "Uitrekken",
  "ch.map.mirror": "Spiegelen",
  "ch.map.once": "Eén keer",
  "ch.map.repeat.hint": "Het patroon herhaalt zich: 1 2 3 4 1 2 3 4 ...",
  "ch.map.stretch.hint": "Elke kolom geldt voor meerdere lampen naast elkaar: 1 1 2 2 3 3 4 4",
  "ch.map.mirror.hint": "Vanaf beide kanten naar het midden: 1 2 3 4 4 3 2 1",
  "ch.map.once.hint": "Alleen de eerste lampen doen mee, de rest blijft uit.",
  "ch.wave": "Golf",
  "ch.waveHelp": "Elke herhaling van het patroon loopt dit aantal stappen later (of eerder bij een min). Zo maak je golven over veel lampen.",
  "ch.steps": "stappen",
  "ch.background": "Achtergrondkleur",
  "ch.backgroundHelp": "Lege vakjes krijgen deze kleur in plaats van uit. Bijvoorbeeld: blauwe achtergrond met een wit looplicht.",
  "ch.audio": "Helderheid op het geluid",
  "ch.audioHelp": "Hoe harder de muziek, hoe feller. Werkt als luisteren aan staat op de pagina Muziek.",
  "ch.runOn": "Op",
  "ch.runNow": "Op de lampen",
  "ch.update": "Bijwerken op de lampen",
  "ch.liveHint": "Deze chase loopt: elke wijziging staat na een moment op de lampen.",
  "ch.save": "Opslaan",
  "ch.savedMsg": "Opgeslagen.",
  "ch.duplicate": "Kopie maken",
  "ch.delete": "Verwijderen",

  // ---------------------------------------------------------------- USB-stroombesparing
  "usb.powerButton": "USB-stroombesparing uitzetten",
  "usb.powerHelp": "Windows zet USB-poorten soms in slaap om stroom te sparen; de dongle valt dan even weg. Deze knop zet dat uit voor de dongle en de USB-poort waar hij in zit (en in het energieplan), en herstart de dongle. Windows vraagt om beheerdersrechten. Eén keer doen per pc (en opnieuw als je een andere USB-poort gebruikt).",
  "usb.powerConfirm": "USB-stroombesparing uitzetten voor de dongle? Windows vraagt zo om beheerdersrechten. Het licht kan heel even haperen.",
  "usb.powerBusy": "Bezig... bevestig de vraag van Windows.",
  "usb.power.ok": "Klaar: Windows zet de USB-poort van de dongle niet meer in slaap.",
  "usb.power.cancelled": "Geannuleerd: er is niets veranderd.",
  "usb.power.notfound": "Geen dongle gevonden. Steek hem in en probeer opnieuw.",
  "usb.power.failed": "Dat lukte niet: {error}",
  "tip.usb2": "Gebruik een USB 2.0-poort (meestal zwart) of een USB 2.0-hub met eigen voeding. De uDMX werkt soms onbetrouwbaar op USB 3-poorten (blauw).",
  "report.title": "Probleemrapport",
  "report.help": "Het rapport bevat: het logboek van de app, eventuele crashmeldingen, de versie van Windows en de app, hoe Windows de dongle ziet (driver, USB-poort, stroombesparing, wanneer hij in- en uitgestoken werd) en de show. De PIN-code en de sleutel van de links worden eruit gehaald.",
  "report.hint": "Gaat er iets mis (dongle valt weg, crash, lampen doen raar)? Maak een rapport en stuur het bestand naar wie je helpt (mail, WhatsApp). Doe het liefst direct nadat het gebeurde.",
  "report.crashes": "De app is {n} keer gecrasht (laatste: {when}). Maak een rapport en stuur het op.",
  "report.note": "Wat gebeurde er? (mag leeg)",
  "report.notePlaceholder": "Bijv.: om 21:15 viel de dongle weg tijdens het effect Regenboog, lampen bleven hangen.",
  "report.make": "Rapport maken",
  "report.busy": "Bezig (tot 30 s)...",
  "report.download": "Downloaden",
  "report.done": "Klaar. Het bestand staat op het bureaublad en de map is geopend. Stuur dit bestand op:",
  // ---------------------------------------------------------------- eenvoudige bediening (telefoon)
  "easy.button": "Eenvoudig",
  "easy.switchHint": "Eenvoudige bediening met grote knoppen (ook voor de telefoon)",
  "easy.full": "Alles",
  "easy.noDongle": "Geen dongle",
  "easy.all": "Alle lampen",
  "easy.slow": "Langzaam",
  "easy.fast": "Snel",
  "easy.speed": "Snelheid van het effect",
  "easy.tapHint": "Tik minstens 4 keer op de maat; effecten met ♪ lopen dan mee.",
  "easy.tlPlaying": "Speelt: {name}",
  "easy.settingsTitle": "Eenvoudige bediening",
  "easy.settingsHelp": "De eenvoudige bediening heeft grote knoppen voor telefoon en tablet, en is ook wat anderen zien als de app vergrendeld is. Aan/uit, helderheid, blackout en scènes staan er altijd in; hier kies je wat er nog meer bij komt.",
  "easy.settingsHint": "Wat er naast aan/uit, helderheid en scènes nog te zien is:",
  "easy.show.colors": "Kleuren",
  "easy.show.effects": "Effecten (en eigen chases)",
  "easy.show.groups": "Groepen kiezen",
  "easy.show.music": "Muziek: TAP en BPM",
  "easy.show.timelines": "Tijdlijnen starten en stoppen",
  "easy.show.strobe": "Strobo-knop",
  "easy.openHere": "Eenvoudige bediening openen",

  // ---------------------------------------------------------------- telefoon koppelen
  "phone.title": "Telefoon of tablet",
  "phone.help": "Je bedient het licht dan vanuit de browser op je telefoon of tablet (Safari, Chrome). Er hoeft niets geïnstalleerd te worden. Telefoon en pc moeten op hetzelfde wifi-netwerk zitten. Op de telefoon opent de eenvoudige bediening; met 'Alles' krijg je de volledige app.",
  "phone.enable": "Bedienen vanaf telefoon of tablet",
  "phone.offHint": "Zet dit aan, dan verschijnt hier een QR-code om te scannen.",
  "phone.starting": "Wordt aangezet... even geduld.",
  "phone.noNetwork": "Deze pc heeft geen netwerkverbinding. Verbind hem met hetzelfde wifi-netwerk als de telefoon.",
  "phone.scan": "Scan deze code met de camera van je telefoon (of typ het adres in de browser):",
  "phone.otherNetwork": "Ander netwerk van deze pc",
  "phone.connected": "Verbonden: {list}",
  "phone.noneYet": "Nog geen telefoon of tablet verbonden.",
  "phone.fwOk": "Windows Firewall laat telefoons door.",
  "phone.fwMissing": "Lukt verbinden niet? Dan blokkeert Windows Firewall het waarschijnlijk.",
  "phone.fwButton": "Firewall openzetten",
  "phone.fwHelp": "Maakt in Windows Firewall een regel die alleen apparaten uit je eigen netwerk toelaat, op de poort van Glowsmith. Windows vraagt om beheerdersrechten. Was er bij het aanzetten al een melding van Windows Firewall? Kies daar 'Toestaan'.",
  "phone.fwBusy": "Bevestig de vraag van Windows...",
  "phone.fw.ok": "Klaar: telefoons in je netwerk kunnen nu verbinden.",
  "phone.fw.cancelled": "Geannuleerd: er is niets veranderd.",
  "phone.fw.failed": "Dat lukte niet: {error}",
  "phone.iphone.1": "Zorg dat de iPhone op hetzelfde wifi-netwerk zit als deze pc.",
  "phone.iphone.2": "Open de Camera en richt hem op de QR-code. Tik op de gele link: Safari opent Glowsmith.",
  "phone.iphone.3": "Als app op je beginscherm: tik in Safari op Deel (vierkantje met pijl) > 'Zet op beginscherm' > Voeg toe.",
  "phone.iphone.4": "Open voortaan het Glowsmith-icoon. Werkt het niet meer? Kijk of de app op de pc draait en of het adres hierboven veranderd is.",
  "phone.android.1": "Zorg dat de telefoon op hetzelfde wifi-netwerk zit als deze pc.",
  "phone.android.2": "Open de Camera (of Google Lens) en richt hem op de QR-code. Tik op de link: Chrome opent Glowsmith.",
  "phone.android.3": "Als app op je startscherm: tik in Chrome op ⋮ > 'Toevoegen aan startscherm' (of 'App installeren').",
  "phone.android.4": "Open voortaan het Glowsmith-icoon. Werkt het niet meer? Kijk of de app op de pc draait en of het adres hierboven veranderd is.",
  "phone.trouble": "Lukt het niet? Gast-wifi en sommige hotspots laten apparaten niet met elkaar praten: gebruik het gewone netwerk. Het adres kan veranderen als de pc opnieuw verbindt; geef de pc dan in de router een vast adres.",

  // ---------------------------------------------------------------- koppelingen: telefoon-automatisering
  "int.phoneAuto": "Telefoon: knoppen, Siri en automatisch",
  "int.phoneAutoHelp": "Elke actie heeft een eigen link. Als de telefoon die link opent (via een knop, Siri of een automatisering), voert Glowsmith de actie uit. De sleutel in de link zorgt dat alleen jouw apparaten dit kunnen.",
  "int.phoneAutoHint": "Kies wat er moet gebeuren; de link hieronder doet precies dat. Gebruik hem in de Opdrachten-app (iPhone) of MacroDroid (Android).",
  "int.whatShould": "Wat moet er gebeuren?",
  "int.qrTry": "Uitproberen: scan de code met de telefoon en open de link.",
  "int.act.scene": "Scène: {name}",
  "int.act.flash": "Groen knipperen (5 s), bijv. voor een melding",
  "int.act.flashRed": "Rood knipperen (10 s)",
  "int.act.on": "Alle lampen aan",
  "int.act.off": "Alle lampen uit",
  "int.act.blackout": "Blackout aan/uit",
  "int.act.color": "Alles blauw",
  "int.act.effect": "Effect: regenboog",
  "int.act.stop": "Effecten stoppen",
  "int.act.timeline": "Tijdlijn starten",
  "int.androidTitle": "Android: MacroDroid (ook bij WhatsApp-berichten)",
  "int.wa4": "Plak de link van hierboven.",
  "int.wa5": "Telefoon en pc moeten op hetzelfde wifi-netwerk zitten. Test de macro met de knop 'Test' in MacroDroid.",
  "int.androidMore": "Andere triggers in MacroDroid: een knop op het startscherm (snelkoppeling), een tijdstip, binnenkomen in een wifi-netwerk, een NFC-sticker, of een sms.",
  "int.iosTitle": "iPhone: een knop of Siri (app Opdrachten)",
  "int.ios1": "Open de app Opdrachten (Shortcuts; staat standaard op de iPhone) en tik op + om een nieuwe opdracht te maken.",
  "int.ios2": "Tik op 'Voeg taak toe', zoek 'URL' en kies 'Haal inhoud van URL op' (Get Contents of URL).",
  "int.ios3": "Tik op 'URL' in de taak en plak de link van hierboven. Geef de opdracht bovenaan een naam, bijv. 'Licht rood'.",
  "int.ios4": "Tik op ▶ om te testen. De eerste keer vraagt de iPhone om toegang tot het lokale netwerk: kies 'Sta toe'.",
  "int.ios5": "Gebruiken: tik op de opdracht, zet hem als widget of icoon op je beginscherm (Deel > Zet op beginscherm), of zeg 'Hé Siri, licht rood'.",
  "int.iosAutoTitle": "iPhone: automatisch (bijv. bij een bericht)",
  "int.iosAuto1": "In Opdrachten: tab 'Automatisering' > + (of 'Nieuwe automatisering').",
  "int.iosAuto2": "Kies 'Bericht' (iOS 17 of nieuwer): bij een bericht van een bepaalde persoon, of met een bepaalde tekst. Dit werkt voor iMessage en sms.",
  "int.iosAuto3": "Kies 'Voer direct uit' (en zet 'Meld bij uitvoeren' uit als je geen melding wilt).",
  "int.iosAuto4": "Voeg de taak 'Haal inhoud van URL op' toe met de link van hierboven, of 'Voer opdracht uit' met je opdracht uit het vorige blok.",
  "int.iosTriggers": "Andere automatische triggers op de iPhone: een tijdstip, een wekker, aankomen of vertrekken op een plek, verbinden met een wifi-netwerk, een NFC-sticker (tik je telefoon ertegen), een Focus-modus, of het openen van een app.",
  "int.iosWhatsapp": "WhatsApp op de iPhone: Apple laat apps niet reageren op WhatsApp-berichten. Wat wel kan: laat afzenders een iMessage of sms sturen (trigger 'Bericht'), of gebruik een Android-telefoon met MacroDroid als meldingen-ontvanger.",
  "easy.moreColors": "Meer kleuren ({n})",
  "easy.lessColors": "Minder kleuren",
  // ---------------------------------------------------------------- tijdschema
  "nav.schedule": "Planning",
  "sch.title": "Tijdschema",
  "sch.help": "Laat het licht vanzelf iets doen op vaste tijden: bijvoorbeeld elke avond bij zonsondergang aan, om 23:30 uit, of op werkdagen om 08:00 een scène. De app (en de pc) moet daarvoor aan staan.",
  "sch.enabled": "Tijdschema actief",
  "sch.disabledNote": "Het tijdschema staat uit: er gebeurt niets automatisch.",
  "sch.empty": "Nog niets gepland. Voeg iets toe, of kies een voorbeeld hieronder.",
  "sch.itemOn": "Aan/uit voor dit onderdeel",
  "sch.namePlaceholder": "Naam (mag leeg)",
  "sch.test": "Nu doen",
  "sch.testHint": "Voert de actie meteen uit, om te proberen",
  "sch.ran": "uitgevoerd",
  "sch.kind.time": "Tijd",
  "sch.kind.sunset": "Zonsondergang",
  "sch.kind.sunrise": "Zonsopkomst",
  "sch.minutes": "minuten (min = ervoor)",
  "sch.once": "Eén keer, op",
  "sch.everyDay": "elke dag",
  "sch.day.1": "ma", "sch.day.2": "di", "sch.day.3": "wo", "sch.day.4": "do", "sch.day.5": "vr", "sch.day.6": "za", "sch.day.7": "zo",
  "sch.then": "Dan:",
  "sch.custom": "Eigen opdracht...",
  "sch.commandOk": "Opdracht klopt.",
  "sch.grp.basic": "Algemeen",
  "sch.act.blackoutOn": "Blackout aan",
  "sch.act.blackoutOff": "Blackout uit",
  "sch.act.master": "Helderheid {n}%",
  "sch.act.tlPlay": "Tijdlijn starten: {name}",
  "sch.act.tlStop": "Tijdlijn stoppen",
  "sch.add": "Toevoegen",
  "sch.examples": "Voorbeelden:",
  "sch.ex.sunset": "Aan bij zonsondergang",
  "sch.ex.sunsetName": "Avond: aan",
  "sch.ex.night": "Uit om 23:30",
  "sch.ex.nightName": "Nacht: uit",
  "sch.ex.work": "Werkdagen 08:00 aan",
  "sch.ex.workName": "Ochtend: aan",
  "sch.upcoming": "Komt eraan",
  "sch.nothing": "Niets gepland.",
  "sch.last": "Laatst: {when} - {what} ({result})",
  "sch.sun": "Zon",
  "sch.sunToday": "Vandaag: op {rise}, onder {set}",
  "sch.place": "Plaats (voor zonsopkomst/-ondergang)",
  "sch.placeOther": "Anders (zie coördinaten)",
  "sch.lat": "Breedtegraad",
  "sch.lon": "Lengtegraad",
  "sch.myPlace": "Mijn locatie gebruiken",
  "sch.pcOn": "Het tijdschema werkt alleen als Glowsmith draait. Zet bij Instellingen 'pc wakker houden' aan. Was de pc in slaap, dan wordt alleen wat in de laatste 2 minuten had moeten gebeuren alsnog uitgevoerd.",

  // ---------------------------------------------------------------- scherm volgen
  "fx.screen": "Scherm volgen",
  "fxcat.screen": "Scherm",
  "ss.hint": "De lampen nemen de kleuren van het pc-scherm over, van links naar rechts (zoals ambilight). Mooi bij een film, presentatie of visuals.",
  "ss.previewHint": "Wat de app nu van het scherm ziet",
  "ss.notRunning": "Er komt (nog) geen beeld binnen. Start het effect, of kies een ander scherm.",
  "ss.mode": "Verdeling",
  "ss.modeHelp": "Kolommen: het scherm wordt van links naar rechts over de lampen verdeeld (de lampvolgorde bij Lampen telt). Gemiddelde: alle lampen krijgen de gemiddelde kleur van het hele scherm.",
  "ss.zones": "Kolommen (links → rechts)",
  "ss.average": "Gemiddelde kleur",
  "ss.monitor": "Scherm",
  "ss.saturation": "Kleurversterking: {n}%",
  "ss.smoothing": "Rustiger: {n}%",
  "ss.brightness": "Donker beeld = donkere lampen",
  "ss.mirror": "Links en rechts omdraaien (lampen achter het scherm)",
  "ss.drm": "Let op: video van streamingdiensten (Netflix enz.) in de browser kan als zwart doorkomen door kopieerbeveiliging.",

  // ---------------------------------------------------------------- home assistant
  "ha.status.off": "uit",
  "ha.status.connecting": "verbinden...",
  "ha.status.connected": "verbonden",
  "ha.status.error": "geen verbinding",
  "ha.help": "Via MQTT verschijnt Glowsmith vanzelf in Home Assistant als apparaat met lampen, scènes, effecten en knoppen. Daarmee werkt het ook in automatiseringen, dashboards en spraak (Google, Alexa, Assist) via Home Assistant.",
  "ha.intro": "Bedien het licht vanuit Home Assistant: alles verschijnt vanzelf (MQTT-discovery).",
  "ha.enable": "Verbinden met Home Assistant (MQTT)",
  "ha.host": "Adres van Home Assistant / MQTT-broker",
  "ha.user": "Gebruikersnaam",
  "ha.password": "Wachtwoord",
  "ha.passwordSaved": "•••••• (opgeslagen)",
  "ha.advanced": "Geavanceerd",
  "ha.name": "Apparaatnaam",
  "ha.prefix": "Topic-voorvoegsel",
  "ha.discovery": "Discovery-voorvoegsel",
  "ha.stepsTitle": "Zo koppel je het",
  "ha.step1": "In Home Assistant: Instellingen > Add-ons > Add-on store > 'Mosquitto broker' installeren en starten.",
  "ha.step2": "Instellingen > Apparaten & diensten: voeg 'MQTT' toe (wordt meestal vanzelf gevonden) en bevestig.",
  "ha.step3": "Maak een gebruiker voor Glowsmith: Instellingen > Personen > Gebruikers > Gebruiker toevoegen (bijv. 'dmxlicht').",
  "ha.step4": "Vul hierboven het adres van Home Assistant in (bijv. homeassistant.local of 192.168.1.50), poort 1883, en die gebruiker met wachtwoord. Zet 'Verbinden' aan.",
  "ha.step5": "Bij Apparaten & diensten > MQTT staat nu het apparaat 'Glowsmith' met alle entiteiten.",
  "ha.whatTitle": "Wat je in Home Assistant krijgt",
  "ha.what1": "Lamp 'Licht': aan/uit, helderheid, kleur en effect (ook je eigen chases en Scherm volgen).",
  "ha.what2": "Een lamp per groep (aan/uit en kleur).",
  "ha.what3": "Keuzelijsten 'Scène' en 'Effect', schuif 'Helderheid', schakelaars 'Blackout' en 'Strobo'.",
  "ha.what4": "Knoppen: effecten stoppen, tijdlijn start/GO, tijdlijn stop, tap tempo.",
  "ha.what5": "Sensoren: dongle verbonden, BPM.",
  "ha.what6": "Tekstveld 'Opdracht': elke opdracht uit de opdrachtentaal (zie Opdrachten), bijv. 'flash green 5'.",
  "ha.exampleTitle": "Voorbeelden voor automatiseringen",
  "ha.exampleHint": "In een automatisering of script (YAML-modus). De namen van de entiteiten kunnen bij jou iets anders zijn.",
  "ha.ex1": "Een scène kiezen",
  "ha.ex2": "Een opdracht sturen (bijv. bij de deurbel)",
  "ha.ex3": "Alle lampen oranje op 60%",

  // ---------------------------------------------------------------- osc
  "osc.help": "OSC (Open Sound Control) is een manier waarop apps, lichttafels en showsoftware elkaar berichten sturen over het netwerk. Elk bericht heeft een adres (zoals /dmxlicht/scene/1) en eventueel waarden.",
  "osc.whatTitle": "Wat is OSC?",
  "osc.what": "Met OSC bedien je Glowsmith vanuit apps als TouchOSC (telefoon/tablet), QLab, Bitfocus Companion, Resolume, Open Stage Control of Max. Elk bericht is een adres plus waarden. Het adres is gewoon een opdracht uit de opdrachtentaal, met / ertussen.",
  "osc.setupTitle": "Instellen",
  "osc.setup1": "Zet OSC hierboven aan (standaardpoort 8000).",
  "osc.setup2": "Stel in de andere app als doel in: host {host}, poort {port}, protocol UDP. Staat die app op deze pc, gebruik dan 127.0.0.1.",
  "osc.setup3": "Komt er niets binnen? Bij Instellingen > Telefoon of tablet > 'Firewall openzetten' wordt ook de OSC-poort geopend (alleen voor je eigen netwerk).",
  "osc.setup4": "Controle: hierboven staat 'laatst ontvangen' met het laatste bericht.",
  "osc.rulesTitle": "Zo werkt een adres",
  "osc.rule1": "/dmxlicht/ vooraan mag, maar hoeft niet: /scene/1 werkt ook.",
  "osc.rule2": "Delen van de opdracht staan in het adres (/color/red) of als waarde (/color met de tekst \"red\"); allebei mag.",
  "osc.rule3": "Knoppen: waarde 1 = indrukken, 0 = loslaten. Bij scène, effect, kleur, flash, tap, aan/uit en tijdlijn wordt het loslaten genegeerd, dus een gewone knop werkt goed.",
  "osc.rule4": "Faders: /dmxlicht/master met een kommagetal 0.0-1.0 (0 tot 100%), of een heel getal 0-100.",
  "osc.rule5": "Namen met spaties als tekstwaarde sturen: /dmxlicht/scene met \"Rood licht\". Scènes mogen ook met hun nummer: /scene/2 = de tweede scène.",
  "osc.examplesTitle": "Voorbeelden",
  "osc.address": "Adres",
  "osc.args": "Waarde(n)",
  "osc.does": "Doet",
  "osc.ex.scenePos": "Eerste scène",
  "osc.ex.sceneName": "Scène op naam",
  "osc.ex.blackout": "Blackout aan / uit / wisselen",
  "osc.ex.masterFloat": "Helderheid met een fader (0.0-1.0)",
  "osc.ex.masterInt": "Helderheid in procenten",
  "osc.ex.on": "Alle lampen aan",
  "osc.ex.offGroup": "Een groep uit",
  "osc.ex.color": "Alles rood",
  "osc.ex.colorHex": "Een groep in een eigen kleur",
  "osc.ex.effect": "Effect Vuur op alle lampen",
  "osc.ex.effectGroup": "Effect op één groep",
  "osc.ex.chase": "Je eigen chase",
  "osc.ex.screen": "Scherm volgen",
  "osc.ex.stop": "Effecten stoppen",
  "osc.ex.strobe": "Strobo aan (1) / uit (0): ideaal voor een knop die je ingedrukt houdt",
  "osc.ex.flash": "5 seconden groen knipperen",
  "osc.ex.flashSolid": "10 seconden vast rood",
  "osc.ex.tap": "Tempo tikken",
  "osc.ex.bpm": "Tempo instellen",
  "osc.ex.fade": "Overgangstijd 2,5 s",
  "osc.ex.tlPlay": "Tijdlijn starten",
  "osc.ex.tlGo": "GO (verder na een wachtpunt)",
  "osc.ex.tlPause": "Tijdlijn pauzeren",
  "osc.ex.tlStop": "Tijdlijn stoppen",
  "osc.ex.tlNext": "Naar het volgende punt",
  "osc.ex.tlSeek": "Naar 30 seconden",
  "osc.ex.recover": "USB-verbinding herstellen",
  "osc.appsTitle": "Tips per app",
  "osc.app1": "TouchOSC: zet bij een knop het OSC-adres (bijv. /dmxlicht/scene/1); de knop stuurt zelf 1 bij indrukken en 0 bij loslaten. Een fader op /dmxlicht/master stuurt 0.0-1.0.",
  "osc.app2": "QLab: Network cue, type OSC, bestemming = deze pc en poort; bericht bijv. /dmxlicht/scene \"Pauze\".",
  "osc.app3": "Bitfocus Companion: gebruik liever de Glowsmith-module (Koppelingen > Stream Deck); met de generieke OSC-module kan het ook.",
  "osc.app4": "Resolume / Open Stage Control / Max: stuur naar dezelfde host en poort; de adressen zijn hetzelfde.",
  "osc.names": "Alle effectnamen en kleuren",
  "osc.namesHint": "Gebruik deze namen achter /dmxlicht/effect/ (houd de muis erop voor de Nederlandse naam). Je eigen chases gaan op hun naam.",
  "osc.colorNames": "Kleuren (ook Nederlandse namen en #RRGGBB werken)",

  "help.page.schedule": `# Tijdschema
Laat het licht vanzelf iets doen op vaste momenten.
1. Klik 'Toevoegen' (of kies een voorbeeld).
2. Kies wanneer: een tijd, of zonsondergang/-opkomst met minuten ervoor of erna.
3. Kies de dagen (alles aan = elke dag), of 'Eén keer' op een datum.
4. Kies wat er moet gebeuren: aan, uit, een scène, een effect, een tijdlijn... of een eigen opdracht.
5. 'Overvloeien in': laat het licht heel geleidelijk veranderen, bijv. 30 minuten voor een wekker (zonsopkomst).
6. 'Nu doen' probeert het meteen.
# Goed om te weten
- Rechts staat wat er de komende dagen gebeurt.
- Voor zonsopkomst/-ondergang: kies je plaats.
- De app moet draaien. Zet 'pc wakker houden' aan bij Instellingen.`,
  // ---------------------------------------------------------------- camera op een tv
  "cam.source": "Bron",
  "cam.sourceHelp": "Dit pc-scherm: wat er op het scherm van deze pc staat. Camera op een tv: een webcam kijkt naar een tv (of beamerscherm); je geeft met een kader aan waar de tv in beeld is. Zo werkt het ook met een tv-kastje, Netflix-app of spelcomputer.",
  "cam.fromScreen": "Dit pc-scherm",
  "cam.fromCamera": "Camera op een tv",
  "cam.hint": "Een webcam kijkt naar de tv; alleen wat binnen het kader valt telt. De kleuren gaan van links naar rechts over de lampen, net als bij het pc-scherm.",
  "cam.device": "Webcam",
  "cam.auto": "Automatisch (eerste echte webcam)",
  "cam.none": "Geen webcam gevonden. Sluit een USB-webcam aan.",
  "cam.onlyHere": "Het camerabeeld (en het kader) is alleen op deze pc te zien, niet op een telefoon of tablet.",
  "cam.starting": "Webcam starten...",
  "cam.tl": "Linksboven", "cam.tr": "Rechtsboven", "cam.br": "Rechtsonder", "cam.bl": "Linksonder",
  "cam.reset": "Kader herstellen",
  "cam.whole": "Heel beeld",
  "cam.dragHint": "Sleep de bolletjes 1-4 op de hoeken van de tv (1 = linksboven, met de klok mee).",
  "cam.step1": "Zet de webcam stevig neer, met de hele tv in beeld (recht ervoor werkt het best, schuin mag ook).",
  "cam.step2": "Sleep de vier hoeken van het kader op de hoeken van het tv-beeld (niet de rand van de tv).",
  "cam.step3": "Start Scherm volgen. Bovenaan zie je welke kleuren de app uit de tv haalt.",
  "cam.step4": "Te flets of te druk? Zet 'Kleurversterking' hoger en 'Rustiger' wat hoger. Lukt het niet goed in een donkere kamer: zet de automatische belichting van de webcam uit in de camera-app van Windows.",
  "edge.mode": "Randen (ambilight)",
  "edge.hint": "Echte ambilight: elke lamp neemt de kleur van de rand van het beeld die het dichtst bij hem zit. De bolletjes zijn je {n} lampen in de volgorde van de lijst bij Lampen.",
  "edge.auto": "Lampen vanzelf verdelen over links, boven en rechts",
  "edge.edgeLeft": "Links",
  "edge.edgeTop": "Boven",
  "edge.edgeRight": "Rechts",
  "edge.edgeBottom": "Onder",
  "edge.depth": "Randdiepte: {n}%",
  "edge.reverse": "Tegen de klok in",
  "edge.start": "Eerste lamp schuiven",
  "edge.order": "Volgorde: links van onder naar boven, dan boven van links naar rechts, rechts van boven naar onder, onder van rechts naar links. Klopt het niet met je opstelling? Gebruik 'Tegen de klok in' en 'Eerste lamp schuiven', of zet de lampen bij Lampen in een andere volgorde.",
  "edge.bars": "Zwarte balken (films) overslaan",
  "sch.fade": "Overvloeien in",
  "sch.seconds": "seconden",
  "sch.minutesUnit": "minuten",
  "sch.fadeHint": "het licht gaat heel geleidelijk naar de nieuwe stand",
  "sch.fadeNone": "0 = de gewone overgangstijd",
  "sch.ex.wake": "Wekker: zonsopkomst",
  "sch.ex.wakeName": "Wekker (30 min lichter)",
  "sch.ex.dim": "Langzaam uit om 23:00",
  "sch.ex.dimName": "Avond: langzaam uit",
  "cam.findTv": "Tv automatisch zoeken",
  "cam.searching": "Zoeken...",
  "cam.found": "Gevonden. Controleer de hoeken en stel zo nodig bij.",
  "cam.notFound": "Geen tv gevonden. Zet iets lichts op de tv (bijv. een wit of kleurrijk beeld), maak de kamer wat donkerder en probeer opnieuw.",
  "cam.keysHint": "Klik een bolletje en gebruik de pijltjestoetsen om precies te schuiven (Shift = grotere stappen).",
  "cam.keyV": "Keystone verticaal",
  "cam.keyH": "Keystone horizontaal",
  "cam.rotate": "Draaien",
  "cam.size": "Grootte",
  "cam.moveX": "Links / rechts",
  "cam.moveY": "Omhoog / omlaag",
  "pair.title": "Telefoon koppelen",
  "pair.hint": "Dit apparaat mag het licht nog niet bedienen. Vul de koppelcode in die op de pc staat.",
  "pair.where": "De code staat op de pc bij Instellingen > Telefoon of tablet. Scan je de QR-code daar, dan gaat koppelen vanzelf.",
  "pair.button": "Koppelen",
  "pair.wrong": "Deze code klopt niet (meer). Kijk op de pc voor de huidige code.",
  "pair.tooMany": "Te veel pogingen. Wacht een minuut.",
  "phone.fixedName": "Vaste naam in het netwerk",
  "phone.fixedNameOff": "vaste naam niet actief",
  "phone.fixedNameHelp": "Met de vaste naam (bijv. dmxlicht.local) blijft het adres op je telefoon werken, ook als het IP-adres van de pc verandert. Werkt op iPhone, iPad, Mac, Windows en de meeste Android-telefoons (Android 12+). Lukt het niet? Kies dan het IP-adres.",
  "phone.qrName": "QR met vaste naam",
  "phone.qrIp": "QR met IP-adres",
  "phone.pairing": "Telefoons eerst koppelen",
  "phone.pairingHelp": "Alleen gekoppelde telefoons en tablets mogen het licht bedienen. Koppelen gaat vanzelf met de QR-code, of met de 6-cijferige code. Elke code werkt één keer. Trigger-links (met sleutel), Companion, OSC en Home Assistant werken gewoon door.",
  "phone.code": "Koppelcode",
  "phone.newCode": "Nieuwe code",
  "phone.noPairingWarn": "Iedereen op dit wifi-netwerk kan nu het licht bedienen.",
  "phone.devices": "Gekoppelde apparaten",
  "phone.lastSeen": "laatst {when}",
  "phone.remove": "Verwijderen",
  "patch.universeCol": "Universe",
  "patch.universeHelp": "Eén DMX-kabel heeft 512 kanalen; dat is één 'universe'. Heb je meer lampen (of meer kabels/Art-Net-nodes), zet ze dan in universe 2, 3, ... Elke universe heeft weer adres 1 t/m 512. 'Adressen opnieuw nummeren' gaat vanzelf door naar de volgende universe als er een vol is.",
  "patch.universeN": "Universe {n}",
  "out.udmxUniverse": "USB-dongle stuurt",
  "out.multiHint": "Deze show gebruikt {n} universes. Art-Net en sACN sturen ze allemaal: de eerste op het nummer hierboven, de volgende op +1, +2, ...",
  "out.multiHelp": "Voorbeeld: Art-Net universe 0 hierboven betekent universe 1 van de show op Art-Net 0, universe 2 op Art-Net 1, enz. De USB-dongle heeft één kabel en stuurt dus één universe; kies hierboven welke.",
  "upd.title": "Updates",
  "upd.help": "Nieuwe versies komen van GitHub (github.com/inkvolcano/dmx-light-control). Bij bijwerken wordt de nieuwe versie gedownload, gecontroleerd (controlegetal), en vervangt de app zichzelf; daarna start hij opnieuw. Je show en instellingen (map Instellingen) blijven staan.",
  "upd.current": "Deze versie: {v}",
  "upd.available": "Nieuwe versie beschikbaar: {v}",
  "upd.upToDate": "Je hebt de nieuwste versie.",
  "upd.install": "Bijwerken en opnieuw starten",
  "upd.installing": "Bijwerken... de app sluit en start zo opnieuw.",
  "upd.onlyHere": "Bijwerken kan alleen op de pc zelf.",
  "upd.check": "Nu controleren",
  "upd.lastCheck": "laatst gecontroleerd: {when}",
  "upd.auto": "Automatisch kijken of er een nieuwe versie is (bijwerken doe je zelf)",
  "upd.confirm": "Bijwerken naar versie {v}? De app sluit even en start daarna opnieuw; het licht staat een paar seconden stil.",
  "upd.chip": "Update {v}",
  "upd.chipHint": "Er is een nieuwe versie: klik om naar Instellingen te gaan",
  "nav.screen": "Scherm",
  "ss.openPage": "Instellen op de pagina Scherm",
  "ss.start": "Scherm volgen starten",
  "ss.lampsNow": "Wat de lampen nu doen",
  "ss.isOn": "volgt het scherm",
  "ss.isOff": "staat uit",
  "ss.cardSource": "Beeld",
  "ss.cardCamera": "Camera, kader en keystone",
  "ss.cardSpread": "Verdeling over de lampen",
  "ss.pageHelp": "Hier laat je de lampen de kleuren van een scherm volgen: het scherm van deze pc, of een tv via een webcam. Links kies je de bron (en bij een camera het kader rond de tv, met keystone), rechts hoe de kleuren over de lampen gaan. Starten en stoppen kan hier of bij Effecten > Scherm volgen.",
  "help.page.screen": `# Scherm (ambilight)
De lampen nemen de kleuren over van een beeld, net als ambilight bij een tv.

# Bron
- Dit pc-scherm: wat er op het scherm van deze pc staat (film, presentatie, visuals). Heb je meer schermen, kies er een.
- Camera op een tv: een webcam kijkt naar een tv of beamerscherm. Zo werkt het ook met een tv-kastje, streaming-app of spelcomputer.

# Kader en keystone (alleen bij camera)
1. Zet de webcam stevig neer met de hele tv in beeld.
2. Klik op "Tv automatisch zoeken" (zet de tv aan, met een licht beeld), of sleep de bolletjes 1-4 op de hoeken van de tv.
3. Fijn afstellen: kies een hoek en verschuif met de pijltjestoetsen, of gebruik de keystone-knoppen (verticaal, horizontaal, draaien, grootte), net als bij een beamer.
4. Het raster laat zien hoe de app het beeld rechttrekt.

# Verdeling
- Kolommen: links van het beeld naar links in de rij lampen, enz.
- Gemiddelde: alle lampen dezelfde kleur.
- Randen: lampen rondom de tv; zie het voorbeeld met nummers.
- Markers: je zet per lamp zelf een bolletje op het beeld; die lamp pakt de kleur van precies die plek.
- Rustiger maakt snelle wisselingen zachter; kleurversterking maakt bleke beelden feller.`,
  "pts.mode": "Markers",
  "pts.title": "Markers: waar elke lamp zijn kleur vandaan haalt",
  "pts.hint": "Elk genummerd bolletje is een lamp ({n} in totaal, in de volgorde van de pagina Lampen). Sleep het naar de plek op het beeld waar die lamp zijn kleur moet pakken; het vierkantje is het gebied dat gemeten wordt. Het bolletje kleurt mee met wat de lamp nu doet.",
  "pts.dragHint": "Sleep een bolletje, of klik er een aan en klik dan op het beeld om hem daarheen te zetten.",
  "pts.selectedHint": "Lamp {n} gekozen: klik op het beeld om hem te verplaatsen, of gebruik de pijltjestoetsen (Shift = grotere stappen). Esc = loslaten.",
  "pts.lamp": "Lamp {n}",
  "pts.size": "Meetvlak: {n}% van de breedte",
  "pts.reset": "Op een rij",
  "pts.around": "Rondom het beeld",
  "pts.noPicture": "Nog geen beeld. Op deze pc verschijnt hier het scherm of de tv; elders de kleuren zodra scherm volgen loopt.",
  "pts.onlyHere": "Het echte beeld is alleen op de pc zelf te zien; hier zie je de kleuren die de app meet (zodra scherm volgen loopt).",
  "pts.below": "De markers zet je hieronder op het beeld.",
  "tabs.title": "Tabbladen in het menu",
  "tabs.hint": "Vink uit wat je niet gebruikt; het verdwijnt uit het menu links. Niets gaat verloren: vink het weer aan en het is terug.",
  "tabs.help": "Handig om de app eenvoudiger te maken, bijvoorbeeld voor een collega die alleen de bediening en effecten nodig heeft. Dit geldt voor alle apparaten (pc, telefoon, tablet). Instellingen blijft altijd zichtbaar, anders kun je de tabbladen niet meer terugzetten.",
  "tabs.always": "altijd zichtbaar",
  "tabs.settingsAlways": "Instellingen blijft altijd in het menu, zodat je tabbladen altijd weer aan kunt zetten",
};
