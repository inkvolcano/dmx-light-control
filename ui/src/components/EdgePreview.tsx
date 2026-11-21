import type { ScreenSyncSettings } from "../api";

type Side = "left" | "top" | "right" | "bottom";

/** Like C#'s Math.Round (banker's rounding), so the preview splits lamps exactly like the engine. */
function roundHalfEven(x: number) {
  const f = Math.floor(x), d = x - f;
  if (Math.abs(d - 0.5) > 1e-9) return Math.round(x);
  return f % 2 === 0 ? f : f + 1;
}

/** Same layout as the engine (ScreenFeed.EdgeLayout): clockwise from the bottom of the left side. */
export function edgeLayout(set: ScreenSyncSettings, lamps: number): { side: Side; index: number; count: number }[] {
  let l: number, t: number, r: number, b: number;
  if (set.edgeAuto ?? true) {
    let side = roundHalfEven((lamps * 9) / 34);
    side = lamps >= 3 ? Math.max(1, side) : 0;
    [l, t, r, b] = [side, Math.max(0, lamps - 2 * side), side, 0];
  } else [l, t, r, b] = [set.edgeLeft ?? 0, set.edgeTop ?? 0, set.edgeRight ?? 0, set.edgeBottom ?? 0];
  const chain: { side: Side; index: number; count: number }[] = [];
  for (let i = 0; i < l; i++) chain.push({ side: "left", index: i, count: l });
  for (let i = 0; i < t; i++) chain.push({ side: "top", index: i, count: t });
  for (let i = 0; i < r; i++) chain.push({ side: "right", index: i, count: r });
  for (let i = 0; i < b; i++) chain.push({ side: "bottom", index: i, count: b });
  if (set.edgeReverse) chain.reverse();
  if (!chain.length) return chain;
  const s = (((set.edgeStart ?? 0) % chain.length) + chain.length) % chain.length;
  return [...chain.slice(s), ...chain.slice(0, s)];
}

/**
 * The picture as the app sees it (a small grid), the edge strips, and the lamps around it with their
 * number and their current colour, so you see which lamp follows which part of the edge.
 */
export function EdgePreview({ set, lamps, grid, colors }: {
  set: ScreenSyncSettings; lamps: number;
  grid: { width: number; height: number; cells: string[] } | null; colors: string[];
}) {
  const layout = edgeLayout(set, lamps);
  const depth = Math.min(50, Math.max(2, set.edgeDepth ?? 15));
  const pos = (i: number) => {
    const p = layout[i % Math.max(1, layout.length)];
    if (!p) return { left: "50%", top: "50%" };
    const f = ((p.index + 0.5) / p.count) * 100;
    switch (p.side) {
      case "left": return { left: "-7%", top: `${100 - f}%` };
      case "top": return { left: `${f}%`, top: "-11%" };
      case "right": return { left: "107%", top: `${f}%` };
      default: return { left: `${100 - f}%`, top: "111%" };
    }
  };
  return (
    <div className="edge-preview">
      <div className="edge-tv" style={grid && grid.width ? { gridTemplateColumns: `repeat(${grid.width}, 1fr)` } : undefined}>
        {grid?.cells.map((c, i) => <span key={i} style={{ background: c }} />)}
        <div className="edge-depth" style={{ inset: `${depth}%` }} />
        {Array.from({ length: lamps }, (_, i) => (
          <b key={i} className="edge-lamp" style={{ ...pos(i), background: colors[i] ?? "#222" }}>{i + 1}</b>
        ))}
      </div>
    </div>
  );
}
