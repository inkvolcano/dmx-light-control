import type { AppSettings } from "./api";
import { useSyncExternalStore } from "react";
import { api, type Bootstrap, type ControlLive, type FullState, type MusicLive, type OutputStatus, type TimelineLive } from "./api";

// Two separate stores so the 20x/s live colours don't re-render every page.

type Listener = () => void;

function createStore<T>(initial: T) {
  let value = initial;
  const listeners = new Set<Listener>();
  return {
    get: () => value,
    set(next: T) {
      value = next;
      listeners.forEach((l) => l());
    },
    subscribe(l: Listener) {
      listeners.add(l);
      return () => listeners.delete(l);
    },
  };
}

export interface ShowSnapshot {
  boot: Bootstrap | null;
  full: FullState | null;
  online: boolean;
  /** This phone/tablet must be paired first (the app answered 401 "pair"). */
  needsPairing?: boolean;
}

export interface LiveSnapshot {
  heads: string[];
  output: OutputStatus;
  outputs: OutputStatus[];
  music: MusicLive;
  timeline: TimelineLive;
  test: { channels: Record<number, number>; exclusive: boolean; walk: string | null };
  frame: Uint8Array;
  control: ControlLive | null;
}

export const showStore = createStore<ShowSnapshot>({ boot: null, full: null, online: false });
export const liveStore = createStore<LiveSnapshot>({
  heads: [],
  output: {
    name: "uDMX", connected: false, fault: null, detail: null, connectedSince: null, reconnects: 0, frames: 0,
    errors: 0, recovered: 0, framesPerSecond: 0, mode: "", lastErrorAt: null, lastError: null,
  },
  outputs: [],
  music: {
    levels: { level: 0, bass: 0, mid: 0, high: 0, bpm: 0, beats: 0 },
    bpm: 120, beat: 0, following: false, listening: false, error: null,
  },
  timeline: { id: null, playing: false, positionMs: 0 },
  test: { channels: {}, exclusive: false, walk: null },
  frame: new Uint8Array(512),
  control: null,
});

function decode(b64: string | undefined) {
  if (!b64) return new Uint8Array(512);
  const s = atob(b64);
  const a = new Uint8Array(s.length);
  for (let i = 0; i < s.length; i++) a[i] = s.charCodeAt(i);
  return a;
}

export const useShow = () => useSyncExternalStore(showStore.subscribe, showStore.get);
export const useLive = () => useSyncExternalStore(liveStore.subscribe, liveStore.get);

// pairing: the pairing screen resolves this when the code was accepted
let pairedResolve: (() => void) | null = null;
export function pairingSucceeded() {
  showStore.set({ ...showStore.get(), needsPairing: false });
  pairedResolve?.();
  pairedResolve = null;
}
function waitForPairing() {
  showStore.set({ ...showStore.get(), needsPairing: true, online: true });
  return new Promise<void>((r) => (pairedResolve = r));
}

/** Loads the library/palette once, then keeps state live over a WebSocket (reconnects by itself). */
export async function connect() {
  // QR code from the PC: "?pair=123456" pairs this device at once
  const code = new URLSearchParams(location.search).get("pair");
  if (code) {
    try {
      const { deviceName } = await import("./pages/PairScreen");
      await api.pair(code, deviceName());
    } catch { /* old or used code: the pairing screen asks for the current one */ }
    const q = new URLSearchParams(location.search);
    q.delete("pair");
    history.replaceState(null, "", location.pathname + (q.toString() ? "?" + q : "") + location.hash);
  }
  for (;;) {
    try {
      const [boot, full] = await Promise.all([api.bootstrap(), api.state()]);
      showStore.set({ boot, full, online: true, needsPairing: false });
      break;
    } catch (e) {
      if (String(e).includes(": 401")) { await waitForPairing(); continue; }
      showStore.set({ ...showStore.get(), online: false });
      await new Promise((r) => setTimeout(r, 1000));
    }
  }
  openSocket();
}

function openSocket() {
  const url = new URL("ws", location.href);
  url.protocol = location.protocol === "https:" ? "wss:" : "ws:";
  const ws = new WebSocket(url);
  ws.onopen = () => showStore.set({ ...showStore.get(), online: true });
  ws.onmessage = (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.type === "state") showStore.set({ ...showStore.get(), full: msg.state, online: true });
    else if (msg.type === "live") liveStore.set({
      heads: msg.heads, output: msg.output, outputs: msg.outputs ?? [], music: msg.music, timeline: msg.timeline,
      test: msg.test ?? { channels: {}, exclusive: false, walk: null }, frame: decode(msg.frame), control: msg.control ?? null,
    });
  };
  ws.onclose = async () => {
    showStore.set({ ...showStore.get(), online: false });
    // removed from the paired devices on the PC? then pair again before reconnecting
    try {
      const p = await api.pairStatus();
      if (p.required && !p.paired) { await waitForPairing(); connect(); return; }
    } catch { /* app not reachable: just retry */ }
    setTimeout(openSocket, 1000);
  };
}

/**
 * Saves a part of the app settings. Saves are merged into the newest local copy, so a second save that
 * goes out before the server's update arrived does not put the first change back.
 */
let optimisticSettings: AppSettings | null = null;
export async function updateSettings(change: Partial<AppSettings>) {
  const base = optimisticSettings ?? showStore.get().full!.settings;
  const next = { ...base, ...change } as AppSettings;
  optimisticSettings = next;
  try { return await api.settings(next); }
  finally { if (optimisticSettings === next) optimisticSettings = null; }
}

/** Reloads the bootstrap data (after the fixture library changed). */
export async function reloadBootstrap() {
  const boot = await api.bootstrap();
  showStore.set({ ...showStore.get(), boot });
}
