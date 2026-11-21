import { useEffect, useState } from "react";
import { api, type ScreenSyncSettings } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { CameraFrameEditor } from "./CameraFrameEditor";
import { EdgePreview } from "./EdgePreview";
import { useLive } from "../store";
import { Help } from "./Help";

/** Settings and live view for "follow the screen" (ambilight). */
export function ScreenSyncPanel() {
  const t = useT();
  const { full } = useShow();
  const set: ScreenSyncSettings = full!.state.screenSync ?? { mode: "zones", monitor: 0, saturation: 140, smoothing: 50, followBrightness: true, mirror: false };
  const [info, setInfo] = useState<Awaited<ReturnType<typeof api.screenInfo>> | null>(null);
  const { heads } = useLive();
  const lamps = Math.max(1, full!.rig.heads);
  const edges = set.mode === "edges";
  const num = (v: string, min: number, max: number) => Math.min(max, Math.max(min, Math.round(+v || 0)));
  const [cams, setCams] = useState<string[] | null>(null);
  const camera = set.source === "camera";
  useEffect(() => { if (camera) api.cameras().then((r) => setCams(r.cameras)).catch(() => setCams([])); }, [camera]);
  const local = ["127.0.0.1", "localhost", "[::1]"].includes(location.hostname);
  useEffect(() => {
    let alive = true;
    const load = () => api.screenInfo().then((i) => alive && setInfo(i)).catch(() => {});
    load();
    const h = setInterval(load, 400);
    return () => { alive = false; clearInterval(h); };
  }, []);
  const save = (c: Partial<ScreenSyncSettings>) => api.screenSync({ ...set, ...c });
  const slider = (e: { target: EventTarget }) => +(e.target as HTMLInputElement).value;

  return (
    <div className="stack">
      <div className="field">
        <span className="row" style={{ gap: 4 }}>{t("cam.source")} <Help k="cam.sourceHelp" /></span>
        <div className="seg">
          <button className={!camera ? "active" : ""} onClick={() => save({ source: "screen" })}>{t("cam.fromScreen")}</button>
          <button className={camera ? "active" : ""} onClick={() => save({ source: "camera" })}>{t("cam.fromCamera")}</button>
        </div>
      </div>
      <p className="hint">{camera ? t("cam.hint") : t("ss.hint")}</p>
      {camera && (
        <>
          <label className="field">{t("cam.device")}
            <select className="input" value={set.camera ?? ""} onChange={(e) => save({ camera: e.target.value || null })}>
              <option value="">{t("cam.auto")}</option>
              {(cams ?? []).map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </label>
          {cams && cams.length === 0 && <div className="note warn">{t("cam.none")}</div>}
          {info?.cameraError && <div className="note warn">{info.cameraError}</div>}
          {local
            ? <CameraFrameEditor corners={set.corners ?? [0.1, 0.1, 0.9, 0.1, 0.9, 0.9, 0.1, 0.9]} onChange={(c) => save({ corners: c })} />
            : <p className="hint">{t("cam.onlyHere")}</p>}
          <ol className="steps">{[1, 2, 3, 4].map((i) => <li key={i}>{t("cam.step" + i)}</li>)}</ol>
        </>
      )}
      <div className="preview-strip" title={t("ss.previewHint")}>
        {(info?.columns.length ? info.columns : ["#000000"]).map((c, i) => <div key={i} style={{ background: c }} />)}
      </div>
      {info && !info.fresh && <p className="hint warn-text">{t("ss.notRunning")}</p>}
      <div className="field">
        <span className="row" style={{ gap: 4 }}>{t("ss.mode")} <Help k="ss.modeHelp" /></span>
        <div className="seg">
          <button className={set.mode === "zones" ? "active" : ""} onClick={() => save({ mode: "zones" })}>{t("ss.zones")}</button>
          <button className={set.mode === "average" ? "active" : ""} onClick={() => save({ mode: "average" })}>{t("ss.average")}</button>
          <button className={edges ? "active" : ""} onClick={() => save({ mode: "edges" })}>{t("edge.mode")}</button>
        </div>
      </div>
      {edges && (
        <div className="stack edge-settings">
          <EdgePreview set={set} lamps={lamps} grid={info?.grid ?? null} colors={heads} />
          <p className="hint">{t("edge.hint", { n: lamps })}</p>
          <label className="check">
            <input type="checkbox" checked={set.edgeAuto ?? true} onChange={(e) => save({ edgeAuto: e.target.checked })} />
            <span className="box" /> {t("edge.auto")}
          </label>
          {!(set.edgeAuto ?? true) && (
            <div className="edge-counts">
              {(["edgeLeft", "edgeTop", "edgeRight", "edgeBottom"] as const).map((k) => (
                <label key={k} className="field">{t("edge." + k)}
                  <input className="input num" type="number" min={0} max={128} value={set[k] ?? 0}
                    onChange={(e) => save({ [k]: num(e.target.value, 0, 128) })} />
                </label>
              ))}
            </div>
          )}
          <label className="field">{t("edge.depth", { n: set.edgeDepth ?? 15 })}
            <input type="range" min={2} max={50} defaultValue={set.edgeDepth ?? 15} key={"d" + (set.edgeDepth ?? 15)}
              onPointerUp={(e) => save({ edgeDepth: slider(e) })} onKeyUp={(e) => save({ edgeDepth: slider(e) })} />
          </label>
          <div className="row">
            <label className="check">
              <input type="checkbox" checked={!!set.edgeReverse} onChange={(e) => save({ edgeReverse: e.target.checked })} />
              <span className="box" /> {t("edge.reverse")}
            </label>
            <label className="row" style={{ gap: 6 }}>
              <span className="muted">{t("edge.start")}</span>
              <button className="btn sm" onClick={() => save({ edgeStart: (set.edgeStart ?? 0) - 1 })}>−</button>
              <b>{(((set.edgeStart ?? 0) % lamps) + lamps) % lamps}</b>
              <button className="btn sm" onClick={() => save({ edgeStart: (set.edgeStart ?? 0) + 1 })}>+</button>
            </label>
          </div>
          <p className="hint">{t("edge.order")}</p>
        </div>
      )}
      {(set.mode !== "average") && (
        <label className="check">
          <input type="checkbox" checked={set.skipBlackBars ?? true} onChange={(e) => save({ skipBlackBars: e.target.checked })} />
          <span className="box" /> {t("edge.bars")}
        </label>
      )}
      {!camera && info && info.monitors.length > 1 && (
        <label className="field">{t("ss.monitor")}
          <select className="input" value={set.monitor} onChange={(e) => save({ monitor: +e.target.value })}>
            {info.monitors.map((m, i) => <option key={i} value={i}>{m}</option>)}
          </select>
        </label>
      )}
      <label className="field">{t("ss.saturation", { n: set.saturation })}
        <input type="range" min={0} max={300} step={10} defaultValue={set.saturation} key={"s" + set.saturation}
          onPointerUp={(e) => save({ saturation: slider(e) })} onKeyUp={(e) => save({ saturation: slider(e) })} />
      </label>
      <label className="field">{t("ss.smoothing", { n: set.smoothing })}
        <input type="range" min={0} max={100} step={5} defaultValue={set.smoothing} key={"m" + set.smoothing}
          onPointerUp={(e) => save({ smoothing: slider(e) })} onKeyUp={(e) => save({ smoothing: slider(e) })} />
      </label>
      <label className="check">
        <input type="checkbox" checked={set.followBrightness} onChange={(e) => save({ followBrightness: e.target.checked })} />
        <span className="box" /> {t("ss.brightness")}
      </label>
      {set.mode === "zones" && (
        <label className="check">
          <input type="checkbox" checked={set.mirror} onChange={(e) => save({ mirror: e.target.checked })} />
          <span className="box" /> {t("ss.mirror")}
        </label>
      )}
      {!camera && <p className="hint">{t("ss.drm")}</p>}
    </div>
  );
}
