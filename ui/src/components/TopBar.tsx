import { useEffect, useMemo, useState } from "react";
import { api, throttle } from "../api";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";
import { Icon } from "./Icon";
import { NowButton } from "./LiveRail";

/** The bar over every page. With the right column shown, tempo and "what runs" live there instead of here. */
// fade: 0 to 30 s on a curve, so short fades get most of the slider (0 = direct)
const fadeToPos = (ms: number) => Math.round(Math.sqrt(Math.min(30000, Math.max(0, ms)) / 30000) * 1000);
const posToFade = (p: number) => { const ms = Math.pow(p / 1000, 2) * 30000; return ms < 50 ? 0 : ms < 2000 ? Math.round(ms / 50) * 50 : Math.round(ms / 100) * 100; };

/** A small slider in the top bar: drag, the value shows next to it; follows changes from elsewhere when not dragged. */
function DragValue({ label, title, value, range, toPos, fromPos, show, onChange }: {
  label: string; title: string; value: number; range: [number, number];
  toPos: (v: number) => number; fromPos: (p: number) => number; show: (v: number) => string; onChange: (v: number) => void;
}) {
  const [v, setV] = useState(value);
  const [dragging, setDragging] = useState(false);
  useEffect(() => { if (!dragging) setV(value); }, [value, dragging]);
  return (
    <div className="master drag-value" title={title}>
      <label>{label}</label>
      <input type="range" min={range[0]} max={range[1]} value={toPos(v)}
        onPointerDown={() => setDragging(true)} onPointerUp={() => setDragging(false)} onBlur={() => setDragging(false)}
        onChange={(e) => { const n = fromPos(+e.target.value); setV(n); onChange(n); }} />
      <span className="val">{show(v)}</span>
    </div>
  );
}

/** The strobe speed as a thin bar inside the strobe button (follows changes from elsewhere when not dragged). */
function StrobeSpeed({ value, onChange, title }: { value: number; onChange: (v: number) => void; title: string }) {
  const [v, setV] = useState(value);
  const [dragging, setDragging] = useState(false);
  useEffect(() => { if (!dragging) setV(value); }, [value, dragging]);
  return (
    <input className="strobe-speed" type="range" min={1} max={100} value={v} title={title + ": " + v} aria-label={title}
      style={{ ["--fill" as string]: v + "%" }}
      onPointerDown={() => setDragging(true)} onPointerUp={() => setDragging(false)} onBlur={() => setDragging(false)}
      onChange={(e) => { const n = +e.target.value; setV(n); onChange(n); }} />
  );
}

export function TopBar({ rail }: { rail: boolean }) {
  const t = useT();
  const { full } = useShow();
  const { output, music } = useLive();
  const s = full?.state;
  const [bright, setBright] = useState(s?.brightnessPercent ?? 100);
  const [dragging, setDragging] = useState(false);
  const [strobing, setStrobing] = useState(false);
  const [recovering, setRecovering] = useState(false);
  const [update, setUpdate] = useState<string | null>(null);
  useEffect(() => {
    let alive = true;
    const load = () => api.update().then((u) => alive && setUpdate(u.available ? u.latest : null)).catch(() => {});
    const first = setTimeout(load, 20000);          // the app checks ~15 s after start
    const h = setInterval(load, 30 * 60 * 1000);
    return () => { alive = false; clearTimeout(first); clearInterval(h); };
  }, []);
  const strobe = (on: boolean) => { setStrobing(on); api.strobe({ active: on }); };

  // Ctrl+Z / Ctrl+Y (not while typing in a field)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey) || e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;
      const k = e.key.toLowerCase();
      if (k === "z" && !e.shiftKey) { e.preventDefault(); api.undo(); }
      else if (k === "y" || (k === "z" && e.shiftKey)) { e.preventDefault(); api.redo(); }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
  const send = useMemo(() => throttle((v: number) => api.master({ brightness: v })), []);
  const sendFade = useMemo(() => throttle((ms: number) => api.master({ fadeMs: ms })), []);
  const sendStrobe = useMemo(() => throttle((v: number) => api.strobe({ speed: v })), []);

  useEffect(() => {
    if (!dragging && s) setBright(s.brightnessPercent);
  }, [s?.brightnessPercent, dragging]);

  if (!full || !s) return <header className="topbar" />;
  const ids = full.patch.map((p) => p.id);
  const looks = ids.map((id) => s.looks[id]);
  const allOn = s.mode === "static" && ids.length > 0 && looks.every((l) => l?.on);
  const allOff = s.mode === "static" && looks.every((l) => !l?.on);

  return (
    <header className="topbar">
      <button className={"btn lg on" + (allOn ? " active" : "")} title={t("top.onHint")} onClick={() => api.looks([], true)}>
        <Icon name="power" /> {t("top.on")}
      </button>
      {/* off and blackout as one two-part button: off (left) switches the lamps off, blackout (right) darkens for a moment */}
      <div className="combo">
        <button className={"btn lg off" + (allOff ? " active" : "")} title={t("top.offHint")} onClick={() => api.looks([], false)}>
          {t("top.off")}
        </button>
        <button className={"btn lg blackout" + (s.blackout ? " active" : "")} title={t(s.blackout ? "top.blackoutOnHint" : "top.blackoutHint")} onClick={() => api.master({ blackout: !s.blackout })}>
          <Icon name="moon" /> {s.blackout ? t("top.blackoutOn") : t("top.blackout")}
        </button>
      </div>
      {/* brightness, transition and strobe speed: three small sliders under each other */}
      <div className="top-sliders">
        <div className="master">
          <label>{t("top.master")}</label>
          <input type="range" min={0} max={100} value={bright}
            onPointerDown={() => setDragging(true)} onPointerUp={() => setDragging(false)}
            onChange={(e) => { const v = +e.target.value; setBright(v); send(v); }} />
          <span className="val">{bright}%</span>
        </div>
        <DragValue label={t("live.fade")} title={t("top.fadeHint")} value={s.fadeMs} range={[0, 1000]}
          toPos={fadeToPos} fromPos={posToFade} show={(ms) => (ms === 0 ? t("live.fadeNone") : (ms / 1000).toFixed(ms < 10000 ? 1 : 0) + " s")} onChange={sendFade} />
      </div>
      {/* strobe: hold the button; its speed is the thin bar inside it */}
      <div className={"strobe-box" + (strobing ? " active" : "")}>
        <button className={"btn strobe" + (strobing ? " active" : "")} title={t("top.strobeHint")}
          onPointerDown={(e) => { (e.target as Element).setPointerCapture?.(e.pointerId); strobe(true); }}
          onPointerUp={() => strobe(false)} onPointerCancel={() => strobe(false)}
          onContextMenu={(e) => e.preventDefault()}>
          <Icon name="bolt" /> {t("top.strobe")}
        </button>
        <StrobeSpeed value={s.strobeSpeed} onChange={sendStrobe} title={t("live.strobeSpeed")} />
      </div>
      <div className="grow" />
      <NowButton />
      <button className="btn ghost icon-btn" title={t("top.undo")} disabled={!full.undo} onClick={() => api.undo()}><Icon name="undo" /></button>
      <button className="btn ghost icon-btn" title={t("top.redo")} disabled={!full.redo} onClick={() => api.redo()}><Icon name="redo" /></button>
      {!rail && (s.music.syncEffects || s.music.modulation !== "none" || s.music.enabled) && (
        <span className="chip bpm-chip" title="TAP" onClick={() => api.tap()}>
          <span className="dot" style={{ opacity: 1 - (music.beat - Math.floor(music.beat)) * 0.85 }} />
          {music.bpm.toFixed(0)} BPM
        </span>
      )}
      {update && <span className="chip ok clickable" title={t("upd.chipHint")} onClick={() => { try { localStorage.setItem("page", "settings"); } catch { /* ignore */ } location.reload(); }}>
        <span className="dot" />{t("upd.chip", { v: update })}</span>}
      {!rail && <span className="chip mode">{t("top.mode." + s.mode)}</span>}
      <span className={"chip clickable out-chip " + (output.connected ? "ok" : "warn")}
        title={(output.connected
          ? t("status.connected", { name: output.name }) + (output.reconnects ? ` · ${output.reconnects}×` : "") + "\n"
          : t("fault." + (output.fault ?? "Other")) + "\n") + t("usb.recover")} onClick={() => {
          if (recovering) return;   // one recover at a time (each takes up to a few seconds)
          setRecovering(true);
          api.recover(false).catch(() => {}).finally(() => setRecovering(false));
        }}>
        <span className={"dot" + (recovering ? " busy" : "")} />
        {output.connected ? output.name : t("faultShort." + (output.fault ?? "Other"), { name: output.name })}
      </span>
    </header>
  );
}
