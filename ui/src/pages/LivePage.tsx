import { useEffect, useMemo, useRef, useState, type PointerEvent as RPointerEvent } from "react";
import { api, type ColorRef, type FullState, type PaletteColor, type PatchedFixture } from "../api";
import { ColorInput, contrast } from "../components/ColorPicker";
import { GroupsBar } from "../components/GroupsBar";
import { ScenesBar } from "../components/ScenesBar";
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

/** "Picking a colour also turns the lamp on" - remembered per device. */
function useColorTurnsOn(): [boolean, (v: boolean) => void] {
  const [v, setV] = useState(() => {
    try { return localStorage.getItem("colorTurnsOn") !== "0"; } catch { return true; }
  });
  useEffect(() => { try { localStorage.setItem("colorTurnsOn", v ? "1" : "0"); } catch { /* ignore */ } }, [v]);
  return [v, setV];
}

export function LivePage({ selected, setSelected, goPatch }: Props) {
  const t = useT();
  const { full, boot } = useShow();
  const [colorTurnsOn, setColorTurnsOn] = useColorTurnsOn();
  if (!full || !boot) return null;
  const targets = selected.length > 0 ? selected : [];
  const count = selected.length || full.patch.length;

  if (full.patch.length === 0)
    return (
      <div className="empty">
        <Icon name="bulb" size={40} />
        <div>{t("live.empty")}</div>
        <button className="btn primary" onClick={goPatch}>{t("live.goPatch")}</button>
      </div>
    );

  const looks = (selected.length ? selected : full.patch.map((p) => p.id)).map((id) => full.state.looks[id]);
  const allOn = full.state.mode === "static" && looks.every((l) => l?.on);
  const allOff = full.state.mode === "static" && looks.every((l) => !l?.on);
  const pick = (color: ColorRef) => api.looks(targets, colorTurnsOn ? true : undefined, color);

  return (
    <div className="live">
      <div>
        <div className="page-head">
          <h1>{t("live.title")}</h1>
          <span className="chip">{selected.length ? t("live.selected", { n: selected.length }) : t("live.allLamps")}</span>
          <div className="grow" />
          <button className="btn sm ghost" onClick={() => api.updateFixtures(full.patch.map((p) => ({ id: p.id, x: -1, y: -1 })))}>
            <Icon name="layout" /> {t("live.resetLayout")}
          </button>
          <button className="btn sm" onClick={() => setSelected(full.patch.map((p) => p.id))}>{t("live.selectAll")}</button>
          <button className="btn sm" onClick={() => setSelected([])} disabled={!selected.length}>{t("live.selectNone")}</button>
        </div>
        <ScenesBar />
        <GroupsBar selected={selected} setSelected={setSelected} />
        <Stage full={full} selected={selected} setSelected={setSelected} />
        <p className="hint" style={{ marginTop: 10 }}>{t("live.hint")}</p>
      </div>

      <div className="card palette-panel">
        <div className="onoff">
          <button className={"btn lg on" + (allOn ? " active" : "")} onClick={() => api.looks(targets, true)}>
            <Icon name="power" /> {t("live.on")} <span className="muted">({count})</span>
          </button>
          <button className={"btn lg off" + (allOff ? " active" : "")} onClick={() => api.looks(targets, false)}>
            {t("live.off")} <span className="muted">({count})</span>
          </button>
        </div>
        <label className="check" style={{ marginBottom: 12 }}>
          <input type="checkbox" checked={colorTurnsOn} onChange={(e) => setColorTurnsOn(e.target.checked)} />
          <span className="box" /> {t("live.colorTurnsOn")}
        </label>
        <div className="palette-section">
          <h3>{t("live.palette")}</h3>
          <Swatches colors={boot.palette.fan} cls="fan" onPick={(c) => pick({ key: c.key })} />
        </div>
        <div className="palette-section">
          <h3>{t("live.specials")}</h3>
          <Swatches colors={boot.palette.specials} cls="special" onPick={(c) => pick({ key: c.key })} />
        </div>
        <CustomColors colors={full.state.customColors} onPick={(hex) => pick({ hex })} />
        <FadeControl value={full.state.fadeMs} />
        <div className="palette-section">
          <h3>{t("live.strobeSpeed")}</h3>
          <div className="slider">
            <input type="range" min={1} max={100} value={full.state.strobeSpeed} onChange={(e) => api.strobe({ speed: +e.target.value })} />
            <span className="val">{full.state.strobeSpeed}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function FadeControl({ value }: { value: number }) {
  const t = useT();
  const steps = [0, 300, 1000, 2000, 5000];
  return (
    <div className="palette-section">
      <h3>{t("live.fade")}</h3>
      <div className="seg" style={{ width: "100%" }}>
        {steps.map((ms) => (
          <button key={ms} style={{ flex: 1 }} className={value === ms ? "active" : ""} onClick={() => api.master({ fadeMs: ms })}>
            {ms === 0 ? t("live.fadeNone") : ms / 1000 + " s"}
          </button>
        ))}
      </div>
    </div>
  );
}

function Swatches({ colors, cls, onPick }: { colors: PaletteColor[]; cls: string; onPick: (c: PaletteColor) => void }) {
  const t = useT();
  return (
    <div className={"swatches " + cls}>
      {colors.map((c) => (
        <button key={c.key} className="swatch" style={{ background: c.hex, color: contrast(c.hex) }}
          title={t("color." + c.key, undefined, c.name)} onClick={() => onPick(c)}>
          {c.key === "uv" ? "UV" : ""}
        </button>
      ))}
    </div>
  );
}

function CustomColors({ colors, onPick }: { colors: string[]; onPick: (hex: string) => void }) {
  const t = useT();
  const save = (list: string[]) => api.customColors(list);
  return (
    <div className="palette-section">
      <h3>{t("live.custom")}</h3>
      <div className="swatches custom">
        {colors.map((hex, i) => (
          <button key={i + hex} className="swatch" style={{ background: hex }} title={hex + " - " + t("live.customHint")}
            onClick={() => onPick(hex)}
            onContextMenu={(e) => { e.preventDefault(); save(colors.filter((_, j) => j !== i)); }} />
        ))}
        <label className="swatch add" title={t("live.addCustom")}>
          +
          <ColorInput value="#ffffff" onPick={(hex) => { if (!colors.includes(hex)) save([...colors, hex]); onPick(hex); }} />
        </label>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- stage

function Stage({ full, selected, setSelected }: { full: FullState; selected: string[]; setSelected: (ids: string[]) => void }) {
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
    <div className="stage" ref={ref} onPointerDown={onStageDown} onPointerMove={onMove} onPointerUp={onUp} onPointerCancel={onUp}>
      <div className="stage-label">stage</div>
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
