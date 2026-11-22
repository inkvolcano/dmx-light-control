import { api } from "../api";
import { Help } from "../components/Help";
import { Icon } from "../components/Icon";
import { PointsBlock, ScreenSettings, ScreenSource, useScreenSync } from "../components/ScreenSyncPanel";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

/**
 * Lamps follow a screen ("ambilight"): this PC's screen or a webcam aimed at a TV, with the frame, keystone
 * and automatic TV detection on the left and how the picture is spread over the lamps on the right.
 */
export function ScreenPage() {
  const t = useT();
  const { full } = useShow();
  const { heads } = useLive();
  const s = useScreenSync();
  const fx = full!.state.effect;
  const running = full!.state.mode === "effect" && !!fx.screenSync;
  const start = () => api.effect({ ...fx, screenSync: true, chaseId: null }, true);
  const stop = () => api.mode("static");

  return (
    <div className="screen-page">
      <div className="page-head">
        <button className="btn sm ghost" onClick={() => window.dispatchEvent(new Event("dmx-back"))}><Icon name="chevron-left" size={16} /> {t("rail.back")}</button>
        <h1>{t("ss.setupTitle")}</h1>
        <Help k="ss.pageHelp" />
        <div className="grow" />
        {running
          ? <button className="btn off active" onClick={stop}><Icon name="stop" /> {t("fx.stop")}</button>
          : <button className="btn primary" onClick={start}><Icon name="play" /> {t("ss.start")}</button>}
      </div>
      <div className="card stack" style={{ marginBottom: 16 }}>
        <div className="row">
          <h3 className="grow" style={{ margin: 0 }}>{t("ss.lampsNow")}</h3>
          <span className={"chip" + (running ? " ok" : "")}>{running ? t("ss.isOn") : t("ss.isOff")}</span>
        </div>
        <div className="preview-strip">
          {(heads.length ? heads : ["#000000"]).map((h, i) => <div key={i} style={{ background: h }} />)}
        </div>
      </div>
      <div className="screen-layout">
        <div className="card">
          <h2><Icon name="monitor" /> {s.camera ? t("ss.cardCamera") : t("ss.cardSource")}</h2>
          <ScreenSource s={s} />
        </div>
        <div className="card">
          <h2>{t("ss.cardSpread")}</h2>
          <ScreenSettings s={s} pointsHere={false} />
        </div>
      </div>
      {s.set.mode === "points" && (
        <div className="card" style={{ marginTop: 16 }}>
          <h2 style={{ marginTop: 0 }}>{t("pts.title")}</h2>
          <PointsBlock s={s} />
        </div>
      )}
    </div>
  );
}
