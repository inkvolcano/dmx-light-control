import { api, type SceneInfo } from "../api";
import { useT } from "../i18n";
import { currentSettings, updateSettings, useShow } from "../store";
import { SceneSlotMenu } from "./SceneSlotMenu";
import { toast } from "../components/Toast";

/** The four preset buttons: per button a scene id ("" = empty). Not chosen yet = the first four scenes. */
export function presetSlots(scenes: SceneInfo[], saved: string[] | null | undefined): string[] {
  return [0, 1, 2, 3].map((i) => (saved ? saved[i] ?? "" : scenes[i]?.id ?? ""));
}

/**
 * Four preset buttons: click = recall the scene (with its fade; for the chosen lamps only when some are chosen),
 * the arrow = choose which scene sits on the button. Editing scenes is on Effecten en presets > Presets.
 * The same four are in the right column on the other pages.
 */
export function ScenesBar({ selected = [] }: { selected?: string[] }) {
  const t = useT();
  const { full } = useShow();
  const scenes = full!.scenes;
  const slots = presetSlots(scenes, full!.settings.railScenes);
  const setSlot = (i: number, id: string) => {
    const next = presetSlots(scenes, currentSettings().railScenes);
    next[i] = id;
    updateSettings({ railScenes: next });
  };
  const others = scenes.filter((s) => !slots.includes(s.id)).length;

  // "new" under an arrow: what is on now becomes a new scene on that button
  const create = async (slot: number) => {
    const name = t("scenes.new", { n: scenes.length + 1 });
    const r = await api.saveScene({ name });
    setSlot(slot, r.id);
    toast(t("toast.sceneSaved", { name }));
  };

  return (
    <div className="bar">
      <h3>{t("scenes.presets")}</h3>
      {slots.map((id, i) => {
        const s = scenes.find((x) => x.id === id);
        return (
          <div key={i} className={"scene" + (s ? "" : " empty") + (s && full!.state.activeScene === s.id ? " active" : "")}
            style={{ ["--scene" as string]: s?.color ?? "#444" }}>
            {s ? (
              <button className="go" onClick={() => api.recallScene(s.id, selected)}
                title={selected.length ? t("scenes.forSelection", { n: selected.length }) : undefined}>
                <b>{s.name}</b>
                <small>{s.fadeMs ? (s.fadeMs / 1000).toFixed(1) + " s" : t("live.fadeNone")}</small>
              </button>
            ) : (
              <span className="go"><b>{t("rail.slotEmpty")}</b><small>{t("scenes.pickHint")}</small></span>
            )}
            <SceneSlotMenu scenes={scenes} slot={i} value={s ? id : ""} onChoose={(v) => setSlot(i, v)} onNew={() => create(i)} />
          </div>
        );
      })}
      {others > 0 && <span className="hint">{t("scenes.others", { n: others })}</span>}
      {scenes.length === 0 && <span className="hint">{t("scenes.empty")}</span>}
    </div>
  );
}
