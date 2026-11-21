import { useState } from "react";
import { api, type ChannelDef, type ChannelKind, type FixtureMode, type FixtureType } from "../api";
import { reloadBootstrap } from "../store";
import { useT } from "../i18n";
import { Icon } from "./Icon";

const kinds: ChannelKind[] = ["dimmer", "red", "green", "blue", "white", "amber", "uv", "hue", "saturation", "value", "strobe", "fixed"];

/** Make or change a fixture type: modes, channels, segments, strobe ranges. */
export function FixtureEditor({ initial, onClose }: { initial: FixtureType; onClose: () => void }) {
  const t = useT();
  const [ft, setFt] = useState<FixtureType>(() => structuredClone(initial));
  const [modeIx, setModeIx] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const mode = ft.modes[modeIx];

  const setMode = (m: FixtureMode) => setFt({ ...ft, modes: ft.modes.map((x, i) => (i === modeIx ? m : x)) });
  const renumber = (chs: ChannelDef[]) => chs.map((c, i) => ({ ...c, number: i + 1 }));
  const setCh = (i: number, c: Partial<ChannelDef>) => setMode({ ...mode, channels: mode.channels.map((x, j) => (j === i ? { ...x, ...c } : x)) });
  const move = (i: number, d: number) => {
    const chs = [...mode.channels];
    const j = i + d;
    if (j < 0 || j >= chs.length) return;
    [chs[i], chs[j]] = [chs[j], chs[i]];
    setMode({ ...mode, channels: renumber(chs), footprint: Math.max(mode.footprint, chs.length) });
  };
  const add = () => {
    const chs = renumber([...mode.channels, { number: 0, kind: "fixed", cell: -1, default: 0, label: "", rangeMin: 1, rangeMax: 255 } as ChannelDef]);
    setMode({ ...mode, channels: chs, footprint: Math.max(mode.footprint, chs.length) });
  };
  const remove = (i: number) => {
    const chs = renumber(mode.channels.filter((_, j) => j !== i));
    setMode({ ...mode, channels: chs, footprint: chs.length });
  };

  const save = async () => {
    setError(null);
    const res = await fetch("api/library/save", { method: "POST", headers: api.headers(), body: JSON.stringify({ ...ft, modes: ft.modes.map((m) => ({ ...m, footprint: Math.max(m.footprint, m.channels.length) })) }) });
    if (!res.ok) { setError(await res.text()); return; }
    await reloadBootstrap();
    onClose();
  };

  return (
    <div className="offline" onClick={onClose}>
      <div className="box editor" onClick={(e) => e.stopPropagation()}>
        <div className="row">
          <h2 style={{ flex: 1 }}>{t("fe.title")}</h2>
          <button className="btn ghost icon-btn" onClick={onClose}>×</button>
        </div>
        <div className="row">
          <label className="field">{t("fe.manufacturer")}<input className="input" value={ft.manufacturer} onChange={(e) => setFt({ ...ft, manufacturer: e.target.value })} /></label>
          <label className="field" style={{ flex: 1 }}>{t("fe.model")}<input className="input" value={ft.model} onChange={(e) => setFt({ ...ft, model: e.target.value })} /></label>
          <label className="field">{t("fe.colors")}<input className="input" style={{ width: 110 }} value={ft.colors ?? ""} onChange={(e) => setFt({ ...ft, colors: e.target.value })} /></label>
        </div>

        <div className="row">
          <div className="seg">
            {ft.modes.map((m, i) => <button key={i} className={i === modeIx ? "active" : ""} onClick={() => setModeIx(i)}>{m.name || "?"}</button>)}
          </div>
          <button className="btn sm" onClick={() => { setFt({ ...ft, modes: [...ft.modes, { name: t("fe.newMode"), footprint: 1, channels: [], cellCount: 1 }] }); setModeIx(ft.modes.length); }}>
            <Icon name="plus" /> {t("fe.addMode")}
          </button>
          {ft.modes.length > 1 && <button className="btn sm ghost danger" onClick={() => { setFt({ ...ft, modes: ft.modes.filter((_, i) => i !== modeIx) }); setModeIx(0); }}>{t("fe.removeMode")}</button>}
        </div>

        {mode && (
          <>
            <div className="row">
              <label className="field">{t("fe.modeName")}<input className="input" value={mode.name} onChange={(e) => setMode({ ...mode, name: e.target.value })} /></label>
              <label className="field">{t("fe.display")}<input className="input" style={{ width: 90 }} value={mode.displayCode ?? ""} onChange={(e) => setMode({ ...mode, displayCode: e.target.value })} /></label>
              <label className="field">{t("fe.footprint")}<input className="input num" type="number" min={mode.channels.length || 1} max={512} value={mode.footprint} onChange={(e) => setMode({ ...mode, footprint: Math.max(+e.target.value, mode.channels.length) })} /></label>
            </div>
            <div className="fe-table">
              <div className="fe-row fe-head"><span>#</span><span>{t("fe.kind")}</span><span>{t("fe.segment")}</span><span>{t("fe.label")}</span><span>{t("fe.value")}</span><span /></div>
              {mode.channels.map((c, i) => (
                <div key={i} className="fe-row">
                  <span className="mono muted">{c.number}</span>
                  <select className="input" value={c.kind} onChange={(e) => setCh(i, { kind: e.target.value as ChannelKind })}>
                    {kinds.map((k) => <option key={k} value={k}>{t("kind." + k, undefined, k)}</option>)}
                  </select>
                  <input className="input num" type="number" min={0} max={64} placeholder="-" value={c.cell >= 0 ? c.cell + 1 : ""}
                    title={t("fe.segmentHint")} onChange={(e) => setCh(i, { cell: e.target.value === "" ? -1 : Math.max(0, +e.target.value - 1) })} />
                  <input className="input" value={c.label ?? ""} onChange={(e) => setCh(i, { label: e.target.value })} />
                  {c.kind === "strobe" ? (
                    <span className="row" style={{ gap: 4, flexWrap: "nowrap" }}>
                      <input className="input num" title={t("fe.off")} type="number" min={0} max={255} value={c.default} onChange={(e) => setCh(i, { default: +e.target.value })} />
                      <input className="input num" title={t("fe.slow")} type="number" min={0} max={255} value={c.rangeMin} onChange={(e) => setCh(i, { rangeMin: +e.target.value })} />
                      <input className="input num" title={t("fe.fast")} type="number" min={0} max={255} value={c.rangeMax} onChange={(e) => setCh(i, { rangeMax: +e.target.value })} />
                    </span>
                  ) : c.kind === "fixed" ? (
                    <input className="input num" type="number" min={0} max={255} value={c.default} title={t("fe.fixedHint")} onChange={(e) => setCh(i, { default: +e.target.value })} />
                  ) : <span />}
                  <span className="row" style={{ gap: 2, flexWrap: "nowrap" }}>
                    <button className="btn sm ghost icon-btn" onClick={() => move(i, -1)}>↑</button>
                    <button className="btn sm ghost icon-btn" onClick={() => move(i, 1)}>↓</button>
                    <button className="btn sm ghost danger icon-btn" onClick={() => remove(i)}><Icon name="trash" /></button>
                  </span>
                </div>
              ))}
            </div>
            <div className="row">
              <button className="btn sm" onClick={add}><Icon name="plus" /> {t("fe.addChannel")}</button>
              <span className="hint">{t("fe.hint")}</span>
            </div>
          </>
        )}
        {error && <div className="note warn"><Icon name="warn" />{error}</div>}
        <div className="row" style={{ justifyContent: "flex-end" }}>
          <button className="btn ghost" onClick={onClose}>{t("common.cancel")}</button>
          <button className="btn primary" onClick={save}><Icon name="save" /> {t("fe.save")}</button>
        </div>
      </div>
    </div>
  );
}
