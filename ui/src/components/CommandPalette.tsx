import { useEffect, useMemo, useRef, useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";
import { pages } from "../pages/pageList";
import { useShow } from "../store";
import { Icon } from "./Icon";

type Item = { id: string; label: string; kind: string; icon: string; swatch?: string; run: () => void };

/** Lower case without accents, so "scene" finds "scène". */
const norm = (s: string) => s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();

/**
 * Ctrl+K (or the search button): type a few letters of a scene, effect, colour, page or action and press Enter.
 * Words may come in any order ("rood scene", "wit warm").
 */
export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const t = useT();
  const { full, boot } = useShow();
  const [q, setQ] = useState("");
  const [sel, setSel] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => { if (open) { setQ(""); setSel(0); setTimeout(() => input.current?.focus(), 0); } }, [open]);

  const items = useMemo<Item[]>(() => {
    if (!full || !boot) return [];
    const s = full.state;
    const goto = (p: string) => window.dispatchEvent(new CustomEvent("dmx-goto", { detail: p }));
    const out: Item[] = [];
    for (const sc of full.scenes)
      out.push({ id: "s" + sc.id, label: sc.name, kind: t("cmd.scene"), icon: "bulb", swatch: sc.color ?? undefined, run: () => api.recallScene(sc.id) });
    out.push(
      { id: "a-on", label: t("cmd.allOn"), kind: t("cmd.action"), icon: "power", run: () => api.looks([], true) },
      { id: "a-off", label: t("cmd.allOff"), kind: t("cmd.action"), icon: "power", run: () => api.looks([], false) },
      { id: "a-bo", label: s.blackout ? t("cmd.blackoutOff") : t("cmd.blackoutOn"), kind: t("cmd.action"), icon: "moon", run: () => api.master({ blackout: !s.blackout }) },
      { id: "a-stop", label: t("cmd.stopEffect"), kind: t("cmd.action"), icon: "stop", run: () => api.mode("static") },
      { id: "a-screen", label: t("ss.start"), kind: t("cmd.action"), icon: "monitor", run: () => api.effect({ ...s.effect, screenSync: true, chaseId: null }, true) },
      { id: "a-tap", label: "TAP", kind: t("cmd.action"), icon: "music", run: () => api.tap() },
    );
    for (const c of [...boot.palette.fan, ...boot.palette.specials])
      out.push({ id: "c" + c.key, label: t("color." + c.key, undefined, c.name), kind: t("cmd.color"), icon: "", swatch: c.hex, run: () => api.looks([], true, { key: c.key }) });
    for (const e of boot.effects)
      out.push({ id: "e" + e.index, label: t("fxname." + e.key, undefined, e.name), kind: t("cmd.effect"), icon: "sparkles",
        run: () => api.effect({ ...s.effect, effectIndex: e.index, chaseId: null, screenSync: false }, true) });
    for (const c of s.chases ?? [])
      out.push({ id: "ch" + c.id, label: c.name, kind: t("cmd.chase"), icon: "sparkles", run: () => api.effect({ ...s.effect, chaseId: c.id, screenSync: false }, true) });
    for (const p of pages)
      out.push({ id: "p" + p.id, label: t("nav." + p.id), kind: t("cmd.page"), icon: p.icon, run: () => goto(p.id) });
    return out;
  }, [full, boot, t]);

  const shown = useMemo(() => {
    const words = norm(q).split(/\s+/).filter(Boolean);
    if (!words.length) return items.filter((i) => i.kind === t("cmd.scene") || i.kind === t("cmd.action")).slice(0, 30);
    return items
      .map((i) => {
        const hay = norm(i.label + " " + i.kind);
        if (!words.every((w) => hay.includes(w))) return null;
        // names that start with what you typed first, then shorter names
        const score = (norm(i.label).startsWith(words[0]) ? 0 : 1) * 1000 + i.label.length;
        return { i, score };
      })
      .filter((x): x is { i: Item; score: number } => !!x)
      .sort((a, b) => a.score - b.score)
      .slice(0, 40)
      .map((x) => x.i);
  }, [items, q, t]);

  useEffect(() => { setSel(0); }, [q]);
  useEffect(() => { list.current?.querySelector(".cmd-item.sel")?.scrollIntoView({ block: "nearest" }); }, [sel]);

  if (!open) return null;
  const pick = (i: Item | undefined) => { if (!i) return; onClose(); i.run(); };
  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") { e.preventDefault(); onClose(); }
    else if (e.key === "ArrowDown") { e.preventDefault(); setSel((x) => Math.min(shown.length - 1, x + 1)); }
    else if (e.key === "ArrowUp") { e.preventDefault(); setSel((x) => Math.max(0, x - 1)); }
    else if (e.key === "Enter") { e.preventDefault(); pick(shown[sel]); }
  };
  return (
    <div className="cmd-backdrop" onPointerDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="cmd" role="dialog" aria-label={t("cmd.title")}>
        <div className="cmd-search">
          <Icon name="search" />
          <input ref={input} className="cmd-input" value={q} placeholder={t("cmd.placeholder")} onChange={(e) => setQ(e.target.value)} onKeyDown={onKey} />
          <kbd>Esc</kbd>
        </div>
        <div className="cmd-list" ref={list}>
          {shown.map((i, n) => (
            <button key={i.id} className={"cmd-item" + (n === sel ? " sel" : "")} onPointerEnter={() => setSel(n)} onClick={() => pick(i)}>
              {i.swatch && !i.icon ? <span className="cmd-swatch" style={{ background: i.swatch }} />
                : <span className="cmd-icon" style={i.swatch ? { color: i.swatch } : undefined}><Icon name={i.icon} size={16} /></span>}
              <span className="cmd-label">{i.label}</span>
              <span className="cmd-kind">{i.kind}</span>
            </button>
          ))}
          {!shown.length && <p className="hint" style={{ padding: 14 }}>{t("cmd.none")}</p>}
        </div>
      </div>
    </div>
  );
}
