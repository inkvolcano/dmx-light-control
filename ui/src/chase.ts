// Own chases: types, a preview renderer that matches the engine (src/DmxLicht.Core/Effects/CustomChase.cs),
// and the tools/templates of the chase editor.

export type ChaseDirection = "forward" | "reverse" | "bounce" | "random";
export type ChaseMapping = "repeat" | "stretch" | "mirror" | "once";

export interface ChaseStep { colors: (string | null)[]; durationMs?: number | null; level: number; fadePercent?: number | null }
export interface CustomChase {
  id: string; name: string; columns: number; stepMs: number; fadePercent: number;
  direction: ChaseDirection; mapping: ChaseMapping; repeatOffset: number;
  syncToBeat: boolean; beatsPerStep: number; speedPercent: number;
  background?: string | null; audioLevel: boolean; steps: ChaseStep[];
}

export const newId = () => Math.random().toString(16).slice(2, 10).padEnd(8, "0");

export function emptyStep(cols: number): ChaseStep { return { colors: Array(cols).fill(null), durationMs: null, level: 100, fadePercent: null }; }

export function newChase(name: string, cols = 4, color = "#FF0000"): CustomChase {
  return { ...runner(cols, color), id: newId(), name };
}

const base = (cols: number): Omit<CustomChase, "steps"> => ({
  id: "", name: "", columns: cols, stepMs: 300, fadePercent: 0, direction: "forward", mapping: "repeat", repeatOffset: 0,
  syncToBeat: false, beatsPerStep: 1, speedPercent: 100, background: null, audioLevel: false,
});

// ---------------------------------------------------------------- rendering (preview)

function shuffle(k: number, seed: number) {
  // small deterministic shuffle; does not need to equal the engine's order, just look the same
  const o = Array.from({ length: k }, (_, i) => i);
  let s = (seed * 2654435761) >>> 0 || 1;
  for (let i = k - 1; i > 0; i--) { s = (s * 1103515245 + 12345) >>> 0; const j = s % (i + 1); [o[i], o[j]] = [o[j], o[i]]; }
  return o;
}

export function order(c: CustomChase, cycle: number): number[] {
  const k = c.steps.length, f = Array.from({ length: k }, (_, i) => i);
  switch (c.direction) {
    case "reverse": return f.reverse();
    case "bounce": return k > 2 ? [...f, ...f.slice(1, k - 1).reverse()] : f;
    case "random": {
      const o = shuffle(k, cycle);
      if (k > 2 && o[0] === shuffle(k, cycle - 1)[k - 1]) [o[0], o[1]] = [o[1], o[0]];
      return o;
    }
    default: return f;
  }
}

const duration = (c: CustomChase, i: number) => Math.max(20, c.steps[i].durationMs ?? c.stepMs);

/** Absolute step counter and progress in that step. */
export function position(c: CustomChase, ms: number, beat: number): { counter: number; frac: number } {
  const len = order(c, 0).length;
  const speed = Math.min(1000, Math.max(5, c.speedPercent)) / 100;
  if (c.syncToBeat) {
    const pos = Math.max(0, beat) * speed / Math.max(0.125, c.beatsPerStep);
    return { counter: Math.floor(pos), frac: pos - Math.floor(pos) };
  }
  const scaled = Math.floor(ms * speed);
  const total = order(c, 0).reduce((a, i) => a + duration(c, i), 0);
  const cycle = Math.floor(scaled / total);
  let t = scaled % total, idx = 0;
  const o = order(c, cycle);
  while (idx < len - 1 && t >= duration(c, o[idx])) { t -= duration(c, o[idx]); idx++; }
  return { counter: cycle * len + idx, frac: Math.min(1, t / duration(c, o[idx])) };
}

export function stepAt(c: CustomChase, counter: number) {
  const len = order(c, 0).length;
  const cycle = Math.floor(counter / len);
  return order(c, cycle)[counter - cycle * len];
}

export function mapLamp(c: CustomChase, lamp: number, lamps: number): { col: number; rep: number } {
  const cols = Math.max(1, c.columns);
  switch (c.mapping) {
    case "stretch": return { col: Math.floor((lamp * cols) / Math.max(1, lamps)), rep: 0 };
    case "mirror": { const j = lamp < Math.floor((lamps + 1) / 2) ? lamp : lamps - 1 - lamp; return { col: j % cols, rep: Math.floor(j / cols) }; }
    case "once": return { col: lamp < cols ? lamp : -1, rep: 0 };
    default: return { col: lamp % cols, rep: Math.floor(lamp / cols) };
  }
}

const valid = (h: string | null | undefined) => (h && /^#[0-9a-f]{6}$/i.test(h) ? h : null);
const rgb = (h: string) => { const n = parseInt(h.slice(1), 16); return [(n >> 16) & 255, (n >> 8) & 255, n & 255]; };
const css = (r: number, g: number, b: number, l: number) => `rgb(${Math.round(r * l)},${Math.round(g * l)},${Math.round(b * l)})`;

/** Colours of the lamps at this moment (CSS colours, brightness included) and the step each lamp is in. */
export function render(c: CustomChase, ms: number, lamps: number, beat: number, audioLevel = 1): { colors: string[]; steps: number[] } {
  const n = Math.max(1, lamps);
  if (!c.steps.length) return { colors: Array(n).fill("#000"), steps: Array(n).fill(-1) };
  const { counter, frac } = position(c, ms, beat);
  const bg = valid(c.background);
  const gain = c.audioLevel ? Math.min(1, 0.1 + 0.9 * audioLevel) : 1;
  const colors: string[] = [], steps: number[] = [];
  for (let i = 0; i < n; i++) {
    const { col, rep } = mapLamp(c, i, n);
    if (col < 0) { colors.push("#000"); steps.push(-1); continue; }
    const at = counter - rep * c.repeatOffset;
    const ci = stepAt(c, at), cur = c.steps[ci], next = c.steps[stepAt(c, at + 1)];
    steps.push(ci);
    const f = Math.min(100, Math.max(0, cur.fadePercent ?? c.fadePercent)) / 100;
    const mix = f <= 0 || frac <= 1 - f ? 0 : (frac - (1 - f)) / f;
    const a = valid(cur.colors[col]) ?? bg, b = valid(next.colors[col]) ?? bg;
    const la = cur.level / 100, lb = next.level / 100;
    if (mix <= 0) { colors.push(a ? css(...(rgb(a) as [number, number, number]), la * gain) : "#000"); continue; }
    const from = rgb(a ?? b ?? "#000000"), to = rgb(b ?? a ?? "#000000");
    const lvl = ((a ? (1 - mix) * la : 0) + (b ? mix * lb : 0)) * gain;
    colors.push(css(from[0] + (to[0] - from[0]) * mix, from[1] + (to[1] - from[1]) * mix, from[2] + (to[2] - from[2]) * mix, lvl));
  }
  return { colors, steps };
}

// ---------------------------------------------------------------- tools

export const withColumns = (c: CustomChase, cols: number): CustomChase => {
  cols = Math.min(64, Math.max(1, cols));
  return { ...c, columns: cols, steps: c.steps.map((s) => ({ ...s, colors: Array.from({ length: cols }, (_, i) => s.colors[i] ?? null) })) };
};
const mapSteps = (c: CustomChase, f: (s: ChaseStep, i: number) => ChaseStep): CustomChase => ({ ...c, steps: c.steps.map(f) });
const rot = <T,>(a: T[], k: number) => a.map((_, i) => a[(((i - k) % a.length) + a.length) % a.length]);

export const tools = {
  /** Every row one column further: the pattern moves left/right. */
  shift: (c: CustomChase, dir: 1 | -1) => mapSteps(c, (s) => ({ ...s, colors: rot(s.colors, dir) })),
  mirror: (c: CustomChase) => mapSteps(c, (s) => ({ ...s, colors: [...s.colors].reverse() })),
  reverseSteps: (c: CustomChase) => ({ ...c, steps: [...c.steps].reverse() }),
  /** Lit cells off, off cells in the brush colour. */
  invert: (c: CustomChase, color: string) => mapSteps(c, (s) => ({ ...s, colors: s.colors.map((x) => (x ? null : color)) })),
  /** Every lit cell gets the brush colour. */
  recolor: (c: CustomChase, color: string) => mapSteps(c, (s) => ({ ...s, colors: s.colors.map((x) => (x ? color : null)) })),
  clear: (c: CustomChase) => mapSteps(c, (s) => ({ ...s, colors: s.colors.map(() => null) })),
  /** Adds a shifted copy of every step: a pattern of N steps becomes a moving pattern. */
  addShiftedCopies: (c: CustomChase) => {
    const steps: ChaseStep[] = [];
    for (let k = 0; k < c.columns; k++) for (const s of c.steps) steps.push({ ...s, colors: rot(s.colors, k) });
    return { ...c, steps: steps.slice(0, 256) };
  },
};

// ---------------------------------------------------------------- templates

export const TEMPLATES = ["runner", "bounce", "alternate", "buildUp", "buildDown", "outsideIn", "colorCycle", "rainbow", "random", "blink", "comet", "twoColors"] as const;
export type TemplateKey = (typeof TEMPLATES)[number];

/** The same colour, darker (for tails). */
export function dim(hex: string, f: number) {
  const [r, g, b] = rgb(hex);
  return "#" + [r, g, b].map((v) => Math.round(v * f).toString(16).padStart(2, "0")).join("").toUpperCase();
}

const RAINBOW = ["#FF0000", "#FF8000", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF", "#8000FF", "#FF00FF"];
const row = (cols: number, f: (i: number) => string | null): ChaseStep => ({ ...emptyStep(cols), colors: Array.from({ length: cols }, (_, i) => f(i)) });
const make = (cols: number, steps: ChaseStep[], extra: Partial<CustomChase> = {}): CustomChase => ({ ...base(cols), ...extra, steps });

function runner(cols: number, color: string) { return make(cols, Array.from({ length: cols }, (_, s) => row(cols, (i) => (i === s ? color : null)))); }

/** A new chase from a template, with the brush colours and column count. */
export function template(key: TemplateKey, cols: number, a: string, b: string, colors: string[]): CustomChase {
  const pal = colors.length ? colors : RAINBOW;
  switch (key) {
    case "runner": return runner(cols, a);
    case "bounce": return { ...runner(cols, a), direction: "bounce" };
    case "alternate": return make(cols, [row(cols, (i) => (i % 2 ? null : a)), row(cols, (i) => (i % 2 ? a : null))], { stepMs: 400 });
    case "buildUp": return make(cols, Array.from({ length: cols + 1 }, (_, s) => row(cols, (i) => (i < s ? a : null))), { stepMs: 250 });
    case "buildDown": return make(cols, Array.from({ length: cols + 1 }, (_, s) => row(cols, (i) => (i < cols - s ? a : null))), { stepMs: 250 });
    case "outsideIn": {
      const half = Math.ceil(cols / 2);
      return make(cols, Array.from({ length: half }, (_, s) => row(cols, (i) => (i === s || i === cols - 1 - s ? a : null))), { stepMs: 250, direction: "bounce" });
    }
    case "colorCycle": return make(cols, pal.slice(0, 8).map((c) => row(cols, () => c)), { stepMs: 800, fadePercent: 60 });
    case "rainbow": return make(cols, Array.from({ length: cols }, (_, s) => row(cols, (i) => RAINBOW[((i + s) * 8 / cols | 0) % 8])), { stepMs: 200, fadePercent: 100 });
    case "random": {
      let seed = 7;
      const rnd = () => ((seed = (seed * 1103515245 + 12345) >>> 0) / 4294967296);
      return make(cols, Array.from({ length: 8 }, () => row(cols, () => (rnd() < 0.4 ? pal[(rnd() * pal.length) | 0] : null))), { stepMs: 200, direction: "random" });
    }
    case "blink": return make(cols, [row(cols, () => a), row(cols, () => null)], { stepMs: 300 });
    case "comet": {
      // a head with a tail that fades: same colour at 100/50/20 %
      const tail1 = dim(a, 0.4), tail2 = dim(a, 0.12);
      const steps = Array.from({ length: cols }, (_, s) => row(cols, (i) => (i === s ? a : i === s - 1 ? tail1 : i === s - 2 ? tail2 : null)));
      return make(cols, steps, { stepMs: 120, fadePercent: 50 });
    }
    case "twoColors": return make(cols, [row(cols, (i) => (i % 2 ? b : a)), row(cols, (i) => (i % 2 ? a : b))], { stepMs: 500, fadePercent: 40 });
  }
}
