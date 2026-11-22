import { useEffect, useRef } from "react";
import { api, type EffectSettings } from "../api";

// loops already fetched, per effect and the settings that change its look
const cache = new Map<string, Promise<string[][]>>();
const keyOf = (index: number, lamps: number, fx: EffectSettings) =>
  [index, lamps, fx.colorA, fx.colorB, fx.stepMs, fx.onMs, fx.offMs, fx.delayMs, fx.colorPerLamp ? fx.lampColors.join("") : ""].join("|");
const load = (index: number, lamps: number, key: string) => {
  let p = cache.get(key);
  if (!p) {
    p = api.effectPreview(index, lamps);
    cache.set(key, p);
    p.catch(() => cache.delete(key));
    if (cache.size > 600) cache.delete(cache.keys().next().value as string);
  }
  return p;
};

// one clock for every tile on screen (25 frames a second), so many tiles cost one timer
const players = new Set<(frame: number) => void>();
let frame = 0, timer: number | undefined;
const play = (f: (frame: number) => void) => {
  players.add(f);
  if (timer === undefined) timer = window.setInterval(() => { frame++; players.forEach((p) => p(frame)); }, 40);
  return () => { players.delete(f); if (!players.size && timer !== undefined) { clearInterval(timer); timer = undefined; } };
};

/**
 * The moving example on an effect tile: a few dots that play a short loop of the effect, rendered by the app
 * itself (exactly what the lamps would do). It plays while the tile is on screen; tiles scrolled away rest.
 * The dots are coloured directly (no React render per frame).
 */
export function FxPreview({ index, fx, lamps = 4 }: { index: number; fx: EffectSettings; lamps?: number }) {
  const box = useRef<HTMLSpanElement>(null);
  const key = keyOf(index, lamps, fx);
  useEffect(() => {
    const el = box.current;
    if (!el) return;
    let frames: string[][] | null = null, stop: (() => void) | null = null, alive = true;
    const dots = () => Array.from(el.children) as HTMLElement[];
    const paint = (n: number) => {
      if (!frames) return;
      const row = frames[n % frames.length];
      dots().forEach((d, i) => { const c = row[i] ?? "#000"; d.style.background = c; d.style.boxShadow = `0 0 6px ${c}`; });
    };
    const seen = new IntersectionObserver(([e]) => {
      if (e.isIntersecting) {
        load(index, lamps, key).then((f) => { if (!alive) return; frames = f; paint(frame); if (!stop) stop = play(paint); }).catch(() => {});
      } else if (stop) { stop(); stop = null; }
    });
    seen.observe(el);
    return () => { alive = false; seen.disconnect(); if (stop) stop(); };
  }, [key]); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <span className="fx-dots" ref={box} aria-hidden="true">
      {Array.from({ length: lamps }, (_, i) => <span key={i} />)}
    </span>
  );
}
