import { useState } from "react";
import { api, type AppSettings } from "../api";
import { Icon } from "../components/Icon";
import { OutputCard } from "../components/OutputCard";
import { OutputsCard } from "../components/OutputsCard";
import { EasyCard, PhoneCard } from "../components/PhoneCard";
import { ReportCard } from "../components/ReportCard";
import { UpdateCard } from "../components/UpdateCard";
import { ShowsCard } from "../components/ShowsCard";
import { languages, useT } from "../i18n";
import { reloadBootstrap, updateSettings, useShow } from "../store";

function LockCard({ hasPin, locked }: { hasPin: boolean; locked: boolean }) {
  const t = useT();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [msg, setMsg] = useState<string | null>(null);
  const run = async (r: { newPin?: string; locked?: boolean }) => {
    try { await api.lockSetup({ pin: current || (sessionStorage.getItem("pin") ?? undefined), ...r }); setMsg("✓"); setNext(""); }
    catch { setMsg(t("op.wrongPin")); }
  };
  return (
    <div className="card stack">
      <h2><Icon name="lock" /> {t("op.title")}</h2>
      <p className="hint">{t("op.hint")}</p>
      {hasPin && !sessionStorage.getItem("pin") && (
        <label className="field">{t("op.currentPin")}<input className="input" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} /></label>
      )}
      <div className="row" style={{ alignItems: "flex-end" }}>
        <label className="field">{hasPin ? t("op.newPin") : t("op.setPin")}<input className="input" type="password" inputMode="numeric" value={next} onChange={(e) => setNext(e.target.value)} /></label>
        <button className="btn" disabled={!next} onClick={() => { run({ newPin: next }); try { sessionStorage.setItem("pin", next); } catch { /* ignore */ } }}>{t("fe.save")}</button>
        {hasPin && <button className="btn ghost danger" onClick={() => run({ newPin: "", locked: false })}>{t("op.removePin")}</button>}
      </div>
      <label className="check">
        <input type="checkbox" checked={locked} disabled={!hasPin} onChange={(e) => run({ locked: e.target.checked })} />
        <span className="box" /> {t("op.startLocked")}
      </label>
      {msg && <span className="muted">{msg}</span>}
    </div>
  );
}

export function SettingsPage() {
  const t = useT();
  const { full, boot } = useShow();
  const [note, setNote] = useState<string | null>(null);
  const s = full!.settings;

  const save = async (change: Partial<AppSettings>) => {
    const r = await updateSettings(change);
    if (r?.note === "restart") setNote(t("settings.restart"));
  };

  return (
    <div>
      <div className="page-head"><h1>{t("settings.title")}</h1></div>
      {note && <div className="note" style={{ marginBottom: 14 }}><Icon name="info" />{note}</div>}
      <div className="settings-grid">
        <div className="card stack">
          <h2>{t("settings.language")}</h2>
          <div className="seg">
            {languages.map((l) => (
              <button key={l.code} className={s.language === l.code ? "active" : ""} onClick={() => save({ language: l.code })}>{l.label}</button>
            ))}
          </div>
        </div>

        <PhoneCard settings={s} save={save} />
        <EasyCard settings={s} save={save} openEasy={() => window.dispatchEvent(new Event("dmx-easy"))} />
        <ShowsCard />
        <OutputCard settings={s} save={save} />
        <ReportCard />
        <UpdateCard settings={s} save={save} />
        <OutputsCard settings={s} save={save} />

        <div className="card stack">
          <label className="check">
            <input type="checkbox" checked={full!.state.useWhiteLed} onChange={(e) => api.master({ useWhiteLed: e.target.checked })} />
            <span className="box" /> {t("settings.whiteLed")}
          </label>
        </div>

        <div className="card stack">
          <h2>{t("patch.library")}</h2>
          <p className="hint">{t("settings.libraryHint")}</p>
          <div className="row">
            <button className="btn" onClick={async () => { await api.reloadLibrary(); await reloadBootstrap(); }}>
              <Icon name="refresh" /> {t("settings.reloadLibrary")}
            </button>
            <span className="muted">{boot!.library.length} types</span>
          </div>
        </div>

        <LockCard hasPin={!!s.hasPin} locked={s.locked} />

        <div className="card stack">
          <h2>{t("settings.about")}</h2>
          <p className="hint">Glowsmith · {t("settings.version", { v: boot!.version })}</p>
        </div>
      </div>
    </div>
  );
}
