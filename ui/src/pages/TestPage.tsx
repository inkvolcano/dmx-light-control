import { useEffect, useMemo, useState } from "react";
import { api, throttle } from "../api";
import { Icon } from "../components/Icon";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

/** DMX monitor of all 512 channels, setting channels by hand, and "lamp by lamp". */
export function TestPage() {
  const t = useT();
  const { full, boot } = useShow();
  const { frame, test } = useLive();
  const [sel, setSel] = useState<number | null>(null);
  const [walkMs, setWalkMs] = useState(1000);
  const universes = Math.max(1, full!.rig.universes ?? 1);
  const [uni, setUni] = useState(1);
  const u = Math.min(uni, universes);
  const off = (u - 1) * 512;   // frame index of channel 0 of this universe
  const set = useMemo(() => throttle((channels: Record<number, number>, exclusive: boolean) => api.test(channels, exclusive), 40), []);
  const channels: Record<number, number> = { ...test.channels };

  // which fixture owns each address (for colours and the info line)
  const owner = useMemo(() => {
    const map: { id: string; name: string; label: string; index: number }[] = [];
    full!.patch.forEach((p, i) => {
      const info = full!.rig.fixtures.find((f) => f.id === p.id);
      if (!info) return;
      for (let c = 1; c <= info.footprint; c++) {
        if (p.address + c - 1 > 512) break;
        const addr = ((p.universe ?? 1) - 1) * 512 + p.address + c - 1;   // absolute, as the frame
        const def = [...info.channels].reverse().find((x) => x.number === c);
        map[addr] = { id: p.id, name: p.name, label: def ? (def.label ?? def.kind) : String(c), index: i };
      }
    });
    return map;
  }, [full]);

  useEffect(() => () => { api.clearTest(); }, []);   // leaving the page releases the test channels

  const setValue = (ch: number, v: number) => set({ ...channels, [ch]: v }, test.exclusive);
  const release = (ch: number) => { const c = { ...channels }; delete c[ch]; api.test(c, test.exclusive); };
  const walking = test.walk != null;
  const walkName = full!.patch.find((p) => p.id === test.walk)?.name;
  const v = sel ? (channels[sel] ?? frame[sel - 1] ?? 0) : 0;
  const o = sel ? owner[sel] : undefined;

  return (
    <div>
      <div className="page-head">
        <h1>{t("test.title")}</h1>
        <div className="grow" />
        <label className="check">
          <input type="checkbox" checked={test.exclusive} onChange={(e) => api.test(channels, e.target.checked)} />
          <span className="box" /> {t("test.exclusive")}
        </label>
        <button className="btn sm" onClick={() => { api.clearTest(); setSel(null); }}>{t("test.clear")}</button>
      </div>

      <div className="settings-grid" style={{ marginBottom: 14 }}>
        <div className="card stack">
          <h2>{t("test.channel")}{sel ? ` ${universes > 1 ? "U" + (Math.floor((sel - 1) / 512) + 1) + "." : ""}${((sel - 1) % 512) + 1}` : ""}</h2>
          {sel ? (
            <>
              <p className="hint">{o ? `${o.name} · ${o.label}` : t("test.free")}</p>
              <div className="slider">
                <input type="range" min={0} max={255} value={v} onChange={(e) => setValue(sel, +e.target.value)} />
                <span className="val">{v}</span>
              </div>
              <div className="row">
                <button className="btn sm" onClick={() => setValue(sel, 0)}>0</button>
                <button className="btn sm" onClick={() => setValue(sel, 128)}>50%</button>
                <button className="btn sm" onClick={() => setValue(sel, 255)}>255</button>
                <button className="btn sm ghost" onClick={() => release(sel)} disabled={channels[sel] === undefined}>{t("test.release")}</button>
              </div>
            </>
          ) : <p className="hint">{t("test.pick")}</p>}
        </div>
        <div className="card stack">
          <h2>{t("test.walk")}</h2>
          <p className="hint">{t("test.walkHint")}</p>
          <div className="row">
            <select className="input" value={walkMs} onChange={(e) => { setWalkMs(+e.target.value); if (walking) api.walk(+e.target.value); }}>
              {[500, 1000, 2000, 4000].map((ms) => <option key={ms} value={ms}>{ms / 1000} s</option>)}
            </select>
            {walking
              ? <button className="btn off active" onClick={() => api.walk(0)}><Icon name="stop" /> {t("fx.stop")}</button>
              : <button className="btn primary" onClick={() => api.walk(walkMs)} disabled={!full!.patch.length}><Icon name="play" /> {t("fx.start")}</button>}
          </div>
          {walking && <p><b>{walkName}</b> <span className="muted">· {full!.patch.find((p) => p.id === test.walk)?.address}</span></p>}
        </div>
      </div>

      <div className="card">
        {universes > 1 && (
          <div className="seg" style={{ marginBottom: 10 }}>
            {Array.from({ length: universes }, (_, i) => i + 1).map((n) => (
              <button key={n} className={u === n ? "active" : ""} onClick={() => { setUni(n); setSel(null); }}>{t("patch.universeN", { n })}</button>
            ))}
          </div>
        )}
        <div className="dmx-grid">
          {Array.from({ length: 512 }, (_, i) => {
            const ch = off + i + 1;
            const val = frame[ch - 1] ?? 0;
            const own = owner[ch];
            const forced = channels[ch] !== undefined;
            return (
              <button key={ch} className={"dmx-cell" + (sel === ch ? " sel" : "") + (forced ? " forced" : "") + (own ? (own.index % 2 ? " own alt" : " own") : "")}
                onClick={() => setSel(ch)} title={own ? `${ch - off}: ${own.name} · ${own.label}` : String(ch - off)}>
                <span className="fill" style={{ height: (val / 255) * 100 + "%" }} />
                <span className="n">{ch - off}</span>
                <span className="v">{val || ""}</span>
              </button>
            );
          })}
        </div>
        <p className="hint" style={{ marginTop: 10 }}>{t("test.legend", { n: boot!.version })}</p>
      </div>
    </div>
  );
}
