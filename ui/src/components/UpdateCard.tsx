import { useEffect, useState } from "react";
import { api, type AppSettings } from "../api";
import { useT } from "../i18n";
import { Help } from "./Help";
import { Icon } from "./Icon";
import { onLinux } from "../store";

export interface UpdateInfo {
  current: string; latest: string | null; available: boolean; notes: string | null; url: string | null; published: string | null;
  /** The newest release's Raspberry Pi package, if it has one. */
  piUrl?: string | null;
  /** False on a Pi installed before the update helper existed (2.1.43 or older): install by hand once. */
  canInstall?: boolean;
  state: string; progress: number; error: string | null; lastCheck: string | null; repo: string;
}

/** New versions from GitHub: check, read what's new, install with one click (the show stays). */
export function UpdateCard({ settings, save }: { settings: AppSettings; save: (c: Partial<AppSettings>) => void }) {
  const t = useT();
  const [info, setInfo] = useState<UpdateInfo | null>(null);
  const [busy, setBusy] = useState(false);
  const local = ["127.0.0.1", "localhost", "[::1]"].includes(location.hostname);
  // Windows: only on the PC itself. The Pi has no screen: any paired device, once install.sh put the update helper there.
  const canInstall = onLinux() ? info?.canInstall === true : local;
  useEffect(() => {
    let alive = true;
    const load = () => api.update().then((i) => alive && setInfo(i)).catch(() => {});
    load();
    const h = setInterval(load, info?.state === "downloading" || info?.state === "installing" ? 700 : 5000);
    return () => { alive = false; clearInterval(h); };
  }, [info?.state]);

  const check = async () => { setBusy(true); try { setInfo(await api.checkUpdate()); } finally { setBusy(false); } };
  const install = async () => { if (confirm(t("upd.confirm", { v: info?.latest ?? "" }))) setInfo(await api.installUpdate()); };

  return (
    <div className="card stack">
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="refresh" /> {t("upd.title")}</h2>
        <Help k="upd.help" />
      </div>
      <p>{t("upd.current", { v: info?.current ?? "…" })}</p>
      {info?.available ? (
        <div className="note"><Icon name="info" />
          <div className="stack" style={{ gap: 6, flex: 1 }}>
            <b>{t("upd.available", { v: info.latest! })}</b>
            {info.notes && <pre className="upd-notes">{info.notes}</pre>}
            {info.state === "downloading" && <div className="upd-bar"><span style={{ width: info.progress + "%" }} /></div>}
            {info.state === "installing" && <span>{t("upd.installing")}</span>}
            {canInstall
              ? <button className="btn primary" style={{ alignSelf: "flex-start" }} disabled={info.state === "downloading" || info.state === "installing"} onClick={install}>
                  <Icon name="refresh" /> {t("upd.install")}</button>
              : <span className="hint">{t(onLinux() ? "linux.update" : "upd.onlyHere")}</span>}
          </div>
        </div>
      ) : info?.latest ? <p className="hint">{t("upd.upToDate")}</p> : null}
      {info?.error && <div className="note warn"><Icon name="warn" /><span>{info.error}</span></div>}
      <div className="row">
        <button className="btn sm" disabled={busy || info?.state === "checking"} onClick={check}>{t("upd.check")}</button>
        {info?.lastCheck && <span className="hint">{t("upd.lastCheck", { when: new Date(info.lastCheck).toLocaleString() })}</span>}
      </div>
      <label className="check">
        <input type="checkbox" checked={settings.checkUpdates ?? true} onChange={(e) => save({ checkUpdates: e.target.checked })} />
        <span className="box" /> {t("upd.auto")}
      </label>
      {info && <a className="hint" href={`https://github.com/${info.repo}/releases`} target="_blank" rel="noreferrer">github.com/{info.repo}</a>}
    </div>
  );
}
