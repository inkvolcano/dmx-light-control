import { useEffect, useMemo, useState } from "react";
import { api, throttle, type ChannelDef } from "../api";
import { Icon } from "../components/Icon";
import { useT } from "../i18n";
import { useShow } from "../store";

const kindColor: Record<string, string> = {
  red: "#ff4d4d", green: "#3ddc6e", blue: "#4d7cff", white: "#f2f2f2", amber: "#ffb000", uv: "#8a3dff",
  dimmer: "#fcb24e", hue: "linear-gradient(90deg,#f33,#ff3,#3f3,#3ff,#33f,#f3f)", saturation: "#bbb", value: "#ddd",
};

export function FadersPage({ selected }: { selected: string[] }) {
  const t = useT();
  const { full, boot } = useShow();
  const patch = full!.patch;
  const [id, setId] = useState<string>(() => selected[0] ?? patch[0]?.id ?? "");
  const [sameType, setSameType] = useState(false);
  const [local, setLocal] = useState<Record<number, number>>({});

  useEffect(() => { if (!patch.some((p) => p.id === id) && patch[0]) setId(patch[0].id); }, [patch, id]);
  useEffect(() => setLocal({}), [id, full?.state.faders]);

  const fixture = patch.find((p) => p.id === id);
  const info = full!.rig.fixtures.find((f) => f.id === id);
  const targets = useMemo(() => fixture
    ? (sameType ? patch.filter((p) => p.typeId === fixture.typeId && p.mode === fixture.mode).map((p) => p.id) : [fixture.id])
    : [], [fixture, sameType, patch]);
  // one throttle per channel: with a shared one, moving two faders quickly lost the last value of the first
  const throttles = useMemo(() => new Map<string, (ids: string[], ch: number, v: number) => void>(), []);
  const sendFader = (ids: string[], ch: number, v: number) => {
    const k = ids.join(",") + "#" + ch;
    let f = throttles.get(k);
    if (!f) { f = throttle((i: string[], c: number, x: number) => i.forEach((id) => api.fader(id, c, x)), 50); throttles.set(k, f); }
    f(ids, ch, v);
  };

  if (!patch.length) return <div className="empty">{t("faders.none")}</div>;
  const values = full!.state.faders[id] ?? [];
  const type = boot!.library.find((x) => x.id === fixture?.typeId);

  return (
    <div>
      <div className="page-head">
        <h1>{t("faders.title")}</h1>
        <select className="input" value={id} onChange={(e) => setId(e.target.value)} style={{ minWidth: 240 }}>
          {patch.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.address}</option>)}
        </select>
        <label className="check">
          <input type="checkbox" checked={sameType} onChange={(e) => setSameType(e.target.checked)} />
          <span className="box" /> {t("faders.allSame")} {sameType && <span className="muted">({targets.length})</span>}
        </label>
        <div className="grow" />
        <button className="btn sm" onClick={() => api.fadersAll(targets, 0)}>{t("faders.zero")}</button>
        <button className="btn sm" onClick={() => api.fadersAll(targets, 255)}>{t("faders.full")}</button>
        <button className="btn sm" onClick={() => api.fadersFromOutput()}><Icon name="refresh" /> {t("faders.take")}</button>
      </div>
      <p className="hint" style={{ marginBottom: 14 }}>
        {t("faders.hint")} {type && <span>· {type.manufacturer} {type.model} · {fixture?.mode}</span>}
      </p>
      <div className="fader-bank">
        {info && Array.from({ length: info.footprint }, (_, i) => {
          const ch = i + 1;
          const def: ChannelDef | undefined = [...info.channels].reverse().find((c) => c.number === ch);
          const v = local[ch] ?? values[ch] ?? 0;
          const label = def ? (def.kind === "fixed" ? def.label ?? "" : (def.cell >= 0 ? def.label : t("kind." + def.kind))) : "-";
          return (
            <div key={ch} className={"fader" + (def?.kind === "fixed" ? " fixed" : "")} title={def?.note}>
              <span className="ch">{ch} · {fixture!.address + i}</span>
              <span className="kind">{label}</span>
              <input type="range" min={0} max={255} value={v}
                onChange={(e) => { const nv = +e.target.value; setLocal((l) => ({ ...l, [ch]: nv })); sendFader(targets, ch, nv); }} />
              <span className="v">{v}</span>
              <span className="stripe" style={{ background: def ? kindColor[def.kind] ?? "var(--line)" : "var(--line)" }} />
            </div>
          );
        })}
      </div>
      {info?.channels.filter((c) => c.note).map((c) => (
        <div key={c.number} className="note" style={{ marginTop: 10 }}>
          <Icon name="info" /> <span><b>CH{c.number} {c.label}:</b> {c.note}</span>
        </div>
      ))}
    </div>
  );
}
