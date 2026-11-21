import { useEffect, useState } from "react";
import { useT } from "../i18n";
import { Icon } from "./Icon";
import { ReportCard } from "./ReportCard";
import { Popover } from "./Popover";

/** Small "?" next to a control; click shows a short explanation (translation key k). */
export function Help({ k, vars }: { k: string; vars?: Record<string, string | number> }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <span className="help-q" onClick={(e) => { e.preventDefault(); e.stopPropagation(); }}>
      <button type="button" className="help-dot" aria-label={t("help.what")} onClick={() => setOpen(!open)}>?</button>
      {open && <Popover onClose={() => setOpen(false)}><div className="help-pop">{t(k, vars)}</div></Popover>}
    </span>
  );
}

const TABS = ["page", "start", "trouble", "keys", "glossary"] as const;
type Tab = (typeof TABS)[number];

/** Lines of a help text: one per line; "# " = heading, "- " = bullet, "1. " = numbered. */
function Text({ text }: { text: string }) {
  const out: React.ReactNode[] = [];
  let list: { ordered: boolean; items: string[] } | null = null;
  const flush = () => {
    if (!list) return;
    const items = list.items.map((x, i) => <li key={i}>{x}</li>);
    out.push(list.ordered ? <ol key={out.length}>{items}</ol> : <ul key={out.length}>{items}</ul>);
    list = null;
  };
  for (const raw of text.split("\n")) {
    const line = raw.trim();
    if (!line) { flush(); continue; }
    const num = /^\d+\.\s+/.exec(line), bullet = line.startsWith("- ");
    if (num || bullet) {
      const ordered = !!num;
      if (!list || list.ordered !== ordered) { flush(); list = { ordered, items: [] }; }
      list.items.push(num ? line.slice(num[0].length) : line.slice(2));
      continue;
    }
    flush();
    if (line.startsWith("# ")) out.push(<h3 key={out.length}>{line.slice(2)}</h3>);
    else out.push(<p key={out.length}>{line}</p>);
  }
  flush();
  return <>{out}</>;
}

/** Help drawer: explanation of the current page, a quick start, troubleshooting, keys and words. */
export function HelpPanel({ page, onClose, initialTab = "page" }: { page: string; onClose: () => void; initialTab?: Tab }) {
  const t = useT();
  const [tab, setTab] = useState<Tab>(initialTab);
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [onClose]);
  const body = tab === "page" ? t("help.page." + page) : t("help." + tab + ".text");
  return (
    <div className="help-drawer-wrap" onPointerDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <aside className="help-drawer" role="dialog" aria-label={t("help.title")}>
        <div className="row">
          <h2 className="grow"><Icon name="info" /> {t("help.title")}</h2>
          <button className="btn ghost icon-btn" onClick={onClose}>×</button>
        </div>
        <div className="chips">
          {TABS.map((x) => (
            <span key={x} className={"chip" + (tab === x ? " active" : "")} onClick={() => setTab(x)}>
              {x === "page" ? t("nav." + page) : t("help.tab." + x)}
            </span>
          ))}
        </div>
        <div className="help-text"><Text text={body} /></div>
        {tab === "trouble" && <div className="card"><ReportCard compact /></div>}
        <p className="hint">{t("help.footer")}</p>
      </aside>
    </div>
  );
}
