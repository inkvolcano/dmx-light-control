import { useState } from "react";
import { api } from "../api";
import { translate } from "../i18n";
import { pairingSucceeded } from "../store";

const lang = () => (navigator.language?.toLowerCase().startsWith("nl") ? "nl" : "en");

/**
 * Shown on a phone/tablet that is not paired yet (the app answers "pair" to everything else).
 * The code is on the PC: Settings > Phone or tablet. Scanning the QR code there pairs automatically.
 */
export function PairScreen() {
  const t = (k: string) => translate(lang(), k);
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const submit = async () => {
    setBusy(true); setError(null);
    try {
      await api.pair(code.replace(/\D/g, ""), deviceName());
      pairingSucceeded();
    } catch (e) {
      const m = String(e);
      setError(m.includes("too many") ? t("pair.tooMany") : t("pair.wrong"));
    } finally { setBusy(false); }
  };
  return (
    <div className="pair-screen">
      <div className="pair-box">
        <div className="brand-dot" />
        <h1>{t("pair.title")}</h1>
        <p className="hint">{t("pair.hint")}</p>
        <input className={"input pin-input" + (error ? " invalid" : "")} inputMode="numeric" autoComplete="one-time-code" maxLength={7}
          placeholder="123456" value={code} onChange={(e) => { setCode(e.target.value); setError(null); }}
          onKeyDown={(e) => e.key === "Enter" && submit()} autoFocus />
        {error && <p className="warn-text">{error}</p>}
        <button className="btn primary" disabled={busy || code.replace(/\D/g, "").length !== 6} onClick={submit}>{t("pair.button")}</button>
        <p className="hint small">{t("pair.where")}</p>
      </div>
    </div>
  );
}

/** A readable name for the device list on the PC, e.g. "iPhone (Safari)". */
export function deviceName() {
  const ua = navigator.userAgent;
  const dev = /iPhone/.test(ua) ? "iPhone" : /iPad/.test(ua) ? "iPad" : /Android/.test(ua) ? "Android" : /Windows/.test(ua) ? "Windows" : /Mac/.test(ua) ? "Mac" : "Apparaat";
  const browser = /Edg\//.test(ua) ? "Edge" : /CriOS|Chrome\//.test(ua) ? "Chrome" : /FxiOS|Firefox\//.test(ua) ? "Firefox" : /Safari\//.test(ua) ? "Safari" : "";
  return browser ? `${dev} (${browser})` : dev;
}
