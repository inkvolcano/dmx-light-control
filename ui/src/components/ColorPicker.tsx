import { useEffect, useRef } from "react";

/** Native colour input that reports only the final pick (not every drag step). */
export function ColorInput({ value, onPick, title }: { value: string; onPick: (hex: string) => void; title?: string }) {
  const ref = useRef<HTMLInputElement>(null);
  const cb = useRef(onPick);
  cb.current = onPick;
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const h = () => cb.current(el.value.toUpperCase());
    el.addEventListener("change", h);
    return () => el.removeEventListener("change", h);
  }, []);
  return <input ref={ref} type="color" defaultValue={value.toLowerCase()} key={value} title={title} />;
}

export function contrast(hex: string) {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  return 0.299 * r + 0.587 * g + 0.114 * b > 140 ? "#111" : "#fff";
}
