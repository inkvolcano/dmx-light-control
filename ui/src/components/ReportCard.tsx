import { useEffect, useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";
import { Help } from "./Help";
import { Icon } from "./Icon";

/** "Probleemrapport": one zip (log, crashes, PC/USB info) that someone can send to whoever helps them. */
export function ReportCard({ compact }: { compact?: boolean }) {
  const t = useT();
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [path, setPath] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<{ crashes: number; lastCrash: string | null } | null>(null);
  useEffect(() => { api.reportInfo().then(setInfo).catch(() => {}); }, [path]);

  const make = async () => {
    setBusy(true); setErr(null); setPath(null);
    try { setPath((await api.saveReport(note)).path); }
    catch (e) { setErr(String(e)); }
    finally { setBusy(false); }
  };

  return (
    <div className={compact ? "stack" : "card stack"}>
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="warn" /> {t("report.title")}</h2>
        <Help k="report.help" />
      </div>
      <p className="hint">{t("report.hint")}</p>
      {info && info.crashes > 0 && (
        <div className="note warn"><Icon name="warn" />
          <span>{t("report.crashes", { n: info.crashes, when: info.lastCrash ? new Date(info.lastCrash).toLocaleString() : "?" })}</span>
        </div>
      )}
      <label className="field">
        {t("report.note")}
        <textarea className="input" rows={3} value={note} maxLength={2000} placeholder={t("report.notePlaceholder")} onChange={(e) => setNote(e.target.value)} />
      </label>
      <div className="row">
        <button className="btn primary" disabled={busy} onClick={make}>
          {busy ? <span className="spinner" /> : <Icon name="save" />} {busy ? t("report.busy") : t("report.make")}
        </button>
        <a className="btn ghost" href={"api/report.zip?note=" + encodeURIComponent(note)} download>{t("report.download")}</a>
      </div>
      {path && <div className="note"><Icon name="info" /><span>{t("report.done")}<br /><span className="mono">{path}</span></span></div>}
      {err && <div className="note warn"><Icon name="warn" /><span>{err}</span></div>}
    </div>
  );
}
