import type { CustomChase } from "./chase";
// Types mirror the C# DTOs (camelCase JSON). Calls go to the local app on the same origin.

export type ChannelKind =
  | "dimmer" | "red" | "green" | "blue" | "white" | "amber" | "uv"
  | "hue" | "saturation" | "value" | "fixed" | "strobe";

export interface ChannelDef {
  number: number;
  kind: ChannelKind;
  cell: number;
  default: number;
  rangeMin?: number;
  rangeMax?: number;
  label?: string;
  note?: string;
}

export interface FixtureMode {
  name: string;
  footprint: number;
  channels: ChannelDef[];
  displayCode?: string;
  cellCount: number;
}

export interface FixtureType {
  id: string;
  manufacturer: string;
  model: string;
  category: string;
  colors?: string;
  modes: FixtureMode[];
  notes?: string;
  custom: boolean;
}

export interface PatchedFixture {
  id: string;
  name: string;
  typeId: string;
  mode: string;
  address: number;
  /** Universe 1..8 (each its own DMX line / Art-Net or sACN universe). */
  universe?: number;
  x: number;
  y: number;
}

export interface ColorRef { key?: string; hex?: string }
export interface FixtureGroup { id: string; name: string; fixtureIds: string[] }
export interface EffectLayer { groupId: string; effect: EffectSettings; source?: string | null }
export type ClipKind = "scene" | "effect";
export interface TimelineClip { id: string; startMs: number; lengthMs: number; kind: ClipKind; sceneId: string | null; effect: EffectSettings | null; fadeMs: number }
export interface TimelineTrack { id: string; name: string; kind: "scenes" | "effects"; target: string; clips: TimelineClip[] }
export interface TimelineMarker { id: string; atMs: number; name: string | null; kind: "wait" | "cue" }
export interface Timeline {
  id: string; name: string; lengthMs: number; bpm: number; snap: boolean; loop: boolean; audioFile: string | null; tracks: TimelineTrack[];
  startMs?: number; endMs?: number | null; markers?: TimelineMarker[];
}
export interface TimelineLive { id: string | null; playing: boolean; positionMs: number; waiting?: string | null }
export interface SceneTemplateInfo { key: string; name: string; nameEn: string; category: string; color: string }
export interface SceneInfo { id: string; name: string; color: string; fadeMs: number; mode: OutputMode }
export interface FixtureLook { on: boolean; color: ColorRef }

export interface EffectSettings {
  effectIndex: number;
  /** Set when one of the user's own chases runs instead of a library effect. */
  chaseId?: string | null;
  /** Follow the PC screen's colours (ambilight). */
  screenSync?: boolean;
  stepMs: number;
  onMs: number;
  offMs: number;
  delayMs: number;
  colorA: string;
  colorB: string;
  colorPerLamp: boolean;
  lampColors: string[];
}

export type OutputMode = "static" | "effect" | "faders";

export type AudioModulation = "none" | "level" | "bass" | "beatPulse";
export interface MusicSettings {
  enabled: boolean;
  deviceId: string;
  syncEffects: boolean;
  stepsPerBeat: number;
  modulation: AudioModulation;
  floorPercent: number;
  sensitivity: number;
}
export interface MusicLive {
  levels: { level: number; bass: number; mid: number; high: number; bpm: number; beats: number };
  bpm: number;
  beat: number;
  following: boolean;
  listening: boolean;
  error: string | null;
}

export interface ShowState {
  mode: OutputMode;
  brightnessPercent: number;
  blackout: boolean;
  useWhiteLed: boolean;
  looks: Record<string, FixtureLook>;
  faders: Record<string, number[]>;
  effect: EffectSettings;
  layers: EffectLayer[];
  fadeMs: number;
  activeScene: string | null;
  strobeSpeed: number;
  strobeActive?: boolean;
  music: MusicSettings;
  customColors: string[];
  chases: CustomChase[];
  screenSync: ScreenSyncSettings;
}

export interface ScreenSyncSettings {
  mode: "zones" | "average" | "edges" | "points"; monitor: number; saturation: number; smoothing: number; followBrightness: boolean; mirror: boolean;
  source?: "screen" | "camera"; camera?: string | null; corners?: number[];
  edgeAuto?: boolean; edgeLeft?: number; edgeTop?: number; edgeRight?: number; edgeBottom?: number;
  edgeDepth?: number; skipBlackBars?: boolean; edgeReverse?: boolean; edgeStart?: number;
  /** markers: x, y per lamp (0..1 over the picture) and the measured square in % of the width */
  points?: number[] | null; pointSize?: number;
}
export type ScheduleTimeKind = "time" | "sunrise" | "sunset";
export interface ScheduleItem { id: string; name: string; enabled: boolean; days: number[]; date: string | null; kind: ScheduleTimeKind; time: string; offsetMinutes: number; command: string; fadeSeconds?: number }
export interface ScheduleSettings { enabled: boolean; latitude: number; longitude: number; items: ScheduleItem[] }
export interface MqttSettings { enabled: boolean; host: string; port: number; user: string; password?: string | null; prefix: string; discovery: string; name: string }

export interface OutputSettings {
  udmx: boolean;
  udmxUniverse?: number;
  artNet: boolean;
  artNetHost: string;
  artNetUniverse: number;
  sacn: boolean;
  sacnUniverse: number;
  sacnUnicast: string;
  sacnPriority: number;
  networkBind: string;
}

export interface MidiMapping { id: string; type: string; channel: number; number: number; command: string }
export interface ControlSettings {
  oscEnabled: boolean;
  oscPort: number;
  midiEnabled: boolean;
  midiDevice: string | null;
  midiMappings: MidiMapping[];
  triggerKey: string;
}
export interface ControlLive {
  osc: { port: number; error: string | null; last: string | null };
  midi: {
    device: string | null; error: string | null; learning: boolean;
    last: { type: string; channel: number; number: number; value: number } | null;
    learned: { type: string; channel: number; number: number; value: number } | null;
  };
}

export interface CompanionStatus {
  sourceFound: boolean;
  sourceVersion: string | null;
  companionFound: boolean;
  modulesDir: string;
  installedVersion: string | null;
  upToDate: boolean;
  companionRunning: boolean;
}

export interface AppSettings {
  language: string;
  dongleSpeed: number;
  allowNetwork: boolean;
  port: number;
  preventSleep: boolean;
  outputs: OutputSettings;
  control: ControlSettings;
  locked: boolean;
  hasPin?: boolean;
  easy?: EasySettings;
  /** menu tabs switched off in Settings ("easy" = the Easy button) */
  hiddenPages?: string[];
  mqtt?: MqttSettings;
  requirePairing?: boolean;
  checkUpdates?: boolean;
  hostName?: string;
  devices?: { id: string; name: string; paired: string; lastSeen: string }[];
  hasMqttPassword?: boolean;
}

export interface EasySettings { colors: boolean; effects: boolean; groups: boolean; timelines: boolean; music: boolean; strobe: boolean }

export interface RigFixtureInfo {
  id: string;
  firstHead: number;
  cells: number;
  footprint: number;
  lastAddress: number;
  universe?: number;
  /** Frame index before the fixture's first channel (universe offset + address - 1). */
  baseChannel?: number;
  channels: ChannelDef[];
}

export interface AddressConflict { fixtureA: string; fixtureB: string; from: number; to: number }

export interface FullState {
  name: string | null;
  undo: number;
  redo: number;
  patch: PatchedFixture[];
  state: ShowState;
  groups: FixtureGroup[];
  scenes: SceneInfo[];
  timelines: Timeline[];
  schedule: ScheduleSettings;
  identify: string[];
  settings: AppSettings;
  rig: {
    fixtures: RigFixtureInfo[];
    missing: string[];
    conflicts: AddressConflict[];
    usedChannels: number;
    universes?: number;
    usedPerUniverse?: number[];
    heads: number;
  };
}

export interface PaletteColor { key: string; name: string; hex: string }
export type EffectParam = "ColorA" | "ColorB" | "Step" | "OnOff" | "Delay" | "Audio" | "Beat";
/** category / swatch: set by the server for the second effect collection (the classic ones are grouped in the UI). */
export interface EffectInfo { index: number; key: string; name: string; uses: EffectParam[]; category?: string | null; swatch?: string | null }

export interface Bootstrap {
  version: string;
  library: FixtureType[];
  palette: { fan: PaletteColor[]; specials: PaletteColor[] };
  effects: EffectInfo[];
}

export interface OutputStatus {
  name: string;
  connected: boolean;
  /** NotFound | DriverMissing | InUse | Gone | Timeout | Rejected | Other, when not connected */
  fault: string | null;
  detail: string | null;
  connectedSince: string | null;
  reconnects: number;
  frames: number;
  errors: number;
  recovered: number;
  framesPerSecond: number;
  mode: string;
  lastErrorAt: string | null;
  lastError: string | null;
}

/** PIN for this browser tab after unlocking (sent with every request). */
export function sessionPin(): string | null {
  try { return sessionStorage.getItem("pin"); } catch { return null; }
}
function headers(json = true): Record<string, string> {
  const h: Record<string, string> = json ? { "Content-Type": "application/json" } : {};
  const pin = sessionPin();
  if (pin) h["X-Dmx-Pin"] = pin;
  return h;
}

async function call<T = unknown>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch("api/" + path, {
    method,
    headers: headers(body !== undefined),
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`${method} ${path}: ${res.status} ${await res.text()}`);
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  headers: () => headers(true),
  companionStatus: () => call<CompanionStatus>("GET", "companion"),
  installCompanion: () => call<{ path: string; status: CompanionStatus }>("POST", "companion/install"),
  undo: () => call("POST", "undo"),
  redo: () => call("POST", "redo"),
  shows: () => call<{ name: string; modified: string }[]>("GET", "shows"),
  saveShow: (name: string) => call<{ name: string }>("POST", "shows/save", { name }),
  openShow: (name: string) => call("POST", "shows/open", { name }),
  newShow: () => call("POST", "shows/new"),
  removeShow: (name: string) => call("POST", "shows/remove", { name }),
  restore: async (file: File) => {
    const fd = new FormData();
    fd.append("file", file);
    const res = await fetch("api/restore", { method: "POST", body: fd, headers: headers(false) });
    if (!res.ok) throw new Error(await res.text());
  },
  unlock: (pin: string) => call("POST", "unlock", { pin }),
  lockSetup: (r: { pin?: string; newPin?: string; locked?: boolean }) => call("POST", "lock/setup", r),
  removeFixtureType: (id: string) => call("POST", "library/remove", { id }),
  importOfl: async (file: File) => {
    const fd = new FormData();
    fd.append("file", file);
    const res = await fetch("api/library/ofl", { method: "POST", body: fd, headers: headers(false) });
    if (!res.ok) throw new Error(await res.text());
    return (await res.json()) as FixtureType;
  },
  bootstrap: () => call<Bootstrap>("GET", "bootstrap"),
  state: () => call<FullState>("GET", "state"),
  updateFixtures: (updates: (Partial<Omit<PatchedFixture, "typeId">> & { id: string })[]) => call("POST", "patch/update", updates),
  orderFixtures: (ids: string[]) => call("POST", "patch/order", ids),
  removeFixtures: (ids: string[]) => call("POST", "patch/remove", ids),
  renumber: () => call("POST", "patch/renumber"),
  addFixtures: (typeId: string, mode: string, count: number, address?: number) =>
    call<{ added: number; failed: number }>("POST", "patch/add", { typeId, mode, count, address }),
  looks: (ids: string[], on?: boolean, color?: ColorRef) => call("POST", "looks", { ids, on, color }),
  master: (m: { brightness?: number; blackout?: boolean; useWhiteLed?: boolean; fadeMs?: number }) => call("POST", "master", m),
  saveGroup: (g: { id?: string; name?: string; fixtureIds?: string[] }) => call<{ id: string }>("POST", "groups/save", g),
  removeGroup: (id: string) => call("POST", "groups/remove", { id }),
  startLayer: (groupId: string, effect: EffectSettings) => call("POST", "layers/start", { groupId, effect }),
  stopLayer: (groupId: string | null) => call("POST", "layers/stop", { id: groupId }),
  saveScene: (s: { id?: string; name?: string; color?: string; fadeMs?: number; capture?: boolean }) =>
    call<{ id: string }>("POST", "scenes/save", { capture: false, ...s }),
  recallScene: (id: string) => call("POST", "scenes/recall", { id }),
  removeScene: (id: string) => call("POST", "scenes/remove", { id }),
  orderScenes: (ids: string[]) => call("POST", "scenes/order", ids),
  sceneTemplates: () => call<SceneTemplateInfo[]>("GET", "scene-templates"),
  addSceneTemplates: (keys: string[], names: Record<string, string>) => call<{ added: number; skipped: number }>("POST", "scene-templates/add", { keys, names }),
  saveTimeline: (t: Timeline) => call<{ id: string }>("POST", "timelines/save", t),
  removeTimeline: (id: string) => call("POST", "timelines/remove", { id }),
  playTimeline: (id: string, fromMs?: number) => call("POST", "timeline/play", { id, fromMs }),
  pauseTimeline: () => call("POST", "timeline/pause"),
  stopTimeline: () => call("POST", "timeline/stop"),
  seekTimeline: (ms: number) => call("POST", "timeline/seek", { ms }),
  goTimeline: (id: string) => call("POST", "timeline/go", { id }),
  nextTimeline: (id: string) => call("POST", "timeline/next", { id }),
  prevTimeline: (id: string) => call("POST", "timeline/prev", { id }),
  uploadAudio: async (file: File) => {
    const fd = new FormData();
    fd.append("file", file);
    const res = await fetch("api/audio/upload", { method: "POST", body: fd, headers: headers(false) });
    if (!res.ok) throw new Error(await res.text());
    return (await res.json()) as { file: string; lengthMs: number };
  },
  mode: (mode: OutputMode) => call("POST", "mode", { mode }),
  effect: (effect: EffectSettings, start: boolean) => call("POST", "effect", { effect, start }),
  fader: (id: string, channel: number, value: number) => call("POST", "fader", { id, channel, value }),
  fadersAll: (ids: string[], value: number) => call("POST", "faders/all", { ids, value }),
  fadersFromOutput: () => call("POST", "faders/from-output"),
  identify: (id: string, on: boolean) => call("POST", "identify", { id, on }),
  customColors: (colors: string[]) => call("PUT", "custom-colors", colors),
  settings: (s: AppSettings) => call<{ note: string | null }>("PUT", "settings", s),
  reconnect: () => call("POST", "output/reconnect"),
  saveChase: (c: CustomChase) => call<{ id: string }>("POST", "chases/save", c),
  removeChase: (id: string) => call("POST", "chases/remove", { id }),
  orderChases: (ids: string[]) => call("POST", "chases/order", ids),
  reportInfo: () => call<{ crashes: number; lastCrash: string | null }>("GET", "report/info"),
  saveReport: (note: string) => call<{ path: string }>("POST", "report/save", { note }),
  update: () => call<import("./components/UpdateCard").UpdateInfo>("GET", "update"),
  checkUpdate: () => call<import("./components/UpdateCard").UpdateInfo>("POST", "update/check"),
  installUpdate: () => call<import("./components/UpdateCard").UpdateInfo>("POST", "update/install"),
  pair: (code: string, name: string) => call<{ ok: boolean }>("POST", "pair", { code, name }),
  pairStatus: () => call<{ required: boolean; paired: boolean }>("GET", "pair"),
  pairCode: () => call<{ code: string }>("GET", "pair/code"),
  newPairCode: () => call<{ code: string }>("POST", "pair/new-code"),
  removeDevice: (id: string) => call("POST", "devices/remove", { id }),
  phone: () => call<{ allowNetwork: boolean; port: number; hostname: string; firewall: boolean; fixedName: string; fixedNameStatus: string;
    addresses: { ip: string; network: string; preferred: boolean }[]; clients: { ip: string; since: string }[] }>("GET", "phone"),
  phoneFirewall: () => call<{ result: string }>("POST", "phone/firewall"),
  saveSchedule: (s: ScheduleSettings) => call("PUT", "schedule", s),
  scheduleInfo: () => call<{ now: string; sunrise: string | null; sunset: string | null;
    upcoming: { id: string; name: string; command: string; at: string }[];
    last: { name: string; command: string; result: string; at: string } | null }>("GET", "schedule/info"),
  runSchedule: (id: string) => call<{ result: string }>("POST", "schedule/run", { id }),
  checkCommand: (command: string) => call<{ ok: string | null }>("GET", "schedule/check?command=" + encodeURIComponent(command)),
  screenSync: (s: ScreenSyncSettings) => call("PUT", "screen-sync", s),
  screenInfo: () => call<{ monitors: string[]; fresh: boolean; columns: string[]; camera: string; cameraError: string | null;
    grid: { width: number; height: number; cells: string[] } }>("GET", "screen/info"),
  cameras: () => call<{ cameras: string[]; status: string; error: string | null }>("GET", "camera/devices"),
  mqttStatus: () => call<{ status: string; error: string | null }>("GET", "mqtt/status"),
  usbPower: () => call<{ result: string }>("POST", "output/usb-power"),
  recover: (deep = false) => call<{ connected: boolean; devicePresent: boolean; cancelled?: boolean; restart?: string | null; error?: string | null }>("POST", "output/recover", { deep }),
  log: async () => (await fetch("api/log")).text(),
  networkAddresses: () => call<string[]>("GET", "network/addresses"),
  strobe: (s: { active?: boolean; speed?: number }) => call("POST", "strobe", s),
  test: (channels: Record<number, number>, exclusive: boolean) => call("POST", "test", { channels, exclusive }),
  walk: (intervalMs: number) => call("POST", "test/walk", { intervalMs }),
  clearTest: () => call("POST", "test/clear"),
  controlInfo: () => call<{ commands: string[]; addresses: string[] }>("GET", "control/info"),
  midiDevices: () => call<string[]>("GET", "midi/devices"),
  midiLearn: () => call("POST", "midi/learn"),
  audioDevices: () => call<{ id: string; name: string }[]>("GET", "audio/devices"),
  music: (m: MusicSettings) => call("PUT", "music", m),
  tap: () => call<{ bpm: number }>("POST", "tap"),
  bpm: (bpm: number) => call<{ bpm: number }>("POST", "bpm", { bpm }),
  reloadLibrary: () => call("POST", "library/reload"),
};

/** Calls fn at most every `ms`, always delivering the latest arguments (for sliders). */
export function throttle<A extends unknown[]>(fn: (...a: A) => void, ms = 40) {
  let last = 0;
  let timer: ReturnType<typeof setTimeout> | null = null;
  let pending: A | null = null;
  return (...a: A) => {
    pending = a;
    const wait = last + ms - Date.now();
    if (wait <= 0) {
      last = Date.now();
      fn(...a);
      pending = null;
    } else if (!timer) {
      timer = setTimeout(() => {
        timer = null;
        last = Date.now();
        if (pending) fn(...pending);
        pending = null;
      }, wait);
    }
  };
}
