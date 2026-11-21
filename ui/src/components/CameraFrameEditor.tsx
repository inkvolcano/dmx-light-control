import { useEffect, useRef, useState } from "react";
import { useT } from "../i18n";

/** Same perspective mapping as the engine (CameraFrame.Perspective): unit square -> quad TL, TR, BR, BL. */
export function perspective(q: number[]) {
  const [x0, y0, x1, y1, x2, y2, x3, y3] = q;
  const sx = x0 - x1 + x2 - x3, sy = y0 - y1 + y2 - y3;
  const dx1 = x1 - x2, dx2 = x3 - x2, dy1 = y1 - y2, dy2 = y3 - y2;
  const den = dx1 * dy2 - dx2 * dy1;
  let a: number, b: number, d: number, e: number, g = 0, h = 0;
  if ((Math.abs(sx) < 1e-9 && Math.abs(sy) < 1e-9) || Math.abs(den) < 1e-12) { a = x1 - x0; b = x3 - x0; d = y1 - y0; e = y3 - y0; }
  else {
    g = (sx * dy2 - dx2 * sy) / den; h = (dx1 * sy - sx * dy1) / den;
    a = x1 - x0 + g * x1; b = x3 - x0 + h * x3; d = y1 - y0 + g * y1; e = y3 - y0 + h * y3;
  }
  return (u: number, v: number): [number, number] => {
    let w = g * u + h * v + 1;
    if (Math.abs(w) < 1e-9) w = 1e-9;
    return [(a * u + b * v + x0) / w, (d * u + e * v + y0) / w];
  };
}

const clamp = (v: number) => Math.round(Math.min(1, Math.max(0, v)) * 1000) / 1000;

/** Projector-style adjustments on the four corners. */
function adjust(c: number[], kind: string, dir: number, aspect: number): number[] {
  const n = [...c];
  const cx = (c[0] + c[2] + c[4] + c[6]) / 4, cy = (c[1] + c[3] + c[5] + c[7]) / 4;
  const k = 0.005 * dir;
  switch (kind) {
    case "keyV":   // top narrower / wider than bottom
      n[0] += k; n[2] -= k; n[6] -= k; n[4] += k; break;
    case "keyH":   // left edge shorter / taller than right
      n[1] += k; n[7] -= k; n[3] -= k; n[5] += k; break;
    case "rotate": {
      const t = (0.5 * Math.PI / 180) * dir;
      for (let i = 0; i < 4; i++) {
        const x = (c[i * 2] - cx) * aspect, y = c[i * 2 + 1] - cy;
        n[i * 2] = cx + (x * Math.cos(t) - y * Math.sin(t)) / aspect;
        n[i * 2 + 1] = cy + x * Math.sin(t) + y * Math.cos(t);
      }
      break;
    }
    case "size":
      for (let i = 0; i < 4; i++) { n[i * 2] = cx + (c[i * 2] - cx) * (1 + 0.01 * dir); n[i * 2 + 1] = cy + (c[i * 2 + 1] - cy) * (1 + 0.01 * dir); }
      break;
    case "x": for (let i = 0; i < 4; i++) n[i * 2] += k; break;
    case "y": for (let i = 0; i < 4; i++) n[i * 2 + 1] += k; break;
  }
  return n.map(clamp);
}

/**
 * Camera picture with a frame of four corners you drag onto the TV. The picture refreshes a few times
 * per second (only on this PC). Corners are 0..1: TL x,y, TR x,y, BR x,y, BL x,y.
 */
export function CameraFrameEditor({ corners, onChange }: { corners: number[]; onChange: (c: number[]) => void }) {
  const t = useT();
  const [c, setC] = useState(corners);
  const [src, setSrc] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const box = useRef<HTMLDivElement>(null);
  const latest = useRef(c);
  latest.current = c;
  const drag = useRef<number | null>(null);
  const [selected, setSelected] = useState<number | null>(null);
  const [aspect, setAspect] = useState(16 / 9);
  const [detect, setDetect] = useState<string | null>(null);
  const apply = (n: number[]) => { setC(n); onChange(n); };
  const tool = (kind: string, dir: number) => apply(adjust(c, kind, dir, aspect));
  const autoFind = async () => {
    setDetect(t("cam.searching"));
    try {
      const r = await fetch("api/camera/detect", { method: "POST" });
      if (!r.ok) { setDetect(await r.text()); return; }
      const j = await r.json() as { corners: number[] | null };
      if (j.corners) { apply(j.corners.map(clamp)); setDetect(t("cam.found")); }
      else setDetect(t("cam.notFound"));
    } catch (e) { setDetect(String(e)); }
  };
  // arrow keys move the selected corner (Shift = bigger steps)
  useEffect(() => {
    if (selected == null) return;
    const key = (e: KeyboardEvent) => {
      const dx = e.key === "ArrowLeft" ? -1 : e.key === "ArrowRight" ? 1 : 0, dy = e.key === "ArrowUp" ? -1 : e.key === "ArrowDown" ? 1 : 0;
      if (!dx && !dy) { if (e.key === "Escape") setSelected(null); return; }
      e.preventDefault();
      const step = e.shiftKey ? 0.01 : 0.002;
      setC((cur) => {
        const n = [...cur];
        n[selected * 2] = clamp(n[selected * 2] + dx * step);
        n[selected * 2 + 1] = clamp(n[selected * 2 + 1] + dy * step);
        clearTimeout(keyTimer.current);
        keyTimer.current = window.setTimeout(() => onChange(n), 300);
        return n;
      });
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [selected]); // eslint-disable-line react-hooks/exhaustive-deps
  const keyTimer = useRef<number | undefined>(undefined);
  useEffect(() => { if (drag.current == null) setC(corners); }, [corners.join(",")]); // eslint-disable-line react-hooks/exhaustive-deps

  // refresh the picture: next one only after the previous arrived (no pile-up on a slow camera)
  useEffect(() => {
    let alive = true, url: string | null = null;
    const next = async () => {
      while (alive) {
        try {
          const r = await fetch("api/camera/snapshot?t=" + Date.now());
          if (!r.ok) { setError(await r.text()); await new Promise((x) => setTimeout(x, 1500)); continue; }
          const blob = await r.blob();
          if (!alive) break;
          const u = URL.createObjectURL(blob);
          setSrc(u); setError(null);
          if (url) URL.revokeObjectURL(url);
          url = u;
        } catch { await new Promise((x) => setTimeout(x, 1500)); }
        await new Promise((x) => setTimeout(x, 250));
      }
    };
    next();
    return () => { alive = false; if (url) URL.revokeObjectURL(url); };
  }, []);

  const point = (e: React.PointerEvent) => {
    const r = box.current!.getBoundingClientRect();
    return [Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)), Math.min(1, Math.max(0, (e.clientY - r.top) / r.height))];
  };
  const down = (i: number) => (e: React.PointerEvent) => {
    e.preventDefault();
    drag.current = i;
    setSelected(i);
    try { (e.target as Element).setPointerCapture(e.pointerId); } catch { /* not available: moves still reach the frame */ }
  };
  const move = (e: React.PointerEvent) => {
    if (drag.current == null) return;
    const [x, y] = point(e);
    const n = [...c];
    n[drag.current * 2] = Math.round(x * 1000) / 1000;
    n[drag.current * 2 + 1] = Math.round(y * 1000) / 1000;
    latest.current = n;
    setC(n);
  };
  const up = () => {
    if (drag.current == null) return;
    drag.current = null;
    onChange(latest.current);
  };
  const pts = [0, 1, 2, 3].map((i) => `${c[i * 2] * 100},${c[i * 2 + 1] * 100}`).join(" ");
  // perspective grid (thirds), like a projector's keystone test pattern
  const map = perspective(c);
  const gridLines: [number, number, number, number][] = [];
  for (const f of [1 / 3, 0.5, 2 / 3]) {
    const [ax, ay] = map(f, 0), [bx, by] = map(f, 1), [cx2, cy2] = map(0, f), [dx2, dy2] = map(1, f);
    gridLines.push([ax, ay, bx, by], [cx2, cy2, dx2, dy2]);
  }
  const labels = [t("cam.tl"), t("cam.tr"), t("cam.br"), t("cam.bl")];

  return (
    <div className="stack" style={{ gap: 6 }}>
      <div className="cam-frame" ref={box} onPointerMove={move} onPointerUp={up} onPointerCancel={up}>
        {src ? <img src={src} alt="" draggable={false} onLoad={(e) => { const im = e.currentTarget; if (im.naturalHeight) setAspect(im.naturalWidth / im.naturalHeight); }} />
          : <div className="cam-wait">{error ?? t("cam.starting")}</div>}
        <svg viewBox="0 0 100 100" preserveAspectRatio="none">
          <polygon points={pts} className="cam-poly" />
          {gridLines.map(([x1, y1, x2, y2], k) => (
            <line key={k} x1={x1 * 100} y1={y1 * 100} x2={x2 * 100} y2={y2 * 100} className={k === 2 ? "cam-mid" : "cam-grid"} />
          ))}
        </svg>
        {[0, 1, 2, 3].map((i) => (
          <span key={i} className={"cam-handle" + (selected === i ? " sel" : "")} style={{ left: `${c[i * 2] * 100}%`, top: `${c[i * 2 + 1] * 100}%` }}
            onPointerDown={down(i)} title={labels[i]}>{i + 1}</span>
        ))}
      </div>
      <div className="row">
        <button className="btn sm primary" onClick={autoFind}>{t("cam.findTv")}</button>
        <button className="btn sm ghost" onClick={() => apply([0.1, 0.1, 0.9, 0.1, 0.9, 0.9, 0.1, 0.9])}>{t("cam.reset")}</button>
        <button className="btn sm ghost" onClick={() => apply([0, 0, 1, 0, 1, 1, 0, 1])}>{t("cam.whole")}</button>
        {detect && <span className="hint">{detect}</span>}
      </div>
      <p className="hint">{t("cam.dragHint")} {t("cam.keysHint")}</p>
      <div className="keystone">
        {([["keyV", "cam.keyV"], ["keyH", "cam.keyH"], ["rotate", "cam.rotate"], ["size", "cam.size"], ["x", "cam.moveX"], ["y", "cam.moveY"]] as const).map(([k, label]) => (
          <div key={k} className="keystone-row">
            <span className="muted small">{t(label)}</span>
            <button className="btn sm" onClick={() => tool(k, -1)}>−</button>
            <button className="btn sm" onClick={() => tool(k, 1)}>+</button>
          </div>
        ))}
      </div>
    </div>
  );
}
