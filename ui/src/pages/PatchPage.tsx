import {
  DndContext, DragOverlay, PointerSensor, KeyboardSensor, closestCenter, useDraggable, useDroppable, useSensor, useSensors,
  type DragEndEvent, type DragStartEvent,
} from "@dnd-kit/core";
import { SortableContext, arrayMove, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { useEffect, useMemo, useRef, useState } from "react";
import { api, type FixtureMode, type FixtureType, type FullState, type PatchedFixture } from "../api";
import { FixtureEditor } from "../components/FixtureEditor";
import { Help } from "../components/Help";
import { Icon } from "../components/Icon";
import { useT } from "../i18n";
import { reloadBootstrap, useShow } from "../store";

type CardState = Record<string, { mode: string; count: number }>;

export function PatchPage() {
  const t = useT();
  const { full, boot } = useShow();
  const [query, setQuery] = useState("");
  const [dragLib, setDragLib] = useState<FixtureType | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [editing, setEditing] = useState<FixtureType | null>(null);
  const newType = (): FixtureType => ({
    id: "", manufacturer: "", model: t("patch.newType"), category: "par", colors: "RGB", custom: true,
    modes: [{ name: "3ch", footprint: 3, cellCount: 1, channels: [
      { number: 1, kind: "red", cell: -1, default: 0, label: "R" },
      { number: 2, kind: "green", cell: -1, default: 0, label: "G" },
      { number: 3, kind: "blue", cell: -1, default: 0, label: "B" }] }],
  });
  const edit = (type: FixtureType) => setEditing(type.custom ? type : { ...structuredClone(type), id: "", model: type.model + " (2)", custom: true });
  const importOfl = async (file: File) => {
    try { setEditing(await api.importOfl(file)); } catch (e) { setMessage(String(e)); }
  };
  const cards = useRef<CardState>({});
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const lib = boot!.library;
  const patch = full!.patch;
  const shown = lib.filter((x) => !query || `${x.manufacturer} ${x.model} ${x.colors}`.toLowerCase().includes(query.toLowerCase()));

  const add = async (type: FixtureType) => {
    const c = cards.current[type.id] ?? { mode: type.modes[0].name, count: 1 };
    const r = await api.addFixtures(type.id, c.mode, c.count);
    setMessage(r.failed > 0 ? t("patch.full") : null);
  };

  const onDragStart = (e: DragStartEvent) => {
    const id = String(e.active.id);
    if (id.startsWith("lib:")) setDragLib(lib.find((x) => "lib:" + x.id === id) ?? null);
  };

  const onDragEnd = (e: DragEndEvent) => {
    setDragLib(null);
    const a = String(e.active.id);
    const over = e.over ? String(e.over.id) : null;
    if (!over) return;
    if (a.startsWith("lib:")) {
      const type = lib.find((x) => "lib:" + x.id === a);
      if (type) add(type);
      return;
    }
    if (a !== over && patch.some((p) => p.id === over)) {
      const from = patch.findIndex((p) => p.id === a), to = patch.findIndex((p) => p.id === over);
      api.orderFixtures(arrayMove(patch, from, to).map((p) => p.id));
    }
  };

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragStart={onDragStart} onDragEnd={onDragEnd} onDragCancel={() => setDragLib(null)}>
      <div className="page-head"><h1>{t("patch.title")}</h1></div>
      <div className="patch-layout">
        <div className="card">
          <div className="card-head">
            <h2 className="grow">{t("patch.library")}</h2>
            <button className="btn sm" onClick={() => setEditing(newType())}><Icon name="plus" /> {t("patch.newType")}</button>
            <label className="btn sm" title={t("patch.oflHint")}>
              <Icon name="upload" /> {t("patch.importOfl")}
              <input type="file" accept=".json,application/json" style={{ display: "none" }}
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importOfl(f); e.target.value = ""; }} />
            </label>
          </div>
          <input className="input" style={{ width: "100%", marginBottom: 10 }} placeholder={t("patch.search")} value={query} onChange={(e) => setQuery(e.target.value)} />
          <div className="note" style={{ marginBottom: 10 }}><Icon name="info" /><span>{t("patch.noAuto")}</span></div>
          <p className="hint" style={{ marginBottom: 10 }}>{t("patch.dragHint")}</p>
          <div className="lib-list">
            {shown.map((type) => <LibCard key={type.id} type={type} cards={cards.current} onAdd={() => add(type)} onEdit={() => edit(type)} />)}
          </div>
        </div>
        <PatchList full={full!} lib={lib} message={message} />
      </div>
      {editing && <FixtureEditor initial={editing} onClose={() => setEditing(null)} />}
      <DragOverlay dropAnimation={null}>
        {dragLib ? <div className="drag-overlay">+ {dragLib.model}</div> : null}
      </DragOverlay>
    </DndContext>
  );
}

function LibCard({ type, cards, onAdd, onEdit }: { type: FixtureType; cards: CardState; onAdd: () => void; onEdit: () => void }) {
  const t = useT();
  const [mode, setMode] = useState(type.modes[0].name);
  const [count, setCount] = useState(1);
  cards[type.id] = { mode, count };
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: "lib:" + type.id });
  const m = type.modes.find((x) => x.name === mode) ?? type.modes[0];
  return (
    <div className={"lib-card" + (isDragging ? " dragging" : "")} ref={setNodeRef}>
      <div className="title" {...listeners} {...attributes}>
        <Icon name="grip" />
        <div className="grow">
          <div className="model">{type.model}</div>
          <div className="maker">{type.manufacturer}{type.custom ? " · " + t("patch.custom") : ""}</div>
        </div>
        {type.colors && <span className={"tag" + (type.colors.startsWith("RGB") ? " rgb" : "")}>{type.colors}</span>}
        <button className="btn sm ghost icon-btn" title={t("patch.edit")} onPointerDown={(e) => e.stopPropagation()} onClick={onEdit}><Icon name="edit" /></button>
        {type.custom && <button className="btn sm ghost danger icon-btn" title={t("scenes.delete")} onPointerDown={(e) => e.stopPropagation()}
          onClick={async () => { await api.removeFixtureType(type.id); await reloadBootstrap(); }}><Icon name="trash" /></button>}
      </div>
      <div className="controls">
        <select className="input" value={mode} onChange={(e) => setMode(e.target.value)} title={t("patch.mode")}>
          {type.modes.map((x) => <option key={x.name} value={x.name}>{x.name}{x.cellCount > 1 ? ` · ${t("patch.segments", { n: x.cellCount })}` : ""}</option>)}
        </select>
        <input className="input num" type="number" min={1} max={64} value={count} title={t("patch.count")}
          onChange={(e) => setCount(Math.max(1, Math.min(64, +e.target.value || 1)))} style={{ width: "100%" }} />
        <button className="btn primary sm" style={{ height: 34 }} onClick={onAdd}><Icon name="plus" /> {t("patch.add")}</button>
      </div>
      <div className="hint mono">{m.channels.map((c) => c.label ?? c.kind).join(" · ")}</div>
    </div>
  );
}

function PatchList({ full, lib, message }: { full: FullState; lib: FixtureType[]; message: string | null }) {
  const t = useT();
  const patch = full.patch;
  const { setNodeRef, isOver } = useDroppable({ id: "patch-drop" });
  const byId = useMemo(() => Object.fromEntries(patch.map((p) => [p.id, p])), [patch]);
  const conflicts = full.rig.conflicts;
  const identify = new Set(full.identify);

  const update = (id: string, change: Partial<PatchedFixture>) => api.updateFixtures([{ id, ...change }]);

  // 512-channel map of one universe
  const universes = Math.max(1, ...patch.map((p) => p.universe ?? 1));
  const [mapUniverse, setMapUniverse] = useState(1);
  const shownUniverse = Math.min(mapUniverse, universes);
  const map = useMemo(() => {
    const cells: ("" | "used" | "used alt" | "clash")[] = Array(512).fill("");
    patch.forEach((p, i) => {
      if ((p.universe ?? 1) !== shownUniverse) return;
      const fp = modeOf(lib, p)?.footprint ?? 1;
      for (let c = p.address; c < p.address + fp && c <= 512; c++) cells[c - 1] = cells[c - 1] ? "clash" : i % 2 ? "used alt" : "used";
    });
    return cells;
  }, [patch, lib, shownUniverse]);

  return (
    <div className="card">
      <div className="card-head">
        <div className="grow">
          <h2>{t("patch.list")}</h2>
          <div className="hint">{t("patch.listHint")}</div>
        </div>
        <button className="btn sm" onClick={() => api.renumber()} disabled={!patch.length}><Icon name="numbers" /> {t("patch.autoAddress")}</button>
      </div>
      {message && <div className="note warn" style={{ marginBottom: 10 }}><Icon name="warn" />{message}</div>}
      <div ref={setNodeRef} className={"dropzone" + (isOver ? " over" : "")}>
        {patch.length === 0 ? (
          <div className="empty" style={{ padding: 30 }}><Icon name="plug" size={32} />{t("patch.empty")}</div>
        ) : (
          <SortableContext items={patch.map((p) => p.id)} strategy={verticalListSortingStrategy}>
            <table className="patch-table">
              <thead>
                <tr>
                  <th style={{ width: 28 }} />
                  <th>{t("patch.name")}</th>
                  <th>{t("patch.mode")}</th>
                  <th>{t("patch.universeCol")} <Help k="patch.universeHelp" /></th>
                  <th>{t("patch.address")}</th>
                  <th style={{ width: 120 }} />
                </tr>
              </thead>
              <tbody>
                {patch.map((p) => {
                  const mine = conflicts.filter((c) => c.fixtureA === p.id || c.fixtureB === p.id);
                  return (
                    <PatchRow key={p.id} p={p} type={lib.find((x) => x.id === p.typeId)} identify={identify.has(p.id)}
                      problems={[
                        ...(full.rig.missing.includes(p.id) ? [t("patch.missing")] : []),
                        ...mine.map((c) => t("patch.conflict", { name: byId[c.fixtureA === p.id ? c.fixtureB : c.fixtureA]?.name ?? "?", from: c.from, to: c.to })),
                      ]}
                      onChange={(ch) => update(p.id, ch)}
                      onRemove={() => api.removeFixtures([p.id])} />
                  );
                })}
              </tbody>
            </table>
          </SortableContext>
        )}
        {isOver && <div className="hint" style={{ textAlign: "center", marginTop: 8 }}>{t("patch.dropHere")}</div>}
      </div>
      <div className="row" style={{ marginTop: 14 }}>
        <span className="hint grow">{t("patch.universe", { used: map.filter(Boolean).length })}{universes > 1 ? " · " + t("patch.universeN", { n: shownUniverse }) : ""}</span>
        {universes > 1 && (
          <div className="seg">
            {Array.from({ length: universes }, (_, i) => i + 1).map((u) => (
              <button key={u} className={shownUniverse === u ? "active" : ""} onClick={() => setMapUniverse(u)}>U{u}</button>
            ))}
          </div>
        )}
      </div>
      <div className="universe">{map.map((c, i) => <div key={i} className={c} title={String(i + 1)} />)}</div>
    </div>
  );
}

function modeOf(lib: FixtureType[], p: PatchedFixture): FixtureMode | undefined {
  const type = lib.find((x) => x.id === p.typeId);
  return type?.modes.find((m) => m.name === p.mode) ?? type?.modes[0];
}

/** "d001"/"A001" style codes show the address the way the lamp's display does. */
function displayCode(m: FixtureMode | undefined, address: number) {
  const code = m?.displayCode;
  if (!code) return null;
  return /^[A-Za-z]\d{3}$/.test(code) ? code[0] + String(address).padStart(3, "0") : code;
}

function PatchRow({ p, type, identify, problems, onChange, onRemove }: {
  p: PatchedFixture; type?: FixtureType; identify: boolean; problems: string[];
  onChange: (c: Partial<PatchedFixture>) => void; onRemove: () => void;
}) {
  const t = useT();
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: p.id });
  const [name, setName] = useState(p.name);
  const [addr, setAddr] = useState(String(p.address));
  useEffect(() => setName(p.name), [p.name]);
  useEffect(() => setAddr(String(p.address)), [p.address]);
  const mode = type?.modes.find((m) => m.name === p.mode) ?? type?.modes[0];
  const fp = mode?.footprint ?? 1;
  const end = p.address + fp - 1;
  const commitName = () => { if (name.trim() && name !== p.name) onChange({ name: name.trim() }); else setName(p.name); };
  const commitAddr = () => {
    const n = Math.round(+addr);
    if (n >= 1 && n <= 512 && n !== p.address) onChange({ address: n });
    else setAddr(String(p.address));
  };
  const code = displayCode(mode, p.address);

  return (
    <tr ref={setNodeRef} className={"patch-row" + (problems.length ? " conflict" : "") + (isDragging ? " dragging" : "")}
      style={{ transform: CSS.Translate.toString(transform), transition }}>
      <td><span className="handle" {...listeners} {...attributes}><Icon name="grip" /></span></td>
      <td>
        <input className="input" value={name} onChange={(e) => setName(e.target.value)} onBlur={commitName}
          onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} style={{ width: "100%", maxWidth: 260 }} />
        <div className="sub">{type ? `${type.manufacturer} ${type.model}` : p.typeId}</div>
        {problems.map((x, i) => <div key={i} className="problem">⚠ {x}</div>)}
      </td>
      <td>
        <select className="input" value={p.mode} onChange={(e) => onChange({ mode: e.target.value })} disabled={!type}>
          {type?.modes.map((m) => <option key={m.name} value={m.name}>{m.name}</option>)}
        </select>
        {mode && mode.cellCount > 1 && <div className="sub">{t("patch.segments", { n: mode.cellCount })}</div>}
      </td>
      <td>
        <select className="input" value={p.universe ?? 1} onChange={(e) => onChange({ universe: +e.target.value })} title={t("patch.universeCol")}>
          {Array.from({ length: 8 }, (_, i) => i + 1).map((u) => <option key={u} value={u}>{u}</option>)}
        </select>
      </td>
      <td>
        <div className="row" style={{ gap: 6, flexWrap: "nowrap" }}>
          <input className={"input num" + (problems.length ? " invalid" : "")} type="number" min={1} max={512} value={addr}
            onChange={(e) => setAddr(e.target.value)} onBlur={commitAddr} onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
          <span className="mono muted" style={{ whiteSpace: "nowrap" }}>–{Math.min(end, 512)}</span>
        </div>
        {code && <div className="sub" title={t("patch.setDisplay", { addr: p.address, code })}>{code}</div>}
      </td>
      <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
        <button className={"btn sm toggle" + (identify ? " active" : "")} title={t("patch.identifyHint")} onClick={() => api.identify(p.id, !identify)}>
          <Icon name="eye" /> {t("patch.identify")}
        </button>{" "}
        <button className="btn sm ghost danger icon-btn" title={t("patch.remove")} onClick={onRemove}><Icon name="trash" /></button>
      </td>
    </tr>
  );
}
