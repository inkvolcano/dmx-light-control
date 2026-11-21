import { useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";

/** PIN prompt that unlocks the full app from the (locked) easy mode. */
export function PinDialog({ onDone, onCancel }: { onDone: (pin: string) => void; onCancel: () => void }) {
  const t = useT();
  const [pin, setPin] = useState("");
  const [wrong, setWrong] = useState(false);
  const submit = async () => {
    try { await api.unlock(pin); onDone(pin); } catch { setWrong(true); setPin(""); }
  };
  return (
    <div className="offline" onClick={onCancel}>
      <div className="box pin-box" onClick={(e) => e.stopPropagation()}>
        <h2>{t("op.enterPin")}</h2>
        <input className={"input pin-input" + (wrong ? " invalid" : "")} type="password" inputMode="numeric" autoFocus value={pin}
          onChange={(e) => { setPin(e.target.value); setWrong(false); }} onKeyDown={(e) => e.key === "Enter" && submit()} />
        {wrong && <span className="warn-text">{t("op.wrongPin")}</span>}
        <div className="row">
          <button className="btn ghost" onClick={onCancel}>{t("common.cancel")}</button>
          <button className="btn primary" onClick={submit}>{t("op.unlock")}</button>
        </div>
      </div>
    </div>
  );
}
