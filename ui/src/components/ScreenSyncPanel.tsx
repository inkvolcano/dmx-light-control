import { useEffect, useRef, useState } from "react";
import { api, type ScreenSyncSettings } from "../api";
import { useT } from "../i18n";
import { onLinux, useShow } from "../store";
import { CameraFrameEditor } from "./CameraFrameEditor";
import { EdgePreview } from "./EdgePreview";
import { useLive } from "../store";
import { Help } from "./Help";
import { Icon } from "./Icon";
import { PointsEditor, pointsFor } from "./PointsEditor";

/** Shared state for the screen/camera ("ambilight") settings: current settings, live info from the PC, save. */
export function useScreenSync() {
  const { full } = useShow();
  const set: ScreenSyncSettings = full!.state.screenSync ?? { mode: "zones", monitor: 0, saturation: 140, smoothing: 50, followBrightness: true, mirror: false };
  const [info, setInfo] = useState<Awaited<ReturnType<typeof api.screenInfo>> | null>(null);
  const [cams, setCams] = useState<string[] | null>(null);
  const camera = set.source === "camera";
  useEffect(() => { if (camera) api.cameras().then((r) => setCams(r.cameras)).catch(() => setCams([])); }, [camera]);
  useEffect(() => {
    let alive = true;
    const load = () => api.screenInfo().then((i) => alive && setInfo(i)).catch(() => {});
    load();
    const h = setInterval(load, 400);
    return () => { alive = false; clearInterval(h); };
  }, []);
  // the server replaces the whole settings object: always send the newest version (own saves not yet echoed
  // included), so an older closure (arrow-key timer, slider) never undoes another change
  const latest = useRef(set);
  const serverJson = useRef("");
  const json = JSON.stringify(set);
  if (json !== serverJson.current) { serverJson.current = json; latest.current = set; }
  const save = (c: Partial<ScreenSyncSettings>) => {
    const next = { ...latest.current, ...c };
    latest.current = next;
    return api.screenSync(next);
  };
  // the webcam only runs while its picture is open here (or while the screen is being followed): opening this
  // page never switches it on by itself
  const [picture, setPicture] = useState(false);
  return { set, info, cams, camera, save, lamps: Math.max(1, full!.rig.heads), picture, setPicture };
}
type ScreenSync = ReturnType<typeof useScreenSync>;

const isLocal = () => ["127.0.0.1", "localhost", "[::1]"].includes(location.hostname);

/** Where the colours come from: this PC's screen, or a webcam looking at a TV (with frame and keystone). */
export function ScreenSource({ s }: { s: ScreenSync }) {
  const t = useT();
  const { set, info, cams, camera, save } = s;
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
          {!isLocal() ? <p className="hint">{t("cam.onlyHere")}</p>
            : s.picture ? (
              <>
                <CameraFrameEditor corners={set.corners ?? [0.1, 0.1, 0.9, 0.1, 0.9, 0.9, 0.1, 0.9]} onChange={(c) => save({ corners: c })} />
                <button className="btn sm ghost" onClick={() => s.setPicture(false)}><Icon name="eye" /> {t("cam.hidePicture")}</button>
              </>
            ) : <CameraOff s={s} />}
          <ol className="steps">{[1, 2, 3, 4].map((i) => <li key={i}>{t("cam.step" + i)}</li>)}</ol>
        </>
      )}
      {!camera && info && info.monitors.length > 1 && (
        <label className="field">{t("ss.monitor")}
          <select className="input" value={set.monitor} onChange={(e) => save({ monitor: +e.target.value })}>
            {info.monitors.map((m, i) => <option key={i} value={i}>{m}</option>)}
          </select>
        </label>
      )}
      {!camera && <p className="hint">{t("ss.drm")}</p>}
    </div>
  );
}

/** How the picture is spread over the lamps: columns, average or real edges, plus colour and calm settings. */
export function ScreenSettings({ s, pointsHere = true }: { s: ScreenSync; pointsHere?: boolean }) {
  const t = useT();
  const { heads } = useLive();
  const { set, info, save, lamps } = s;
  const edges = set.mode === "edges";
  const num = (v: string, min: number, max: number) => Math.min(max, Math.max(min, Math.round(+v || 0)));
  const slider = (e: { target: EventTarget }) => +(e.target as HTMLInputElement).value;
  return (
    <div className="stack">
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
          <button className={set.mode === "points" ? "active" : ""} onClick={() => save({ mode: "points" })}>{t("pts.mode")}</button>
        </div>
      </div>
      {set.mode === "points" && (pointsHere ? <PointsBlock s={s} /> : <p className="hint">{t("pts.below")}</p>)}
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
      {(set.mode === "zones" || edges) && (
        <label className="check">
          <input type="checkbox" checked={set.skipBlackBars ?? true} onChange={(e) => save({ skipBlackBars: e.target.checked })} />
          <span className="box" /> {t("edge.bars")}
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
    </div>
  );
}

/** The marker editor with its size setting, a reset and an "around the picture" layout. */
export function PointsBlock({ s }: { s: ScreenSync }) {
  const t = useT();
  const { heads } = useLive();
  const { set, info, save, lamps } = s;
  const slider = (e: { target: EventTarget }) => +(e.target as HTMLInputElement).value;
  const around = () => {
    // like ambilight: up the left side, across the top, down the right side
    const n = lamps, side = n >= 3 ? Math.max(1, Math.round((n * 9) / 34)) : 0, top = n - 2 * side, out: number[] = [];
    for (let i = 0; i < side; i++) out.push(0.04, 0.9 - (0.8 * (i + 0.5)) / side);
    for (let i = 0; i < top; i++) out.push(0.05 + (0.9 * (i + 0.5)) / top, 0.07);
    for (let i = 0; i < side; i++) out.push(0.96, 0.1 + (0.8 * (i + 0.5)) / side);
    save({ points: out.length ? out : pointsFor(set, n) });
  };
  return (
    <div className="stack">
      <p className="hint">{t("pts.hint", { n: lamps })}</p>
      {s.camera && !s.picture && isLocal() && <CameraOff s={s} />}
      <PointsEditor set={set} lamps={lamps} colors={heads} grid={info?.grid ?? null} live={!s.camera || s.picture} onChange={(points) => save({ points })} />
      <div className="row" style={{ flexWrap: "wrap" }}>
        <label className="field grow">{t("pts.size", { n: set.pointSize ?? 8 })}
          <input type="range" min={1} max={30} defaultValue={set.pointSize ?? 8} key={"p" + (set.pointSize ?? 8)}
            onPointerUp={(e) => save({ pointSize: slider(e) })} onKeyUp={(e) => save({ pointSize: slider(e) })} />
        </label>
        <button className="btn sm" onClick={() => save({ points: null })}>{t("pts.reset")}</button>
        <button className="btn sm" onClick={around}>{t("pts.around")}</button>
      </div>
    </div>
  );
}

/** The camera picture is closed: say so, with a button to open it (which switches the webcam on). */
function CameraOff({ s }: { s: ScreenSync }) {
  const t = useT();
  return (
    <div className="note">
      <Icon name="eye" />
      <span className="grow">{t("cam.pictureOff")}</span>
      <button className="btn sm" onClick={() => s.setPicture(true)}>{t("cam.showPicture")}</button>
    </div>
  );
}

/** Compact version for the effects panel: a pointer to the Screen page, where the camera frame has room. */
export function ScreenSyncPanel() {
  const t = useT();
  if (onLinux()) return <p className="hint">{t("linux.noScreen")}</p>;
  return (
    <div className="stack">
      <p className="hint">{t("ss.hint")}</p>
      <button className="btn" onClick={() => window.dispatchEvent(new CustomEvent("dmx-goto", { detail: "screen" }))}>
        <Icon name="monitor" /> {t("ss.openPage")}
      </button>
    </div>
  );
}
