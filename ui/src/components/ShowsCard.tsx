import { useEffect, useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { Icon } from "./Icon";

/** Save and open named shows, start a new one, download/restore a backup. */
export function ShowsCard() {
  const t = useT();
  const { full } = useShow();
  const [shows, setShows] = useState<{ name: string; modified: string }[]>([]);
  const [name, setName] = useState(full!.name ?? "");
  const [msg, setMsg] = useState<string | null>(null);
  const refresh = () => api.shows().then(setShows).catch(() => {});
  useEffect(() => { refresh(); }, []);
  useEffect(() => setName(full!.name ?? ""), [full!.name]);

  const saveAs = async () => { await api.saveShow(name || "Show"); setMsg("✓ " + t("shows.saved")); refresh(); };
  const open = async (n: string) => { if (confirm(t("shows.confirmOpen", { name: n }))) { await api.openShow(n); setMsg(t("shows.opened", { name: n })); } };
  const restore = async (file: File) => {
    try { await api.restore(file); setMsg("✓ " + t("shows.restored")); refresh(); }
    catch (e) { setMsg(String(e)); }
  };

  return (
    <div className="card stack">
      <h2><Icon name="save" /> {t("shows.title")}</h2>
      <p className="hint">{t("shows.current")}: <b>{full!.name ?? t("shows.unsaved")}</b> · {t("shows.autosave")}</p>
      <div className="row" style={{ flexWrap: "nowrap" }}>
        <input className="input" style={{ flex: 1 }} value={name} placeholder={t("shows.name")} onChange={(e) => setName(e.target.value)} />
        <button className="btn primary" onClick={saveAs}><Icon name="save" /> {t("shows.saveAs")}</button>
      </div>
      {shows.length > 0 && (
        <div className="show-list">
          {shows.map((s) => (
            <div key={s.name} className="show-row">
              <b className="grow ellipsis">{s.name}</b>
              <span className="muted">{new Date(s.modified).toLocaleString()}</span>
              <button className="btn sm" onClick={() => open(s.name)}>{t("shows.open")}</button>
              <button className="btn sm ghost danger icon-btn" onClick={async () => { if (confirm(t("tl.confirmDelete", { name: s.name }))) { await api.removeShow(s.name); refresh(); } }}><Icon name="trash" /></button>
            </div>
          ))}
        </div>
      )}
      <div className="row">
        <button className="btn" onClick={async () => { if (confirm(t("shows.confirmNew"))) await api.newShow(); }}><Icon name="plus" /> {t("shows.new")}</button>
        <a className="btn" href="api/backup"><Icon name="upload" /> {t("shows.backup")}</a>
        <label className="btn">
          {t("shows.restore")}
          <input type="file" accept=".zip,.json" style={{ display: "none" }} onChange={(e) => { const f = e.target.files?.[0]; if (f && confirm(t("shows.confirmRestore"))) restore(f); e.target.value = ""; }} />
        </label>
      </div>
      <p className="hint">{t("shows.hint")}</p>
      {msg && <span className="muted">{msg}</span>}
    </div>
  );
}
