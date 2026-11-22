import { useEffect, useMemo, useRef, useState, type PointerEvent as RPointerEvent, type ReactNode } from "react";
import { api, type FullState, type PatchedFixture } from "../api";
import { FirstRun } from "../components/FirstRun";
import { GroupsBar } from "../components/GroupsBar";
import { LiveTools } from "../components/LiveTools";
import { TimelineEditor, startTimelineDrag } from "./TimelinePage";
import { Icon } from "../components/Icon";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

interface Props {
  selected: string[];
  setSelected: (ids: string[]) => void;
  goPatch: () => void;
}

type Pos = { x: number; y: number };

/** Positions from the patch, or an automatic grid for fixtures that were never dragged. */
export function stagePositions(patch: PatchedFixture[]): Record<string, Pos> {
  const auto = patch.filter((p) => p.x < 0 || p.y < 0);
  const cols = Math.min(Math.max(auto.length, 1), 8);
  const rows = Math.ceil(auto.length / cols);
  const out: Record<string, Pos> = {};
  let k = 0;
  for (const p of patch) {
    if (p.x >= 0 && p.y >= 0) out[p.id] = { x: p.x, y: p.y };
    else {
      const col = k % cols, row = Math.floor(k / cols);
      out[p.id] = { x: 0.08 + ((col + 0.5) / cols) * 0.84, y: rows <= 1 ? 0.42 : 0.2 + (row / Math.max(rows - 1, 1)) * 0.55 };
      k++;
    }
  }
  return out;
}

export function LivePage({ selected, setSelected, goPatch }: Props) {
  const t = useT();
  const { full, boot } = useShow();
  // the timeline, folded open under the stage with a tick box (per device; "go to the timeline" opens it)
  const [showTl, setShowTlState] = useState(() => { try { return localStorage.getItem("liveTimeline") === "1"; } catch { return false; } });
  const setShowTl = (v: boolean) => { setShowTlState(v); try { localStorage.setItem("liveTimeline", v ? "1" : "0"); } catch { /* ignore */ } };
  useEffect(() => {
    const on = () => setShowTlState(true);
    window.addEventListener("dmx-livetimeline", on);
    return () => window.removeEventListener("dmx-livetimeline", on);
  }, []);
  // no lamps yet: the three-step start, which stays until it is finished (it adds lamps on the way)
  const [wizard, setWizard] = useState(() => !!full && full.patch.length === 0);
  useEffect(() => { if (full && full.patch.length === 0) setWizard(true); }, [full?.patch.length]); // eslint-disable-line react-hooks/exhaustive-deps
  if (!full || !boot) return null;

  if (wizard || full.patch.length === 0)
    return <FirstRun goPatch={goPatch} onDone={() => setWizard(false)} />;


  return (
    <div className="live">
      <div>
        <GroupsBar selected={selected} setSelected={setSelected} />
        {/* the stage's own buttons sit in the stage, top right; what is chosen top left; the hint at the bottom */}
        <Stage full={full} selected={selected} setSelected={setSelected}
          info={<span className={"chip" + (selected.length ? " ok" : "")}>{selected.length ? t("live.selected", { n: selected.length }) : t("live.allLamps")}</span>}
          tools={<>
            <button className="btn sm ghost" title={t("live.resetLayout")} onClick={() => api.updateFixtures(full.patch.map((p) => ({ id: p.id, x: -1, y: -1 })))}>
              <Icon name="layout" />
            </button>
            <button className="btn sm" onClick={() => setSelected(full.patch.map((p) => p.id))}>{t("live.selectAll")}</button>
            <button className="btn sm" onClick={() => setSelected([])} disabled={!selected.length}>{t("live.selectNone")}</button>
          </>}
          hint={t("live.hint")} />
      </div>

      {/* under the stage: colours, effects, presets (for the chosen lamps, or all) */}
      {/* between the stage and the tools: fold the timeline open or shut */}
      {/* one box: the switch is its head, the timeline (when open) its body */}
      <div className={"tl-panel" + (showTl ? " open" : "")}>
        <label className="check tl-toggle" title={t("live.timelineHint")}>
          <input type="checkbox" checked={showTl} onChange={(e) => setShowTl(e.target.checked)} />
          <span className="box" /> <Icon name="timeline" size={15} /> {t("nav.timeline")}
        </label>
        {showTl && <div className="live-timeline"><TimelineEditor embedded /></div>}
      </div>
      <LiveTools selected={selected} onDrag={showTl ? startTimelineDrag : undefined} />
    </div>
  );
}

// ---------------------------------------------------------------- stage

function Stage({ full, selected, setSelected, info, tools, hint }: {
  full: FullState; selected: string[]; setSelected: (ids: string[]) => void; info?: ReactNode; tools?: ReactNode; hint?: string;
}) {
  const t = useT();
  const { heads } = useLive();
  const ref = useRef<HTMLDivElement>(null);
  const base = useMemo(() => stagePositions(full.patch), [full.patch]);
  const [drag, setDrag] = useState<{ ids: string[]; dx: number; dy: number } | null>(null);
  const [lasso, setLasso] = useState<{ x0: number; y0: number; x1: number; y1: number } | null>(null);
  const gesture = useRef<{ kind: "move" | "lasso"; id?: string; sx: number; sy: number; moved: boolean; additive: boolean } | null>(null);
  const rig = useMemo(() => Object.fromEntries(full.rig.fixtures.map((f) => [f.id, f])), [full.rig.fixtures]);
  const types = useShow().boot?.library;
  const identify = new Set(full.identify);

  const rel = (e: RPointerEvent) => {
    const r = ref.current!.getBoundingClientRect();
    return { x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height };
  };

  const onFixtureDown = (e: RPointerEvent, id: string) => {
    e.stopPropagation();
    (e.target as Element).setPointerCapture(e.pointerId);
    const p = rel(e);
    gesture.current = { kind: "move", id, sx: p.x, sy: p.y, moved: false, additive: e.ctrlKey || e.shiftKey || e.metaKey };
  };

  const onStageDown = (e: RPointerEvent) => {
    (e.target as Element).setPointerCapture(e.pointerId);
    const p = rel(e);
    gesture.current = { kind: "lasso", sx: p.x, sy: p.y, moved: false, additive: e.ctrlKey || e.shiftKey || e.metaKey };
  };

  const onMove = (e: RPointerEvent) => {
    const g = gesture.current;
    if (!g) return;
    const p = rel(e);
    const r = ref.current!.getBoundingClientRect();
    if (!g.moved && Math.hypot((p.x - g.sx) * r.width, (p.y - g.sy) * r.height) < 5) return;
    g.moved = true;
    if (g.kind === "move") {
      const ids = selected.includes(g.id!) ? selected : [g.id!];
      setDrag({ ids, dx: p.x - g.sx, dy: p.y - g.sy });
    } else setLasso({ x0: g.sx, y0: g.sy, x1: p.x, y1: p.y });
  };

  const onUp = () => {
    const g = gesture.current;
    gesture.current = null;
    if (!g) return;
    if (g.kind === "move") {
      if (g.moved && drag) {
        const moved = new Set(drag.ids);
        const clamp = (v: number) => Math.min(0.97, Math.max(0.03, v));
        // move the dragged fixtures; pin auto-placed ones where they are so they don't jump
        api.updateFixtures(full.patch.flatMap((p) => {
          const b = base[p.id];
          if (moved.has(p.id)) return [{ id: p.id, x: clamp(b.x + drag.dx), y: clamp(b.y + drag.dy) }];
          return p.x < 0 ? [{ id: p.id, x: b.x, y: b.y }] : [];
        }));
        setDrag(null);
      } else {
        const id = g.id!;
        if (g.additive) setSelected(selected.includes(id) ? selected.filter((s) => s !== id) : [...selected, id]);
        else setSelected(selected.length === 1 && selected[0] === id ? [] : [id]);
      }
    } else {
      if (g.moved && lasso) {
        const [x0, x1] = [Math.min(lasso.x0, lasso.x1), Math.max(lasso.x0, lasso.x1)];
        const [y0, y1] = [Math.min(lasso.y0, lasso.y1), Math.max(lasso.y0, lasso.y1)];
        const hit = full.patch.filter((p) => { const b = base[p.id]; return b.x >= x0 && b.x <= x1 && b.y >= y0 && b.y <= y1; }).map((p) => p.id);
        setSelected(g.additive ? [...new Set([...selected, ...hit])] : hit);
      } else if (!g.additive) setSelected([]);
      setLasso(null);
    }
  };

  return (
    <div className={"stage" + (full.patch.length <= 8 ? " few" : "")} ref={ref} onPointerDown={onStageDown} onPointerMove={onMove} onPointerUp={onUp} onPointerCancel={onUp}>
      <div className="stage-label">{t("live.stageLabel")}</div>
      {/* clicks on these never start a lasso or deselect */}
      {info && <div className="stage-info" onPointerDown={(e) => e.stopPropagation()}>{info}</div>}
      {tools && <div className="stage-tools" onPointerDown={(e) => e.stopPropagation()}>{tools}</div>}
      {hint && <div className="stage-hint">{hint}</div>}
      {full.patch.map((p) => {
        const info = rig[p.id];
        const b = base[p.id];
        const moving = drag?.ids.includes(p.id);
        const pos = moving ? { x: b.x + drag!.dx, y: b.y + drag!.dy } : b;
        const cells = info?.cells ?? 1;
        const type = types?.find((x) => x.id === p.typeId);
        const cls = ["fixture",
          selected.includes(p.id) && "selected", moving && "dragging",
          identify.has(p.id) && "identify", (cells > 1 || type?.category === "bar") && "bar",
          !full.state.looks[p.id]?.on && full.state.mode === "static" && "off-state"].filter(Boolean).join(" ");
        return (
          <div key={p.id} className={cls} style={{ left: pos.x * 100 + "%", top: pos.y * 100 + "%" }}
            onPointerDown={(e) => onFixtureDown(e, p.id)} title={`${p.name} · ${type?.model ?? p.typeId} · ${p.mode}`}>
            <div className="body">
              {Array.from({ length: cells }, (_, c) => {
                const col = info ? heads[info.firstHead + c] ?? "#000000" : "#000000";
                return <div key={c} className="cell" style={{ background: col, boxShadow: glow(col) }} />;
              })}
            </div>
            <div className="label">{p.name}</div>
            <div className="addr mono">{p.address}</div>
          </div>
        );
      })}
      {lasso && (
        <div className="lasso" style={{
          left: Math.min(lasso.x0, lasso.x1) * 100 + "%", top: Math.min(lasso.y0, lasso.y1) * 100 + "%",
          width: Math.abs(lasso.x1 - lasso.x0) * 100 + "%", height: Math.abs(lasso.y1 - lasso.y0) * 100 + "%",
        }} />
      )}
    </div>
  );
}

function glow(hex: string) {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
  if (r + g + b < 30) return "none";
  return `0 0 16px 2px rgba(${r},${g},${b},0.55)`;
}
