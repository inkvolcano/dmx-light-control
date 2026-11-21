import { useEffect, useRef, useState } from "react";

/**
 * Number box that lets you type freely (also "-" or an empty field) and only reports the value on
 * Enter or when you leave the field, clamped to min..max. Without this, every keystroke was clamped
 * ("150" became "2050") or minus signs could not be typed.
 */
export function NumberField({ value, onCommit, min, max, step, allowEmpty, placeholder, className, title, disabled }: {
  value: number | null | undefined; onCommit: (v: number | null) => void;
  min?: number; max?: number; step?: number; allowEmpty?: boolean; placeholder?: string; className?: string; title?: string; disabled?: boolean;
}) {
  const [text, setText] = useState(value == null ? "" : String(value));
  const editing = useRef(false);
  useEffect(() => { if (!editing.current) setText(value == null ? "" : String(value)); }, [value]);
  const commit = () => {
    editing.current = false;
    const tx = text.trim().replace(",", ".");
    if (tx === "") {
      if (allowEmpty) { if (value != null) onCommit(null); }
      else setText(value == null ? "" : String(value));
      return;
    }
    let n = Number(tx);
    if (!Number.isFinite(n)) { setText(value == null ? "" : String(value)); return; }
    if (min != null) n = Math.max(min, n);
    if (max != null) n = Math.min(max, n);
    if (step == null || Number.isInteger(step)) n = Math.round(n);
    setText(String(n));
    if (n !== value) onCommit(n);
  };
  return (
    <input className={className ?? "input num"} inputMode="decimal" type="text" value={text} placeholder={placeholder} title={title} disabled={disabled}
      onFocus={() => (editing.current = true)} onChange={(e) => { editing.current = true; setText(e.target.value); }}
      onBlur={commit} onKeyDown={(e) => { if (e.key === "Enter") (e.target as HTMLInputElement).blur(); }} />
  );
}
