import { useState } from "react";
import { useT } from "../i18n";
import { useShow } from "../store";

/** OSC explained, with example addresses (all working against the command language). */
export function OscGuide({ port, host }: { port: number; host: string }) {
  const t = useT();
  const { full, boot } = useShow();
  const [showNames, setShowNames] = useState(false);
  const scene = full!.scenes[0]?.name ?? "Rood licht";
  const group = full!.groups[0]?.name ?? "Voor";
  const chase = full!.state.chases?.[0]?.name;

  const rows: [string, string, string][] = [
    ["/dmxlicht/scene/1", "1", t("osc.ex.scenePos")],
    ["/dmxlicht/scene", `"${scene}"`, t("osc.ex.sceneName")],
    ["/dmxlicht/blackout", "1 · 0 · toggle", t("osc.ex.blackout")],
    ["/dmxlicht/master", "0.0 – 1.0", t("osc.ex.masterFloat")],
    ["/dmxlicht/master", "0 – 100 (int)", t("osc.ex.masterInt")],
    ["/dmxlicht/on", "", t("osc.ex.on")],
    ["/dmxlicht/off", `"${group}"`, t("osc.ex.offGroup")],
    ["/dmxlicht/color/red", "", t("osc.ex.color")],
    ["/dmxlicht/color", `"#FF8800" "${group}"`, t("osc.ex.colorHex")],
    ["/dmxlicht/effect/fire", "", t("osc.ex.effect")],
    ["/dmxlicht/effect/rainbow-smooth", `"${group}"`, t("osc.ex.effectGroup")],
    ...(chase ? [["/dmxlicht/effect", `"${chase}"`, t("osc.ex.chase")] as [string, string, string]] : []),
    ["/dmxlicht/effect/screen", "", t("osc.ex.screen")],
    ["/dmxlicht/stop", "", t("osc.ex.stop")],
    ["/dmxlicht/strobe", "1 / 0", t("osc.ex.strobe")],
    ["/dmxlicht/flash/green", "5", t("osc.ex.flash")],
    ["/dmxlicht/flash/red/10/solid", "", t("osc.ex.flashSolid")],
    ["/dmxlicht/tap", "", t("osc.ex.tap")],
    ["/dmxlicht/bpm", "128", t("osc.ex.bpm")],
    ["/dmxlicht/fade", "2.5", t("osc.ex.fade")],
    ["/dmxlicht/timeline/play", `"${full!.timelines[0]?.name ?? "Show"}"`, t("osc.ex.tlPlay")],
    ["/dmxlicht/timeline/go", "", t("osc.ex.tlGo")],
    ["/dmxlicht/timeline/pause", "", t("osc.ex.tlPause")],
    ["/dmxlicht/timeline/stop", "", t("osc.ex.tlStop")],
    ["/dmxlicht/timeline/next", "", t("osc.ex.tlNext")],
    ["/dmxlicht/timeline/seek", "30", t("osc.ex.tlSeek")],
    ["/dmxlicht/recover", "", t("osc.ex.recover")],
  ];

  return (
    <div className="stack osc-guide">
      <h3>{t("osc.whatTitle")}</h3>
      <p className="hint">{t("osc.what")}</p>
      <h3>{t("osc.setupTitle")}</h3>
      <ol className="steps">
        <li>{t("osc.setup1")}</li>
        <li>{t("osc.setup2", { host, port })}</li>
        <li>{t("osc.setup3")}</li>
        <li>{t("osc.setup4")}</li>
      </ol>
      <h3>{t("osc.rulesTitle")}</h3>
      <ul className="tips">{[1, 2, 3, 4, 5].map((i) => <li key={i}>{t("osc.rule" + i)}</li>)}</ul>
      <h3>{t("osc.examplesTitle")}</h3>
      <div className="osc-table-wrap">
        <table className="osc-table">
          <thead><tr><th>{t("osc.address")}</th><th>{t("osc.args")}</th><th>{t("osc.does")}</th></tr></thead>
          <tbody>{rows.map(([a, b, c], i) => <tr key={i}><td className="mono">{a}</td><td className="mono">{b}</td><td>{c}</td></tr>)}</tbody>
        </table>
      </div>
      <h3>{t("osc.appsTitle")}</h3>
      <ul className="tips">{[1, 2, 3, 4].map((i) => <li key={i}>{t("osc.app" + i)}</li>)}</ul>
      <button className="btn sm ghost" style={{ alignSelf: "flex-start" }} onClick={() => setShowNames(!showNames)}>
        {showNames ? "▾" : "▸"} {t("osc.names")}
      </button>
      {showNames && (
        <div className="stack" style={{ gap: 6 }}>
          <p className="hint">{t("osc.namesHint")}</p>
          <div className="osc-names mono">
            {boot!.effects.map((e) => <span key={e.key} title={t("fxname." + e.key, undefined, e.name)}>{e.key}</span>)}
          </div>
          <p className="hint">{t("osc.colorNames")}: {[...boot!.palette.fan.slice(0, 16), ...boot!.palette.specials].map((c) => c.key).join(", ")}</p>
        </div>
      )}
    </div>
  );
}
