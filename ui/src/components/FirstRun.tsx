import { useEffect, useMemo, useState } from "react";
import { api, type SceneTemplateInfo } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { Icon } from "./Icon";

/**
 * The first time (no lamps yet): three steps on one card instead of three pages.
 * 1. which lamps, how many, from which address; 2. check each lamp; 3. a few ready-made scenes.
 * Everything here can also be done later on the Lampen page.
 */
export function FirstRun({ goPatch, onDone }: { goPatch: () => void; onDone: () => void }) {
  const t = useT();
  const { full, boot } = useShow();
  const [step, setStep] = useState(1);
  const types = useMemo(() => [...(boot?.library ?? [])].sort((a, b) =>
    (a.manufacturer + a.model).localeCompare(b.manufacturer + b.model)), [boot?.library]);
  const [typeId, setTypeId] = useState(types[0]?.id ?? "");
  const type = types.find((x) => x.id === typeId);
  const [mode, setMode] = useState(type?.modes[0]?.name ?? "");
  useEffect(() => { setMode(type?.modes[0]?.name ?? ""); }, [typeId]); // eslint-disable-line react-hooks/exhaustive-deps
  const footprint = type?.modes.find((m) => m.name === mode)?.footprint ?? 1;
  const [count, setCount] = useState(1);
  const [address, setAddress] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [templates, setTemplates] = useState<SceneTemplateInfo[]>([]);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const en = full?.settings.language === "en";
  useEffect(() => {
    if (step !== 3) return;
    api.sceneTemplates().then((list) => {
      setTemplates(list);
      setPicked(new Set(list.filter((x) => x.category === "basic").slice(0, 6).map((x) => x.key)));
    }).catch(() => {});
  }, [step]);

  const add = async () => {
    if (!type) return;
    setBusy(true); setError(null);
    try {
      const r = await api.addFixtures(type.id, mode, count, address);
      if (r.failed) setError(t("first.failed", { n: r.failed }));
      setStep(2);
    } catch (e) { setError(String(e)); } finally { setBusy(false); }
  };
  const flash = (id: string) => { api.identify(id, true); setTimeout(() => api.identify(id, false), 2000); };
  const finish = async () => {
    const keys = [...picked];
    if (keys.length) {
      const names = Object.fromEntries(templates.filter((x) => picked.has(x.key)).map((x) => [x.key, en ? x.nameEn : x.name]));
      await api.addSceneTemplates(keys, names).catch(() => {});
    }
    setStep(4);
  };

  return (
    <div className="card first-run">
      <div className="first-steps">
        {[1, 2, 3].map((n) => (
          <span key={n} className={"first-step" + (step === n ? " on" : step > n ? " done" : "")}>
            <b>{step > n ? "✓" : n}</b> {t("first.step" + n)}
          </span>
        ))}
      </div>

      {step === 1 && (
        <div className="stack">
          <h2>{t("first.title1")}</h2>
          <p className="hint">{t("first.hint1")}</p>
          <label className="field">{t("first.type")}
            <select className="input" value={typeId} onChange={(e) => setTypeId(e.target.value)}>
              {types.map((x) => <option key={x.id} value={x.id}>{x.manufacturer} {x.model}</option>)}
            </select>
          </label>
          <div className="first-row">
            {type && type.modes.length > 1 && (
              <label className="field">{t("first.mode")}
                <select className="input" value={mode} onChange={(e) => setMode(e.target.value)}>
                  {type.modes.map((m) => <option key={m.name} value={m.name}>{m.name}</option>)}
                </select>
              </label>
            )}
            <label className="field">{t("first.count")}
              <input className="input" type="number" min={1} max={64} value={count} onChange={(e) => setCount(Math.max(1, Math.min(64, +e.target.value || 1)))} />
            </label>
            <label className="field">{t("first.address")}
              <input className="input" type="number" min={1} max={512} value={address} onChange={(e) => setAddress(Math.max(1, Math.min(512, +e.target.value || 1)))} />
            </label>
          </div>
          <p className="hint">{t("first.addresses", {
            list: Array.from({ length: Math.min(count, 8) }, (_, i) => address + i * footprint).join(", ") + (count > 8 ? ", ..." : ""),
            n: footprint,
          })}</p>
          {error && <div className="note warn"><Icon name="warn" />{error}</div>}
          <div className="row">
            <button className="btn primary" disabled={busy || !type} onClick={add}><Icon name="plus" /> {t("first.add")}</button>
            <button className="btn ghost" onClick={goPatch}>{t("first.byHand")}</button>
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="stack">
          <h2>{t("first.title2")}</h2>
          <p className="hint">{t("first.hint2")}</p>
          {error && <div className="note warn"><Icon name="warn" />{error}</div>}
          <div className="first-lamps">
            {(full?.patch ?? []).map((p, i) => (
              <button key={p.id} className="btn" onClick={() => flash(p.id)}>
                <Icon name="eye" /> {p.name || t("first.lamp", { n: i + 1 })} <span className="muted">({p.address})</span>
              </button>
            ))}
          </div>
          <div className="row">
            <button className="btn" onClick={() => api.looks([], true, { key: "white" })}>{t("first.allWhite")}</button>
            <div className="grow" />
            <button className="btn ghost" onClick={() => setStep(1)}>{t("first.addMore")}</button>
            <button className="btn primary" onClick={() => setStep(3)}>{t("first.next")}</button>
          </div>
        </div>
      )}

      {step === 3 && (
        <div className="stack">
          <h2>{t("first.title3")}</h2>
          <p className="hint">{t("first.hint3")}</p>
          <div className="first-templates">
            {templates.slice(0, 18).map((x) => (
              <label key={x.key} className="check">
                <input type="checkbox" checked={picked.has(x.key)}
                  onChange={(e) => { const n = new Set(picked); if (e.target.checked) n.add(x.key); else n.delete(x.key); setPicked(n); }} />
                <span className="box" /> <span className="first-dot" style={{ background: x.color }} /> {en ? x.nameEn : x.name}
              </label>
            ))}
          </div>
          <div className="row">
            <div className="grow" />
            <button className="btn primary" onClick={finish}>{t("first.done")}</button>
          </div>
        </div>
      )}

      {step === 4 && (
        <div className="stack">
          <h2>{t("first.title4")}</h2>
          <p className="hint">{t("first.hint4")}</p>
          <div className="row"><button className="btn primary" onClick={onDone}><Icon name="bulb" /> {t("first.toLive")}</button></div>
        </div>
      )}
    </div>
  );
}
