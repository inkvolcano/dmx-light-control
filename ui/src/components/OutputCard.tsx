import { useState } from "react";
import { api, type AppSettings } from "../api";
import { useT } from "../i18n";
import { onLinux, useLive } from "../store";
import { Help } from "./Help";
import { Icon } from "./Icon";

function since(iso: string | null, t: (k: string, v?: Record<string, string | number>) => string) {
  if (!iso) return "-";
  const s = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (s < 90) return t("diag.seconds", { n: Math.round(s) });
  if (s < 5400) return t("diag.minutes", { n: Math.round(s / 60) });
  return t("diag.hours", { n: (s / 3600).toFixed(1) });
}

/** Dongle status, counters, tips for drop-outs, and the log. */
export function OutputCard({ settings, save }: { settings: AppSettings; save: (c: Partial<AppSettings>) => void }) {
  const t = useT();
  const { output: o } = useLive();
  const [log, setLog] = useState<string | null>(null);
  const [rec, setRec] = useState<{ busy: boolean; result: string | null; offerDeep: boolean; ok: boolean }>({ busy: false, result: null, offerDeep: false, ok: false });

  const recover = async (deep: boolean) => {
    setRec({ busy: true, result: t(deep ? "usb.restarting" : "usb.recovering"), offerDeep: false, ok: false });
    try {
      const r = await api.recover(deep);
      if (r.connected) setRec({ busy: false, result: t("usb.ok"), offerDeep: false, ok: true });
      else if (!r.devicePresent) setRec({ busy: false, result: t("usb.notSeen"), offerDeep: false, ok: false });
      else if (r.cancelled) setRec({ busy: false, result: t("usb.cancelled"), offerDeep: true, ok: false });
      else setRec({ busy: false, result: t(deep ? "usb.stillNot" : "usb.softFailed"), offerDeep: !deep, ok: false });
    } catch (e) {
      setRec({ busy: false, result: String(e), offerDeep: false, ok: false });
    }
  };
  const [power, setPower] = useState<{ busy: boolean; msg: string | null; ok: boolean }>({ busy: false, msg: null, ok: false });
  const usbPower = async () => {
    if (!confirm(t("usb.powerConfirm"))) return;
    setPower({ busy: true, msg: t("usb.powerBusy"), ok: false });
    try {
      const r = await api.usbPower();
      setPower({ busy: false, msg: t("usb.power." + (["ok", "cancelled", "notfound"].includes(r.result) ? r.result : "failed"), { error: r.result }), ok: r.result === "ok" });
    } catch (e) { setPower({ busy: false, msg: String(e), ok: false }); }
  };
  const linux = onLinux();
  const troubled = o.reconnects > 0 || o.errors > o.recovered || (!o.connected && o.fault !== "NotFound");
  const tips = o.fault === "InUse" ? ["tip.inUse"]
    : o.fault === "DriverMissing" ? [linux ? "linux.tipDriver" : "tip.driver"]
    : troubled ? (linux ? ["tip.usb2", "tip.hub", "linux.tipPower", "tip.cable", "tip.dmxLine"]
      : ["tip.usb2", "tip.hub", "tip.power", "tip.selective", "tip.battery", "tip.cable", "tip.dmxLine"]) : [];

  return (
    <div className="card stack">
      <div className="row">
        <h2 style={{ flex: 1 }}>{t("settings.output")}</h2>
        <span className={"chip " + (o.connected ? "ok" : "warn")}><span className="dot" />
          {o.connected ? t("status.connected", { name: o.name }) : t("faultShort." + (o.fault ?? "Other"), { name: o.name })}
        </span>
      </div>

      {!o.connected && (
        <div className="note warn"><Icon name="warn" />
          <span><b>{t((linux && o.fault === "DriverMissing" ? "linux.fault." : "fault.") + (o.fault ?? "Other"))}</b>{o.detail && o.fault !== "NotFound" ? <><br /><span className="mono">{o.detail}</span></> : null}</span>
        </div>
      )}

      <div className="diag">
        <span>{t("diag.connectedFor")}</span><b>{o.connected ? since(o.connectedSince, t) : "-"}</b>
        <span>{t("diag.mode")}</span><b>{o.mode === "single" ? t("diag.single") : o.mode ? t("diag.block", { n: o.mode.replace("block ", "") }) : "-"}</b>
        <span>{t("diag.rate")}</span><b>{o.framesPerSecond.toFixed(1)} /s</b>
        <span>{t("diag.reconnects")}</span><b className={o.reconnects ? "warn-text" : ""}>{o.reconnects}</b>
        <span>{t("diag.errors")}</span><b className={o.errors > o.recovered ? "warn-text" : ""}>{o.errors} <span className="muted">({t("diag.recovered", { n: o.recovered })})</span></b>
        {o.lastError && <><span>{t("diag.lastError")}</span><b className="mono" style={{ fontWeight: 400 }}>{o.lastError} · {since(o.lastErrorAt, t)}</b></>}
      </div>

      <label className="field">
        {t("settings.dongleSpeed")}
        <select className="input" value={settings.dongleSpeed} onChange={(e) => save({ dongleSpeed: +e.target.value })}>
          {[0, 1, 2].map((i) => <option key={i} value={i}>{t("settings.speed" + i)}</option>)}
        </select>
      </label>
      {!linux && <label className="check">
        <input type="checkbox" checked={settings.preventSleep} onChange={(e) => save({ preventSleep: e.target.checked })} />
        <span className="box" /> {t("settings.preventSleep")}
      </label>}
      <div className="row">
        <button className="btn primary" disabled={rec.busy} onClick={() => recover(false)} title={t("usb.hint")}><Icon name="power" /> {t("usb.recover")}</button>
        <button className="btn" onClick={() => api.reconnect()}><Icon name="refresh" /> {t("status.reconnect")}</button>
        <button className="btn ghost" onClick={async () => setLog(log ? null : await api.log())}>{t("diag.log")}</button>
      </div>
      {!linux && <div className="row">
        <button className="btn" disabled={power.busy} onClick={usbPower}><Icon name="bolt" /> {t("usb.powerButton")}</button>
        <Help k="usb.powerHelp" />
      </div>}
      {power.msg && <div className={"note" + (power.ok ? "" : " warn")}><Icon name={power.ok ? "info" : "warn"} /><span>{power.msg}</span></div>}

      {rec.result && (
        <div className={"note" + (rec.ok ? "" : " warn")}>
          <Icon name={rec.ok ? "info" : "warn"} />
          <span>{rec.result}
            {rec.offerDeep && !linux && <><br /><button className="btn sm" style={{ marginTop: 6 }} disabled={rec.busy} onClick={() => recover(true)}>{t("usb.deep")}</button></>}
          </span>
        </div>
      )}
      {tips.length > 0 && (
        <div className="stack" style={{ gap: 6 }}>
          <h3>{t("diag.tips")}</h3>
          <ul className="tips">{tips.map((k) => <li key={k}>{t(k)}</li>)}</ul>
        </div>
      )}
      {log !== null && <pre className="log">{log || "-"}</pre>}
    </div>
  );
}
