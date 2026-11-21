import { useEffect, useState } from "react";
import { api, type AppSettings } from "../api";
import { useT } from "../i18n";
import { Help } from "./Help";
import { Icon } from "./Icon";
import { QrCode } from "./QrCode";

interface PhoneInfo {
  allowNetwork: boolean; port: number; hostname: string; firewall: boolean; fixedName: string; fixedNameStatus: string;
  addresses: { ip: string; network: string; preferred: boolean }[];
  clients: { ip: string; since: string }[];
}

/** Use the app from a phone or tablet: switch on, scan the QR code, firewall, home-screen app. */
export function PhoneCard({ settings, save }: { settings: AppSettings; save: (c: Partial<AppSettings>) => void }) {
  const t = useT();
  const [info, setInfo] = useState<PhoneInfo | null>(null);
  const [fw, setFw] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);
  const [os, setOs] = useState<"iphone" | "android">(() => (/android/i.test(navigator.userAgent) ? "android" : "iphone"));
  const [pick, setPick] = useState(0);
  const [code, setCode] = useState<string | null>(null);
  const [useName, setUseName] = useState(true);
  const pairing = settings.requirePairing ?? true;
  const devices = settings.devices ?? [];
  // the pairing code changes after each pairing: keep it fresh
  useEffect(() => {
    if (!settings.allowNetwork || !pairing) return;
    let alive = true;
    const load = () => api.pairCode().then((r) => alive && setCode(r.code)).catch(() => {});
    load();
    const h = setInterval(load, 3000);
    return () => { alive = false; clearInterval(h); };
  }, [settings.allowNetwork, pairing, devices.length]);

  useEffect(() => {
    let alive = true;
    const load = () => api.phone().then((i) => alive && setInfo(i)).catch(() => {});
    load();
    const h = setInterval(load, 3000);
    return () => { alive = false; clearInterval(h); };
  }, [settings.allowNetwork]);

  const addrs = info?.addresses ?? [];
  const addr = addrs[Math.min(pick, Math.max(0, addrs.length - 1))];
  const nameOk = !!info && info.fixedNameStatus === "on";
  const host = useName && nameOk ? info!.fixedName : addr?.ip;
  const base = host ? `http://${host}:${info!.port}/` : "";
  const url = base ? base + "?easy" + (pairing && code ? "&pair=" + code : "") : "";
  const copy = (s: string) => { navigator.clipboard?.writeText(s); setCopied(s); setTimeout(() => setCopied(null), 1200); };
  const firewall = async () => {
    setFw(t("phone.fwBusy"));
    const r = await api.phoneFirewall().catch((e) => ({ result: String(e) }));
    setFw(t("phone.fw." + (["ok", "cancelled"].includes(r.result) ? r.result : "failed"), { error: r.result }));
  };

  return (
    <div className="card stack phone-card">
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="link" /> {t("phone.title")}</h2>
        <Help k="phone.help" />
      </div>
      <label className="check">
        <input type="checkbox" checked={settings.allowNetwork} onChange={(e) => save({ allowNetwork: e.target.checked })} />
        <span className="box" /> <b>{t("phone.enable")}</b>
      </label>
      {!settings.allowNetwork && <p className="hint">{t("phone.offHint")}</p>}

      {settings.allowNetwork && info && (
        <>
          {!info.allowNetwork && <div className="note"><Icon name="info" />{t("phone.starting")}</div>}
          <div className="row">
            <label className="field" style={{ flex: 1, minWidth: 160 }}>{t("phone.fixedName")}
              <input className="input mono" defaultValue={settings.hostName ?? "dmxlicht"} key={settings.hostName}
                onBlur={(e) => e.target.value.trim() && e.target.value.trim() !== settings.hostName && save({ hostName: e.target.value.trim() })} />
            </label>
            <span className={"chip " + (nameOk ? "ok" : "warn")}><span className="dot" />{nameOk ? info!.fixedName : t("phone.fixedNameOff")}</span>
            <Help k="phone.fixedNameHelp" />
          </div>
          <div className="seg" style={{ alignSelf: "flex-start" }}>
            <button className={useName && nameOk ? "active" : ""} disabled={!nameOk} onClick={() => setUseName(true)}>{t("phone.qrName")}</button>
            <button className={!useName || !nameOk ? "active" : ""} onClick={() => setUseName(false)}>{t("phone.qrIp")}</button>
          </div>
          {addrs.length === 0 ? (
            <div className="note warn"><Icon name="warn" />{t("phone.noNetwork")}</div>
          ) : (
            <div className="phone-qr">
              <QrCode text={url} size={170} />
              <div className="stack" style={{ gap: 8, minWidth: 0 }}>
                <p className="hint" style={{ margin: 0 }}>{t("phone.scan")}</p>
                <div className="url-row"><code className="wrap">{url}</code>
                  <button className="btn sm ghost" onClick={() => copy(url)}>{copied === url ? "✓" : t("int.copy")}</button></div>
                {addrs.length > 1 && (
                  <label className="field">{t("phone.otherNetwork")}
                    <select className="input" value={pick} onChange={(e) => setPick(+e.target.value)}>
                      {addrs.map((a, i) => <option key={a.ip} value={i}>{a.ip} ({a.network})</option>)}
                    </select>
                  </label>
                )}
              </div>
            </div>
          )}

          <div className={"note" + (info.clients.length ? "" : "")}>
            <Icon name={info.clients.length ? "users" : "info"} />
            <span>{info.clients.length
              ? t("phone.connected", { list: info.clients.map((c) => c.ip).join(", ") })
              : t("phone.noneYet")}</span>
          </div>

          <div className="stack" style={{ gap: 6 }}>
            <div className="row">
              <span className="grow">{info.firewall ? "✓ " + t("phone.fwOk") : t("phone.fwMissing")}</span>
              {!info.firewall && <button className="btn sm" onClick={firewall}><Icon name="lock" /> {t("phone.fwButton")}</button>}
              <Help k="phone.fwHelp" />
            </div>
            {fw && <p className="hint">{fw}</p>}
          </div>

          <div className="stack" style={{ gap: 6 }}>
            <label className="check">
              <input type="checkbox" checked={pairing} onChange={(e) => save({ requirePairing: e.target.checked })} />
              <span className="box" /> <b>{t("phone.pairing")}</b> <Help k="phone.pairingHelp" />
            </label>
            {pairing && (
              <div className="row">
                <span className="muted">{t("phone.code")}</span>
                <b className="pair-code">{code ?? "······"}</b>
                <button className="btn sm ghost" onClick={() => api.newPairCode().then((r) => setCode(r.code))}>{t("phone.newCode")}</button>
              </div>
            )}
            {!pairing && <div className="note warn"><Icon name="warn" />{t("phone.noPairingWarn")}</div>}
            {devices.length > 0 && (
              <div className="stack" style={{ gap: 4 }}>
                <span className="muted small">{t("phone.devices")}</span>
                {devices.map((d) => (
                  <div key={d.id} className="row device-row">
                    <span className="grow">{d.name}</span>
                    <span className="muted small">{t("phone.lastSeen", { when: new Date(d.lastSeen).toLocaleString() })}</span>
                    <button className="btn sm ghost danger" onClick={() => api.removeDevice(d.id)}>{t("phone.remove")}</button>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="seg" style={{ alignSelf: "flex-start" }}>
            <button className={os === "iphone" ? "active" : ""} onClick={() => setOs("iphone")}>iPhone / iPad</button>
            <button className={os === "android" ? "active" : ""} onClick={() => setOs("android")}>Android</button>
          </div>
          <ol className="steps">
            {[1, 2, 3, 4].map((i) => <li key={i}>{t(`phone.${os}.${i}`)}</li>)}
          </ol>
          <p className="hint">{t("phone.trouble")}</p>
        </>
      )}
    </div>
  );
}

/** What the easy mode (phones, operators) shows. */
export function EasyCard({ settings, save, openEasy }: { settings: AppSettings; save: (c: Partial<AppSettings>) => void; openEasy: () => void }) {
  const t = useT();
  const e = settings.easy ?? { colors: true, effects: true, groups: true, timelines: true, music: true, strobe: true };
  const keys = ["colors", "effects", "groups", "music", "timelines", "strobe"] as const;
  return (
    <div className="card stack">
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="grid" /> {t("easy.settingsTitle")}</h2>
        <Help k="easy.settingsHelp" />
      </div>
      <p className="hint">{t("easy.settingsHint")}</p>
      {keys.map((k) => (
        <label key={k} className="check">
          <input type="checkbox" checked={e[k]} onChange={(ev) => save({ easy: { ...e, [k]: ev.target.checked } })} />
          <span className="box" /> {t("easy.show." + k)}
        </label>
      ))}
      <button className="btn" style={{ alignSelf: "flex-start" }} onClick={openEasy}><Icon name="grid" /> {t("easy.openHere")}</button>
    </div>
  );
}
