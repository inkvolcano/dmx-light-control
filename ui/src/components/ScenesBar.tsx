import { DndContext, PointerSensor, closestCenter, useSensor, useSensors, type DragEndEvent } from "@dnd-kit/core";
import { SortableContext, arrayMove, horizontalListSortingStrategy, useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useState } from "react";
import { api, type SceneInfo } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { ColorInput } from "./ColorPicker";
import { Icon } from "./Icon";
import { Popover } from "./Popover";
import { TemplatesDialog } from "./TemplatesDialog";

/** Scene buttons: click = recall (with its fade), drag = reorder, ⋯ = edit. */
export function ScenesBar() {
  const t = useT();
  const { full } = useShow();
  const [editing, setEditing] = useState<string | null>(null);
  const [templates, setTemplates] = useState(false);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const scenes = full!.scenes;

  const create = async () => {
    const r = await api.saveScene({ name: t("scenes.new", { n: scenes.length + 1 }) });
    setEditing(r.id);
  };
  const onDragEnd = (e: DragEndEvent) => {
    if (!e.over || e.active.id === e.over.id) return;
    const ids = scenes.map((s) => s.id);
    api.orderScenes(arrayMove(ids, ids.indexOf(String(e.active.id)), ids.indexOf(String(e.over.id))));
  };

  return (
    <div className="bar">
      <h3>{t("scenes.title")}</h3>
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={scenes.map((s) => s.id)} strategy={horizontalListSortingStrategy}>
          {scenes.map((s) => (
            <SceneButton key={s.id} scene={s} active={full!.state.activeScene === s.id}
              editing={editing === s.id} setEditing={(on) => setEditing(on ? s.id : null)} />
          ))}
        </SortableContext>
      </DndContext>
      <button className="btn sm" onClick={create} title={t("scenes.saveHint")}><Icon name="save" /> {t("scenes.save")}</button>
      <button className="btn sm ghost" onClick={() => setTemplates(true)} title={t("tpl.hint")}><Icon name="sparkles" /> {t("tpl.button")}</button>
      {scenes.length === 0 && <span className="hint">{t("scenes.empty")}</span>}
      {templates && <TemplatesDialog onClose={() => setTemplates(false)} />}
    </div>
  );
}

function SceneButton({ scene, active, editing, setEditing }:
  { scene: SceneInfo; active: boolean; editing: boolean; setEditing: (on: boolean) => void }) {
  const t = useT();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: scene.id });
  const [name, setName] = useState(scene.name);
  const [fade, setFade] = useState(String(scene.fadeMs / 1000));

  return (
    <div ref={setNodeRef} className={"scene" + (active ? " active" : "") + (isDragging ? " dragging" : "")}
      style={{ transform: CSS.Translate.toString(transform), transition, ["--scene" as string]: scene.color }}>
      <button className="go" onClick={() => api.recallScene(scene.id)} {...listeners} {...attributes}>
        <b>{scene.name}</b>
        <small>{scene.fadeMs ? (scene.fadeMs / 1000).toFixed(1) + " s" : t("live.fadeNone")}</small>
      </button>
      <button className="more" onClick={() => { setName(scene.name); setFade(String(scene.fadeMs / 1000)); setEditing(!editing); }}>⋯</button>
      {editing && (
        <Popover onClose={() => setEditing(false)}>
          <label className="field">{t("scenes.name")}
            <input className="input" value={name} autoFocus onChange={(e) => setName(e.target.value)}
              onBlur={() => name.trim() && name !== scene.name && api.saveScene({ id: scene.id, name })}
              onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
          </label>
          <div className="row">
            <label className="field" style={{ flex: 1 }}>{t("scenes.color")}
              <span className="colorbtn" style={{ background: scene.color }}>
                <ColorInput value={scene.color} onPick={(hex) => api.saveScene({ id: scene.id, color: hex })} />
              </span>
            </label>
            <label className="field" style={{ width: 90 }}>{t("scenes.fade")}
              <input className="input num" style={{ width: "100%" }} type="number" min={0} max={60} step={0.1} value={fade}
                onChange={(e) => setFade(e.target.value)}
                onBlur={() => { const v = parseFloat(fade.replace(",", ".")); if (v >= 0) api.saveScene({ id: scene.id, fadeMs: Math.round(v * 1000) }); }} />
            </label>
          </div>
          <button className="btn sm" onClick={() => api.saveScene({ id: scene.id, capture: true })}><Icon name="save" /> {t("scenes.overwrite")}</button>
          <div className="row">
            <button className="btn sm ghost danger" onClick={() => { api.removeScene(scene.id); setEditing(false); }}><Icon name="trash" /> {t("scenes.delete")}</button>
            <div style={{ flex: 1 }} />
            <button className="btn sm primary" onClick={() => setEditing(false)}>{t("scenes.done")}</button>
          </div>
        </Popover>
      )}
    </div>
  );
}
