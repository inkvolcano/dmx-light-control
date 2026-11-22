import { useEffect, useState } from "react";
import { api, type SceneTemplateInfo } from "../api";
import { useLang, useT } from "../i18n";
import { useShow } from "../store";
import { contrast } from "./ColorPicker";
import { Icon } from "./Icon";

const order = ["basic", "mood", "party", "theme"];

/** Pick ready-made scenes; they are built for the fixtures in the patch. */
export function TemplatesDialog({ onClose, selected = [] }: { onClose: () => void; selected?: string[] }) {
  const t = useT();
  const lang = useLang();
  const { full } = useShow();
  const [items, setItems] = useState<SceneTemplateInfo[]>([]);
  const [picked, setPicked] = useState<Set<string>>(new Set());
  const [msg, setMsg] = useState<string | null>(null);
  useEffect(() => { api.sceneTemplates().then(setItems).catch(() => {}); }, []);

  // lamps chosen on the stage (not all): the examples are spread over those, and named so
  const some = selected.length > 0 && selected.length < full!.patch.length ? selected : [];
  // nothing chosen = nothing is added (choose lamps first, or "Alles kiezen" for all of them)
  const none = selected.length === 0;
  const name = (x: SceneTemplateInfo) => (lang === "en" ? x.nameEn : x.name) + (some.length ? " " + t("tpl.forN", { n: some.length }) : "");
  const existing = new Set(full!.scenes.map((s) => s.name.toLowerCase()));
  const toggle = (k: string) => setPicked((p) => { const n = new Set(p); if (n.has(k)) n.delete(k); else n.add(k); return n; });
  const pickAll = (keys: string[]) => setPicked((p) => { const n = new Set(p); const all = keys.every((k) => n.has(k)); keys.forEach((k) => (all ? n.delete(k) : n.add(k))); return n; });

  const add = async () => {
    const keys = [...picked];
    const names = Object.fromEntries(items.filter((x) => picked.has(x.key)).map((x) => [x.key, name(x)]));
    const r = await api.addSceneTemplates(keys, names, some);
    setMsg(t("tpl.added", { n: r.added }) + (r.skipped ? " " + t("tpl.skipped", { n: r.skipped }) : ""));
    setPicked(new Set());
  };

  return (
    <div className="offline" onClick={onClose}>
      <div className="box editor" onClick={(e) => e.stopPropagation()}>
        <div className="row">
          <h2 style={{ flex: 1 }}><Icon name="sparkles" /> {t("tpl.title")}</h2>
          <button className="btn ghost icon-btn" onClick={onClose}>×</button>
        </div>
        <p className="hint">{t("tpl.hint")}</p>
        <div className={"note" + (none ? " warn" : "")}><Icon name={none ? "warn" : "info"} />{none ? t("tpl.pickFirst") : some.length ? t("tpl.forSelection", { n: some.length }) : t("tpl.forAll", { n: full!.patch.length })}</div>
        {full!.patch.length === 0 && <div className="note warn"><Icon name="warn" />{t("tpl.noFixtures")}</div>}
        {order.map((cat) => {
          const list = items.filter((x) => x.category === cat);
          if (!list.length) return null;
          return (
            <div key={cat} className="stack" style={{ gap: 8 }}>
              <div className="row">
                <h3 style={{ flex: 1 }}>{t("tpl.cat." + cat)}</h3>
                <button className="btn sm ghost" onClick={() => pickAll(list.map((x) => x.key))}>{t("tpl.pickAll")}</button>
              </div>
              <div className="tpl-grid">
                {list.map((x) => {
                  const have = existing.has(name(x).toLowerCase());
                  return (
                    <button key={x.key} className={"tpl" + (picked.has(x.key) ? " picked" : "") + (have ? " have" : "")}
                      onClick={() => toggle(x.key)} title={have ? t("tpl.have") : ""}>
                      <span className="sw" style={{ background: x.color, color: contrast(x.color) }}>{picked.has(x.key) ? "✓" : have ? "•" : ""}</span>
                      <span>{name(x)}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          );
        })}
        {msg && <div className="note"><Icon name="info" />{msg}</div>}
        <div className="row" style={{ justifyContent: "flex-end" }}>
          <button className="btn ghost" onClick={onClose}>{t("scenes.done")}</button>
          <button className="btn primary" disabled={!picked.size || full!.patch.length === 0 || none} onClick={add}>
            <Icon name="plus" /> {t("tpl.add", { n: picked.size })}
          </button>
        </div>
      </div>
    </div>
  );
}
