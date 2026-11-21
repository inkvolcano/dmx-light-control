import { useEffect, useRef, useState } from "react";
import type { ScreenSyncSettings } from "../api";
import { useT } from "../i18n";

/** Default spot for lamp i of n: on a row through the middle (same as the engine). */
export function defaultPoint(i: number, n: number): [number, number] {
  return [(i + 0.5) / Math.max(1, n), 0.5];
}

/** Every lamp's spot: its marker, or the default row. */
export function pointsFor(set: ScreenSyncSettings, n: number): number[] {
  const out: number[] = [];
  for (let i = 0; i < n; i++) {
    const p = set.points;
    const x = p && p.length > 2 * i + 1 ? p[2 * i] : NaN, y = p && p.length > 2 * i + 1 ? p[2 * i + 1] : NaN;
    const [dx, dy] = defaultPoint(i, n);
    out.push(Number.isFinite(x) ? x : dx, Number.isFinite(y) ? y : dy);
  }
  return out;
}

/**
 * Markers on the picture: each numbered marker is a lamp (in lamp order); drag it to the spot whose colour that
 * lamp should take. The square shows the measured area; the marker is filled with the lamp's live colour.
 * On this PC the background is a live picture of what screen sync sees; elsewhere the small colour grid.
 */
export function PointsEditor({ set, lamps, colors, grid, onChange }: {
  set: ScreenSyncSettings; lamps: number; colors: string[];
  grid: { width: number; height: number; cells: string[] } | null;
  onChange: (points: number[]) => void;
}) {
  const t = useT();
  const box = useRef<HTMLDivElement>(null);
  const [p, setP] = useState<number[]>(() => pointsFor(set, lamps));
  const latest = useRef(p);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const drag = useRef<number | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [src, setSrc] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const local = ["127.0.0.1", "localhost", "[::1]"].includes(location.hostname);
  const key = (set.points ?? []).join(",") + "|" + lamps;
  useEffect(() => { if (drag.current == null) { const n = pointsFor(set, lamps); setP(n); latest.current = n; } }, [key]); // eslint-disable-line react-hooks/exhaustive-deps

  // live picture: next one only after the previous arrived
  useEffect(() => {
    if (!local) return;
    let alive = true, url: string | null = null;
    setSrc(null); setError(null);   // a new source: never keep showing the old picture
    (async () => {
      while (alive) {
        try {
          const r = await fetch("api/screen/snapshot?t=" + Date.now());
          if (!r.ok) { setError(await r.text()); await new Promise((x) => setTimeout(x, 1500)); continue; }
          const blob = await r.blob();
          if (!alive) break;
          const u = URL.createObjectURL(blob);
          setSrc(u); setError(null);
          if (url) URL.revokeObjectURL(url);
          url = u;
        } catch { await new Promise((x) => setTimeout(x, 1500)); }
        await new Promise((x) => setTimeout(x, 400));
      }
    })();
    return () => { alive = false; if (url) URL.revokeObjectURL(url); };
  }, [local, set.source, set.monitor, (set.corners ?? []).join(",")]); // eslint-disable-line react-hooks/exhaustive-deps

  // arrow keys move the selected marker (Shift = bigger steps)
  useEffect(() => {
    if (selected == null) return;
    let timer: number | undefined;
    // clicking anywhere outside the picture lets go of the marker (and of the arrow keys)
    const outside = (e: PointerEvent) => { if (!box.current?.contains(e.target as Node)) setSelected(null); };
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement)?.tagName;
      if (tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA") return;
      const d = e.shiftKey ? 0.02 : 0.004;
      const dx = e.key === "ArrowLeft" ? -d : e.key === "ArrowRight" ? d : 0, dy = e.key === "ArrowUp" ? -d : e.key === "ArrowDown" ? d : 0;
      if (!dx && !dy) { if (e.key === "Escape") setSelected(null); return; }
      e.preventDefault();
      const n = [...latest.current];
      n[selected * 2] = Math.min(1, Math.max(0, Math.round((n[selected * 2] + dx) * 1000) / 1000));
      n[selected * 2 + 1] = Math.min(1, Math.max(0, Math.round((n[selected * 2 + 1] + dy) * 1000) / 1000));
      latest.current = n; setP(n);
      window.clearTimeout(timer);
      timer = window.setTimeout(() => { timer = undefined; onChangeRef.current(latest.current); }, 300);
    };
    window.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", outside);
    return () => {
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", outside);
      // a nudge still waiting is saved now, not dropped (Escape or leaving the page right after it)
      if (timer !== undefined) { window.clearTimeout(timer); onChangeRef.current(latest.current); }
    };
  }, [selected]); // eslint-disable-line react-hooks/exhaustive-deps

  const point = (e: React.PointerEvent) => {
    const r = box.current!.getBoundingClientRect();
    return [Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)), Math.min(1, Math.max(0, (e.clientY - r.top) / r.height))];
  };
  const down = (i: number) => (e: React.PointerEvent) => {
    e.preventDefault(); e.stopPropagation();
    drag.current = i; setSelected(i);
    try { (e.target as Element).setPointerCapture(e.pointerId); } catch { /* moves still reach the frame */ }
  };
  const move = (e: React.PointerEvent) => {
    if (drag.current == null) return;
    const [x, y] = point(e);
    const n = [...latest.current];
    n[drag.current * 2] = Math.round(x * 1000) / 1000;
    n[drag.current * 2 + 1] = Math.round(y * 1000) / 1000;
    latest.current = n; setP(n);
  };
  const up = () => { if (drag.current == null) return; drag.current = null; onChangeRef.current(latest.current); };
  // click on the picture: the selected marker jumps there
  const place = (e: React.PointerEvent) => {
    if (selected == null || drag.current != null) return;
    const [x, y] = point(e);
    const n = [...latest.current];
    n[selected * 2] = Math.round(x * 1000) / 1000; n[selected * 2 + 1] = Math.round(y * 1000) / 1000;
    latest.current = n; setP(n); onChangeRef.current(n);
  };

  const size = Math.min(50, Math.max(1, set.pointSize ?? 8));   // % of the width; square on a 16:9 picture
  const dot = lamps > 40 ? 16 : lamps > 16 ? 20 : 26;
  return (
    <div className="stack" style={{ gap: 6 }}>
      <div className="cam-frame pts-frame" ref={box} onPointerMove={move} onPointerUp={up} onPointerCancel={up} onPointerDown={place}>
        {local && src
          ? <img src={src} alt="" draggable={false} />
          : grid && grid.width > 0
            ? <div className="pts-grid" style={{ gridTemplateColumns: `repeat(${grid.width}, 1fr)` }}>
                {grid.cells.map((c, i) => <span key={i} style={{ background: c }} />)}
              </div>
            : <div className="cam-wait">{error ?? t("pts.noPicture")}</div>}
        {error && src && <div className="cam-error">{error}</div>}
        {Array.from({ length: lamps }, (_, i) => (
          <span key={"a" + i} className={"pts-area" + (selected === i ? " sel" : "")}
            style={{ left: `${p[i * 2] * 100}%`, top: `${p[i * 2 + 1] * 100}%`, width: `${size}%`, height: `${size * 16 / 9}%` }} />
        ))}
        {Array.from({ length: lamps }, (_, i) => (
          <span key={"m" + i} className={"pts-marker" + (selected === i ? " sel" : "")} title={t("pts.lamp", { n: i + 1 })}
            style={{ left: `${p[i * 2] * 100}%`, top: `${p[i * 2 + 1] * 100}%`, width: dot, height: dot, margin: `${-dot / 2}px 0 0 ${-dot / 2}px`,
              background: colors[i] ?? "#000", fontSize: dot > 20 ? 12 : 10 }}
            onPointerDown={down(i)}>{i + 1}</span>
        ))}
      </div>
      <p className="hint">{selected != null ? t("pts.selectedHint", { n: selected + 1 }) : t("pts.dragHint")}</p>
      {!local && <p className="hint">{t("pts.onlyHere")}</p>}
    </div>
  );
}
