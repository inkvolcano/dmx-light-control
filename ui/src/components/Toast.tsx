import { useEffect, useRef, useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";
import { Icon } from "./Icon";

/** Shows a short message at the bottom, with "Undo" when the change can be undone (Ctrl+Z does the same). */
export function toast(text: string, undo = true) {
  window.dispatchEvent(new CustomEvent("dmx-toast", { detail: { text, undo } }));
}

export function Toaster() {
  const t = useT();
  const [msg, setMsg] = useState<{ text: string; undo: boolean; n: number } | null>(null);
  const timer = useRef<number>();
  useEffect(() => {
    let n = 0;
    const on = (e: Event) => {
      const d = (e as CustomEvent<{ text: string; undo: boolean }>).detail;
      setMsg({ ...d, n: ++n });
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => setMsg(null), 6000);
    };
    window.addEventListener("dmx-toast", on);
    return () => { window.removeEventListener("dmx-toast", on); window.clearTimeout(timer.current); };
  }, []);
  if (!msg) return null;
  return (
    <div className="toast" role="status" key={msg.n}>
      <Icon name="info" />
      <span className="grow">{msg.text}</span>
      {msg.undo && <button className="btn sm" onClick={() => { api.undo(); setMsg(null); }}><Icon name="undo" /> {t("toast.undo")}</button>}
      <button className="btn sm ghost icon-btn" aria-label={t("toast.close")} onClick={() => setMsg(null)}>×</button>
    </div>
  );
}
