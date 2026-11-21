import { useEffect, useMemo, useState } from "react";
import { api, throttle } from "../api";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";
import { Icon } from "./Icon";

export function TopBar() {
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
      <button className={"btn lg on" + (allOn ? " active" : "")} onClick={() => api.looks([], true)}>
        <Icon name="power" /> {t("top.on")}
      </button>
      <button className={"btn lg off" + (allOff ? " active" : "")} onClick={() => api.looks([], false)}>
        {t("top.off")}
      </button>
      <div className="master">
        <label>{t("top.master")}</label>
        <input type="range" min={0} max={100} value={bright}
          onPointerDown={() => setDragging(true)} onPointerUp={() => setDragging(false)}
          onChange={(e) => { const v = +e.target.value; setBright(v); send(v); }} />
        <span className="val">{bright}%</span>
      </div>
      <button className={"btn blackout" + (s.blackout ? " active" : "")} onClick={() => api.master({ blackout: !s.blackout })}>
        <Icon name="moon" /> {t("top.blackout")}
      </button>
      <button className={"btn strobe" + (strobing ? " active" : "")} title={t("top.strobeHint")}
        onPointerDown={(e) => { (e.target as Element).setPointerCapture?.(e.pointerId); strobe(true); }}
        onPointerUp={() => strobe(false)} onPointerCancel={() => strobe(false)}
        onContextMenu={(e) => e.preventDefault()}>
        <Icon name="bolt" /> {t("top.strobe")}
      </button>
      <div className="grow" />
      <button className="btn ghost icon-btn" title={t("top.undo")} disabled={!full.undo} onClick={() => api.undo()}><Icon name="undo" /></button>
      <button className="btn ghost icon-btn" title={t("top.redo")} disabled={!full.redo} onClick={() => api.redo()}><Icon name="redo" /></button>
      {(s.music.syncEffects || s.music.modulation !== "none" || s.music.enabled) && (
        <span className="chip bpm-chip" title="TAP" onClick={() => api.tap()}>
          <span className="dot" style={{ opacity: 1 - (music.beat - Math.floor(music.beat)) * 0.85 }} />
          {music.bpm.toFixed(0)} BPM
        </span>
      )}
      {update && <span className="chip ok clickable" title={t("upd.chipHint")} onClick={() => { try { localStorage.setItem("page", "settings"); } catch { /* ignore */ } location.reload(); }}>
        <span className="dot" />{t("upd.chip", { v: update })}</span>}
      <span className="chip mode">{t("top.mode." + s.mode)}</span>
      <span className={"chip clickable " + (output.connected ? "ok" : "warn")}
        title={(output.connected ? "" : t("fault." + (output.fault ?? "Other")) + " - ") + t("usb.recover")} onClick={() => {
          if (recovering) return;   // one recover at a time (each takes up to a few seconds)
          setRecovering(true);
          api.recover(false).catch(() => {}).finally(() => setRecovering(false));
        }}>
        <span className={"dot" + (recovering ? " busy" : "")} />
        {output.connected
          ? t("status.connected", { name: output.name }) + (output.reconnects ? ` · ${output.reconnects}×` : "")
          : t("faultShort." + (output.fault ?? "Other"), { name: output.name })}
      </span>
    </header>
  );
}
