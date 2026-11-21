import { useState } from "react";
import { api, type FixtureGroup } from "../api";
import { useT } from "../i18n";
import { useShow } from "../store";
import { Icon } from "./Icon";
import { Popover } from "./Popover";

/** Group chips: click = select the group's fixtures (Ctrl/Shift = add), ⋯ = edit. */
export function GroupsBar({ selected, setSelected }: { selected: string[]; setSelected: (ids: string[]) => void }) {
  const t = useT();
  const { full } = useShow();
  const [editing, setEditing] = useState<string | null>(null);
  const groups = full!.groups;
  const same = (g: FixtureGroup) => g.fixtureIds.length > 0 && g.fixtureIds.length === selected.length && g.fixtureIds.every((id) => selected.includes(id));

  const create = async () => {
    const r = await api.saveGroup({ name: t("groups.new", { n: groups.length + 1 }), fixtureIds: selected });
    setEditing(r.id);
  };

  return (
    <div className="bar">
      <h3>{t("groups.title")}</h3>
      {groups.map((g) => (
        <span key={g.id} className={"group-chip" + (same(g) ? " selected" : "")}>
          <button className="go" onClick={(e) => setSelected(e.ctrlKey || e.shiftKey ? [...new Set([...selected, ...g.fixtureIds])] : g.fixtureIds)}>
            {g.name}<small>{g.fixtureIds.length}</small>
          </button>
          <button className="more" onClick={() => setEditing(editing === g.id ? null : g.id)}>⋯</button>
          {editing === g.id && <GroupEditor group={g} selected={selected} close={() => setEditing(null)} />}
        </span>
      ))}
      <button className="btn sm" onClick={create} disabled={!selected.length}><Icon name="users" /> {t("groups.fromSelection")}</button>
      {groups.length === 0 && !selected.length && <span className="hint">{t("groups.empty")}</span>}
    </div>
  );
}

function GroupEditor({ group, selected, close }: { group: FixtureGroup; selected: string[]; close: () => void }) {
  const t = useT();
  const [name, setName] = useState(group.name);
  return (
    <Popover onClose={close}>
      <label className="field">{t("scenes.name")}
        <input className="input" value={name} autoFocus onChange={(e) => setName(e.target.value)}
          onBlur={() => name.trim() && name !== group.name && api.saveGroup({ id: group.id, name })}
          onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
      </label>
      <span className="hint">{t("groups.count", { n: group.fixtureIds.length })}</span>
      <button className="btn sm" disabled={!selected.length} onClick={() => api.saveGroup({ id: group.id, fixtureIds: selected })}>{t("groups.update")}</button>
      <div className="row">
        <button className="btn sm ghost danger" onClick={() => { api.removeGroup(group.id); close(); }}><Icon name="trash" /> {t("groups.delete")}</button>
        <div style={{ flex: 1 }} />
        <button className="btn sm primary" onClick={close}>{t("scenes.done")}</button>
      </div>
    </Popover>
  );
}
