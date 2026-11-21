import { useEffect, useState } from "react";
import { api, type AppSettings, type OutputSettings } from "../api";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";
import { Help } from "./Help";

/** Which outputs send DMX: the USB dongle, Art-Net and sACN (any combination). */
export function OutputsCard({ settings, save }: { settings: AppSettings; save: (c: Partial<AppSettings>) => void }) {
  const t = useT();
  const { outputs } = useLive();
  const universes = Math.max(1, useShow().full?.rig.universes ?? 1);
  const o = settings.outputs;
  const [addresses, setAddresses] = useState<string[]>([]);
  const [draft, setDraft] = useState(o);
  // follow the server only when the settings really changed (each state push is a new object)
  const oKey = JSON.stringify(o);
  useEffect(() => setDraft(o), [oKey]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { api.networkAddresses().then(setAddresses).catch(() => {}); }, []);

  const set = (c: Partial<OutputSettings>) => save({ outputs: { ...o, ...c } });
  const commit = () => save({ outputs: draft });
  const status = (name: string) => outputs.find((x) => x.name === name);
  const Line = ({ name }: { name: string }) => {
    const s = status(name);
    if (!s) return null;
    return (
      <span className={"chip " + (s.connected ? "ok" : "warn")} style={{ height: 24 }}>
        <span className="dot" />{s.connected ? `${s.framesPerSecond.toFixed(0)}/s · ${s.mode}` : (s.detail ?? s.lastError ?? "-")}
      </span>
    );
  };

  return (
    <div className="card stack">
      <h2>{t("out.title")}</h2>
      <div className="out-row">
        <label className="check"><input type="checkbox" checked={o.udmx} onChange={(e) => set({ udmx: e.target.checked })} /><span className="box" /> <b>uDMX</b> <span className="muted">USB</span></label>
        <Line name="uDMX" />
      </div>
      {o.udmx && universes > 1 && (
        <div className="out-fields">
          <label className="field">{t("out.udmxUniverse")}
            <select className="input" value={o.udmxUniverse ?? 1} onChange={(e) => set({ udmxUniverse: +e.target.value })}>
              {Array.from({ length: 8 }, (_, i) => i + 1).map((u) => <option key={u} value={u}>{t("patch.universeN", { n: u })}</option>)}
            </select>
          </label>
        </div>
      )}
      {universes > 1 && <p className="hint">{t("out.multiHint", { n: universes })} <Help k="out.multiHelp" /></p>}

      <div className="out-row">
        <label className="check"><input type="checkbox" checked={o.artNet} onChange={(e) => set({ artNet: e.target.checked })} /><span className="box" /> <b>Art-Net</b></label>
        <Line name="Art-Net" />
      </div>
      {o.artNet && (
        <div className="out-fields">
          <label className="field">{t("out.target")}
            <input className="input" value={draft.artNetHost} placeholder="255.255.255.255" onChange={(e) => setDraft({ ...draft, artNetHost: e.target.value })} onBlur={commit} />
          </label>
          <label className="field">{t("out.universe")}
            <input className="input num" type="number" min={0} max={32767} value={draft.artNetUniverse} onChange={(e) => setDraft({ ...draft, artNetUniverse: +e.target.value })} onBlur={commit} />
          </label>
        </div>
      )}

      <div className="out-row">
        <label className="check"><input type="checkbox" checked={o.sacn} onChange={(e) => set({ sacn: e.target.checked })} /><span className="box" /> <b>sACN</b> <span className="muted">E1.31</span></label>
        <Line name="sACN" />
      </div>
      {o.sacn && (
        <div className="out-fields">
          <label className="field">{t("out.universe")}
            <input className="input num" type="number" min={1} max={63999} value={draft.sacnUniverse} onChange={(e) => setDraft({ ...draft, sacnUniverse: +e.target.value })} onBlur={commit} />
          </label>
          <label className="field">{t("out.unicast")}
            <input className="input" value={draft.sacnUnicast} placeholder={t("out.multicast")} onChange={(e) => setDraft({ ...draft, sacnUnicast: e.target.value })} onBlur={commit} />
          </label>
          <label className="field">{t("out.priority")}
            <input className="input num" type="number" min={0} max={200} value={draft.sacnPriority} onChange={(e) => setDraft({ ...draft, sacnPriority: +e.target.value })} onBlur={commit} />
          </label>
        </div>
      )}

      {(o.artNet || o.sacn) && (
        <label className="field">{t("out.nic")}
          <select className="input" value={o.networkBind} onChange={(e) => set({ networkBind: e.target.value })}>
            <option value="">{t("out.nicDefault")}</option>
            {addresses.map((a) => <option key={a} value={a}>{a}</option>)}
          </select>
        </label>
      )}
      <p className="hint">{t("out.hint")}</p>
    </div>
  );
}
