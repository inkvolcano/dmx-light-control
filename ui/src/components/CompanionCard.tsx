import { useEffect, useState } from "react";
import { api, type CompanionStatus } from "../api";
import { useT } from "../i18n";
import { Icon } from "./Icon";
import { onLinux } from "../store";

/** Install the bundled Bitfocus Companion module with one click. */
export function CompanionCard() {
  const t = useT();
  const [st, setSt] = useState<CompanionStatus | null>(null);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const refresh = () => api.companionStatus().then(setSt).catch(() => {});
  useEffect(() => { refresh(); }, []);

  const install = async () => {
    setBusy(true);
    setMsg(null);
    try {
      const r = await api.installCompanion();
      setSt(r.status);
      setMsg({ ok: true, text: t(r.status.companionRunning ? "cm.doneRestart" : "cm.done") });
    } catch (e) {
      setMsg({ ok: false, text: String(e).replace(/^Error:\s*/, "") });
    }
    setBusy(false);
  };

  return (
    <div className="card stack">
      <h2><Icon name="grid" /> {t("cm.title")}</h2>
      <p className="hint">{t("cm.hint")}</p>
      {st && (
        <div className="diag">
          <span>{t("cm.companion")}</span>
          <b className={st.companionFound ? "" : "warn-text"}>{st.companionFound ? (st.companionRunning ? t("cm.running") : t("cm.found")) : t("cm.notFound")}</b>
          <span>{t("cm.module")}</span>
          <b>{st.installedVersion ? `${st.installedVersion}${st.upToDate ? " ✓" : ""}` : t("cm.notInstalled")}</b>
          <span>{t("cm.bundled")}</span>
          <b>{st.sourceFound ? st.sourceVersion : t("cm.missing")}</b>
        </div>
      )}
      {onLinux() ? <div className="note"><Icon name="info" />{t("linux.companion")}</div> : <>
      {st && !st.companionFound && <div className="note"><Icon name="info" />{t("cm.installCompanion")}</div>}
      <div className="row">
        <button className="btn primary" disabled={busy || !st?.sourceFound} onClick={install}>
          <Icon name="upload" /> {busy ? t("cm.installing") : st?.installedVersion ? (st.upToDate ? t("cm.reinstall") : t("cm.update")) : t("cm.install")}
        </button>
        <button className="btn ghost" onClick={refresh}><Icon name="refresh" /></button>
      </div></>}
      {msg && <div className={"note" + (msg.ok ? "" : " warn")}><Icon name={msg.ok ? "info" : "warn"} />{msg.text}</div>}
      {st?.installedVersion && <p className="hint">{t("cm.noActions")}</p>}
      {st && <p className="hint mono" style={{ fontSize: 11 }}>{st.modulesDir}</p>}
    </div>
  );
}
