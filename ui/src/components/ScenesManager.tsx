import { useEffect, useState } from "react";
import { api, type SceneInfo } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { ColorInput, contrast } from "./ColorPicker";
import { Icon } from "./Icon";
import { TemplatesDialog } from "./TemplatesDialog";
import { toast } from "./Toast";
import type { DragStart } from "./LiveTools";

/**
 * The Presets tab: every scene with a big play button (for the chosen lamps only when some are chosen),
 * its name, colour and fade, and overwrite with what is on now, or remove.
 */
export function ScenesManager({ selected = [], onDrag, requireSelection = false }: { selected?: string[]; onDrag?: DragStart; requireSelection?: boolean }) {
  const t = useT();
  const { full } = useShow();
  const scenes = full!.scenes;
  const [templates, setTemplates] = useState(false);
  const create = async () => {
    const name = t("scenes.new", { n: scenes.length + 1 });
    // with some lamps chosen the preset is for those lamps only
    await api.saveScene({ name, ids: selected });
    toast(t("toast.sceneSaved", { name }));
  };
  return (
    <div className="stack">
      <div className="row" style={{ flexWrap: "wrap" }}>
        <p className="hint grow" style={{ margin: 0 }}>{t("scenes.manageHint")}</p>
        <button className="btn" onClick={create}><Icon name="save" /> {t("scenes.newNow")}</button>
        <button className="btn ghost" onClick={() => setTemplates(true)} title={t("tpl.hint")}><Icon name="sparkles" /> {t("tpl.button")}</button>
      </div>
      {scenes.length === 0 && <p className="hint">{t("scenes.empty")}</p>}
      <div className="scene-list">
        {scenes.map((s) => <SceneRow key={s.id} scene={s} active={full!.state.activeScene === s.id} selected={selected} onDrag={onDrag} requireSelection={requireSelection} />)}
      </div>
      {templates && <TemplatesDialog onClose={() => setTemplates(false)} selected={selected} />}
    </div>
  );
}

function SceneRow({ scene, active, selected, onDrag, requireSelection }: { scene: SceneInfo; active: boolean; selected: string[]; onDrag?: DragStart; requireSelection?: boolean }) {
  const t = useT();
  const [name, setName] = useState(scene.name);
  const [fade, setFade] = useState(String(scene.fadeMs / 1000));
  useEffect(() => setName(scene.name), [scene.name]);
  useEffect(() => setFade(String(scene.fadeMs / 1000)), [scene.fadeMs]);
  return (
    <div className={"scene-row" + (active ? " active" : "")}>
      {/* play: the scene now (for the chosen lamps only when some are chosen), in the scene's own colour */}
      <button className="scene-play" style={{ background: scene.color, color: contrast(scene.color) }}
        onClick={() => (requireSelection && !selected.length ? toast(t("live.pickFirst"), false) : api.recallScene(scene.id, selected))}
        onPointerDown={onDrag ? (e) => onDrag({ type: "scene", scene }, e) : undefined}
        title={selected.length ? t("scenes.forSelection", { n: selected.length }) : t("scenes.recall")} aria-label={t("scenes.recall") + ": " + scene.name}>
        <Icon name="play" size={22} />
      </button>
      <label className="colorbtn scene-color" style={{ background: scene.color }} title={t("scenes.color")}>
        <ColorInput value={scene.color} onPick={(hex) => api.saveScene({ id: scene.id, color: hex })} />
      </label>
      <input className="input grow" value={name} aria-label={t("scenes.name")} onChange={(e) => setName(e.target.value)}
        onBlur={() => name.trim() && name !== scene.name && api.saveScene({ id: scene.id, name: name.trim() })}
        onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
      {scene.lamps && <span className="muted scene-lamps" title={t("scenes.lampsHint")}>{t("scenes.lamps", { n: scene.lamps.length })}</span>}
      <label className="scene-fade" title={t("scenes.fade")}>
        <input className="input num" type="number" min={0} max={60} step={0.1} value={fade} aria-label={t("scenes.fade")}
          onChange={(e) => setFade(e.target.value)}
          onBlur={() => { const v = parseFloat(fade.replace(",", ".")); if (v >= 0 && Math.round(v * 1000) !== scene.fadeMs) api.saveScene({ id: scene.id, fadeMs: Math.round(v * 1000) }); }} />
        <span className="muted">s</span>
      </label>
      <button className="btn sm" onClick={() => { api.saveScene({ id: scene.id, capture: true, ids: selected.length ? selected : undefined }); toast(t("toast.sceneOverwritten", { name: scene.name })); }}
        title={t("scenes.overwriteHint")}><Icon name="save" /> {t("scenes.overwrite")}</button>
      <button className="btn sm ghost danger icon-btn" title={t("scenes.delete")} aria-label={t("scenes.delete")}
        onClick={() => { api.removeScene(scene.id); toast(t("toast.sceneRemoved", { name: scene.name })); }}><Icon name="trash" /></button>
    </div>
  );
}
