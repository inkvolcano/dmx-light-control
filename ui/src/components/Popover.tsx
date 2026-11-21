import { useEffect, useRef, type ReactNode } from "react";

/** Small panel under its parent; closes on outside click or Escape. Parent needs position: relative. */
export function Popover({ onClose, children }: { onClose: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const down = (e: PointerEvent) => { if (ref.current && !ref.current.parentElement?.contains(e.target as Node)) onClose(); };
    const key = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    document.addEventListener("pointerdown", down);
    document.addEventListener("keydown", key);
    return () => { document.removeEventListener("pointerdown", down); document.removeEventListener("keydown", key); };
  }, [onClose]);
  return <div className="popover" ref={ref} onPointerDown={(e) => e.stopPropagation()}>{children}</div>;
}
