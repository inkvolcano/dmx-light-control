import { useEffect, useState } from "react";
import { api, type SceneInfo, type SceneTemplateInfo } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { Icon } from "./Icon";
import { Popover } from "./Popover";

let templatesOnce: Promise<SceneTemplateInfo[]> | null = null;
const templates = () => (templatesOnce ??= api.sceneTemplates().catch(() => { templatesOnce = null; return []; }));
const cats = ["basic", "mood", "party", "theme"];

/**
 * The arrow on a preset button: a menu with your own scenes, "save what is on now as a new scene", the
 * ready-made examples (added as a scene when picked) and "empty". Whatever you pick goes on this button.
 */
export function SceneSlotMenu({ scenes, value, slot, onChoose, onNew, alignRight }: {
  scenes: SceneInfo[]; value: string; slot: number;
  onChoose: (sceneId: string) => void; onNew: () => void; alignRight?: boolean;
}) {
  const t = useT();
  const { full } = useShow();
  const [open, setOpen] = useState(false);
  const [list, setList] = useState<SceneTemplateInfo[]>([]);
  const [busy, setBusy] = useState(false);
  const en = full?.settings.language === "en";
  useEffect(() => { if (open) templates().then(setList); }, [open]);
  const choose = (id: string) => { setOpen(false); onChoose(id); };
  const example = async (x: SceneTemplateInfo) => {
    setBusy(true);
    try {
      const name = en ? x.nameEn : x.name;
      const r = await api.addSceneTemplates([x.key], { [x.key]: name });
      const id = r.sceneIds?.[0];
      if (id) choose(id);
    } finally { setBusy(false); }
  };
  const have = new Set(scenes.map((s) => s.name.toLowerCase()));

  return (
    <span className="slot-menu-wrap">
      <button className={"pick" + (open ? " open" : "")} onClick={() => setOpen(!open)} aria-haspopup="menu" aria-expanded={open}
        title={t("rail.slotPick", { n: slot + 1 })} aria-label={t("rail.slotPick", { n: slot + 1 })} />
      {open && (
        <Popover onClose={() => setOpen(false)}>
          <div className={"slot-menu" + (alignRight ? " right" : "")} role="menu">
            <button className="slot-item new" role="menuitem" onClick={() => { setOpen(false); onNew(); }}>
              <Icon name="save" size={15} /> {t("scenes.newHere")}
            </button>
            {scenes.length > 0 && <div className="slot-head">{t("scenes.yours")}</div>}
            {scenes.map((s) => (
              <button key={s.id} className={"slot-item" + (s.id === value ? " on" : "")} role="menuitem" onClick={() => choose(s.id)}>
                <span className="slot-dot" style={{ background: s.color }} /> <span className="grow">{s.name}</span>
                {s.id === value && <span className="slot-check">✓</span>}
              </button>
            ))}
            {cats.map((c) => {
              const items = list.filter((x) => x.category === c);
              if (!items.length) return null;
              return (
                <div key={c}>
                  <div className="slot-head">{t("tpl.button")}: {t("tpl.cat." + c)}</div>
                  {items.map((x) => {
                    const name = en ? x.nameEn : x.name;
                    return (
                      <button key={x.key} className="slot-item example" role="menuitem" disabled={busy} onClick={() => example(x)}
                        title={have.has(name.toLowerCase()) ? t("tpl.have") : t("scenes.exampleHint")}>
                        <span className="slot-dot" style={{ background: x.color }} /> <span className="grow">{name}</span>
                        <Icon name="plus" size={13} />
                      </button>
                    );
                  })}
                </div>
              );
            })}
            {value && (
              <button className="slot-item empty" role="menuitem" onClick={() => choose("")}>{t("scenes.emptyButton")}</button>
            )}
          </div>
        </Popover>
      )}
    </span>
  );
}
