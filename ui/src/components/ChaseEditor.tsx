import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api } from "../api";
import {
  type ChaseDirection, type ChaseMapping, type ChaseStep, type CustomChase, type TemplateKey,
  emptyStep, newId, order, render, TEMPLATES, template, tools, withColumns,
} from "../chase";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";
import { ColorInput, contrast } from "./ColorPicker";
import { Help } from "./Help";
import { Icon } from "./Icon";
import { NumberField } from "./NumberField";

const DIRECTIONS: ChaseDirection[] = ["forward", "reverse", "bounce", "random"];
const MAPPINGS: ChaseMapping[] = ["repeat", "stretch", "mirror", "once"];
const BEATS = [0.25, 0.5, 1, 2, 4, 8];

/**
 * The chase editor: rows are steps, columns are lamps (the pattern repeats over more lamps).
 * Paint with the brush colour; right-click or the "off" brush clears. The preview runs here,
 * without touching the lamps, until you press "Op de lampen".
 */
export function ChaseEditor({ initial, target: initialTarget, onClose }: { initial: CustomChase; target: string; onClose: () => void }) {
  const t = useT();
  const { full, boot } = useShow();
  const live = useLive();
  const [c, setC] = useState<CustomChase>(initial);
  const [history, setHistory] = useState<CustomChase[]>([]);
  const [future, setFuture] = useState<CustomChase[]>([]);
  const [saved, setSaved] = useState(JSON.stringify(initial));
  const [brush, setBrush] = useState<string | null>(firstColor(initial) ?? "#FF0000");
  const [brush2, setBrush2] = useState("#0000FF");
  const [target, setTarget] = useState(initialTarget);
  const [showHelp, setShowHelp] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const dirty = JSON.stringify(c) !== saved;

  const groups = full!.groups;
  const exists = full!.state.chases.some((x) => x.id === c.id);
  const running = full!.state.mode === "effect" && full!.state.effect.chaseId === c.id
    || full!.state.layers.some((l) => l.effect.chaseId === c.id);
  // lamps as the engine counts them: segments of a bar are separate heads
  const cellsOf = (id: string) => full!.rig.fixtures.find((f) => f.id === id)?.cells ?? 1;
  const lampCount = Math.max(1, target
    ? (groups.find((g) => g.id === target)?.fixtureIds ?? []).reduce((n, id) => n + cellsOf(id), 0) || 1
    : full!.rig.heads || c.columns);
  const previewLamps = Math.max(lampCount, c.columns);

  const cRef = useRef(c); cRef.current = c;
  const update = useCallback((next: CustomChase) => {
    setHistory((h) => [...h.slice(-99), cRef.current]);
    setFuture([]);
    setC(next);
  }, []);
  const undo = () => { if (!history.length) return; setFuture((f) => [c, ...f]); setC(history[history.length - 1]); setHistory((h) => h.slice(0, -1)); };
  const redo = () => { if (!future.length) return; setHistory((h) => [...h, c]); setC(future[0]); setFuture((f) => f.slice(1)); };

  // ------------------------------------------------ save / run
  const save = async (cc = c) => {
    const r = await api.saveChase(cc);
    setSaved(JSON.stringify(cc));
    return r.id;
  };
  const runOnLamps = async () => {
    const id = await save();
    const fx = { ...full!.state.effect, chaseId: id, screenSync: false };
    if (!target) await api.effect(fx, true);
    else await api.startLayer(target, fx);
    setMsg(t("ch.running"));
  };
  // while it runs on the lamps, changes go live (shortly after the last edit)
  useEffect(() => {
    if (!running || !dirty) return;
    const h = setTimeout(() => { save().catch(() => {}); }, 250);
    return () => clearTimeout(h);
  }, [c, running]); // eslint-disable-line react-hooks/exhaustive-deps

  const close = () => {
    if (dirty && !confirm(t("ch.unsaved"))) return;
    onClose();
  };
  const remove = async () => {
    if (!confirm(t("ch.confirmDelete", { name: c.name }))) return;
    if (exists) await api.removeChase(c.id);
    onClose();
  };
  const duplicate = async () => {
    const copy = { ...c, id: newId(), name: c.name + " " + t("ch.copySuffix") };
    await api.saveChase(copy);
    setC(copy); setSaved(JSON.stringify(copy)); setHistory([]); setFuture([]);
    setMsg(t("ch.duplicated"));
  };

  // keyboard: Ctrl+Z / Ctrl+Y inside the editor, Escape closes
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if ((e.target as HTMLElement)?.tagName === "INPUT" && (e.target as HTMLInputElement).type !== "range") return;
      if (e.ctrlKey && e.key.toLowerCase() === "z") { e.preventDefault(); e.stopPropagation(); undo(); }
      else if (e.ctrlKey && e.key.toLowerCase() === "y") { e.preventDefault(); e.stopPropagation(); redo(); }
      else if (e.key === "Escape") close();
    };
    window.addEventListener("keydown", key, true);
    return () => window.removeEventListener("keydown", key, true);
  });

  // ------------------------------------------------ preview (drawn directly, 60x per second)
  const liveRef = useRef(live); liveRef.current = live;
  const stripRef = useRef<HTMLDivElement>(null);
  const gridRef = useRef<HTMLDivElement>(null);
  const [paused, setPaused] = useState(false);
  const pausedRef = useRef(paused); pausedRef.current = paused;
  useEffect(() => {
    let raf = 0, last = -2;
    const t0 = performance.now();
    let pausedAt = 0, pauseTotal = 0;
    const frame = (now: number) => {
      raf = requestAnimationFrame(frame);
      if (pausedRef.current) { if (!pausedAt) pausedAt = now; return; }
      if (pausedAt) { pauseTotal += now - pausedAt; pausedAt = 0; }
      const ms = now - t0 - pauseTotal;
      const m = liveRef.current.music;
      const bpm = m?.bpm && m.bpm > 20 ? m.bpm : 120;
      const beat = m?.following || m?.listening ? m.beat : (ms * bpm) / 60000;
      const { colors, steps } = render(cRef.current, ms, previewLamps, beat, m?.levels?.level ?? 1);
      const strip = stripRef.current;
      if (strip) colors.forEach((col, i) => { const el = strip.children[i] as HTMLElement | undefined; if (el) el.style.background = col; });
      const cur = steps.find((s) => s >= 0) ?? -1;
      if (cur !== last && gridRef.current) {
        gridRef.current.querySelectorAll(".ch-row.now").forEach((r) => r.classList.remove("now"));
        gridRef.current.querySelector(`.ch-row[data-i="${cur}"]`)?.classList.add("now");
        last = cur;
      }
    };
    raf = requestAnimationFrame(frame);
    return () => cancelAnimationFrame(raf);
  }, [previewLamps]);

  // ------------------------------------------------ painting
  const paintValue = useRef<string | null | undefined>(undefined);
  useEffect(() => {
    const up = () => (paintValue.current = undefined);
    window.addEventListener("pointerup", up);
    return () => window.removeEventListener("pointerup", up);
  }, []);
  const setCell = (si: number, col: number, v: string | null, record: boolean) => {
    const steps = c.steps.map((s, i) => (i !== si ? s : { ...s, colors: s.colors.map((x, k) => (k === col ? v : x)) }));
    if (record) update({ ...c, steps }); else setC({ ...c, steps });
  };
  const down = (e: React.PointerEvent, si: number, col: number) => {
    e.preventDefault();
    // touch: pointers are captured by the first cell; release so moving paints the cells under the finger
    try { (e.target as Element).releasePointerCapture(e.pointerId); } catch { /* not captured */ }
    const curV = c.steps[si].colors[col];
    const v = e.button === 2 || brush === null ? null : curV === brush ? null : brush;
    paintValue.current = v;
    setCell(si, col, v, true);
  };
  // painting while dragging: the cell under the pointer (works with mouse, pen and touch, also on fast moves)
  const lastCell = useRef("");
  const move = (e: React.PointerEvent) => {
    if (paintValue.current === undefined || !(e.buttons & 3)) return;
    const el = (document.elementFromPoint(e.clientX, e.clientY) as HTMLElement | null)?.closest(".ch-cell") as HTMLElement | null;
    if (!el?.dataset.s) return;
    const key = el.dataset.s + ":" + el.dataset.c;
    if (key === lastCell.current) return;
    const [ps, pc] = lastCell.current ? lastCell.current.split(":").map(Number) : [NaN, NaN];
    lastCell.current = key;
    const si = +el.dataset.s!, col = +el.dataset.c!;
    // fast moves skip cells: fill the straight line from the previous cell
    const cells: [number, number][] = [];
    if (Number.isFinite(ps)) {
      const n = Math.max(Math.abs(si - ps), Math.abs(col - pc));
      for (let i = 1; i <= n; i++) cells.push([Math.round(ps + ((si - ps) * i) / n), Math.round(pc + ((col - pc) * i) / n)]);
    } else cells.push([si, col]);
    const v = paintValue.current;
    setC((cur) => ({
      ...cur,
      steps: cur.steps.map((st, i) => cells.some(([a]) => a === i)
        ? { ...st, colors: st.colors.map((x, k) => (cells.some(([a, b]) => a === i && b === k) ? v! : x)) }
        : st),
    }));
  };

  // ------------------------------------------------ step operations
  const setStep = (si: number, patch: Partial<ChaseStep>) => update({ ...c, steps: c.steps.map((s, i) => (i === si ? { ...s, ...patch } : s)) });
  const moveStep = (si: number, d: number) => {
    const j = si + d;
    if (j < 0 || j >= c.steps.length) return;
    const steps = [...c.steps];
    [steps[si], steps[j]] = [steps[j], steps[si]];
    update({ ...c, steps });
  };
  const dupStep = (si: number) => { if (c.steps.length >= 256) return; const steps = [...c.steps]; steps.splice(si + 1, 0, structuredClone(c.steps[si])); update({ ...c, steps }); };
  const delStep = (si: number) => update({ ...c, steps: c.steps.filter((_, i) => i !== si) });
  const addStep = () => {
    if (c.steps.length >= 256) return;
    const last = c.steps[c.steps.length - 1];
    update({ ...c, steps: [...c.steps, last ? { ...structuredClone(last), colors: last.colors.map((_, i, a) => a[(i - 1 + a.length) % a.length]) } : emptyStep(c.columns)] });
  };
  const applyTemplate = (k: TemplateKey) => {
    if (c.steps.some((s) => s.colors.some(Boolean)) && !confirm(t("ch.replaceConfirm"))) return;
    const tpl = template(k, c.columns, brush ?? "#FF0000", brush2, usedColors(c));
    update({ ...tpl, id: c.id, name: c.name });
  };

  const palette = useMemo(() => {
    const p = [...boot!.palette.fan, ...boot!.palette.specials].map((x) => x.hex.toUpperCase());
    const custom = (full!.state.customColors ?? []).map((x) => x.toUpperCase());
    return [...new Set([...p, ...custom, ...usedColors(c)])].slice(0, 40);
  }, [boot, full, c]);

  const order0 = order(c, 0).length;
  const cycleMs = c.syncToBeat ? null : order(c, 0).reduce((a, i) => a + Math.max(20, c.steps[i].durationMs ?? c.stepMs), 0) * 100 / Math.max(5, c.speedPercent);

  return (
    <div className="offline" onPointerDown={(e) => { if (e.target === e.currentTarget) close(); }}>
      <div className="box editor chase-editor" onContextMenu={(e) => e.preventDefault()}>
        {/* ---------- header ---------- */}
        <div className="row ch-head">
          <Icon name="sparkles" />
          <input className="input ch-name" value={c.name} maxLength={60} onChange={(e) => update({ ...c, name: e.target.value })} aria-label={t("ch.name")} />
          <span className="muted small">{dirty ? t("ch.notSaved") : exists ? t("ch.saved") : ""}</span>
          <div className="grow" />
          <button className="btn sm ghost" disabled={!history.length} onClick={undo} title={t("ch.undo")}><Icon name="undo" /></button>
          <button className="btn sm ghost" disabled={!future.length} onClick={redo} title={t("ch.redo")}><Icon name="redo" /></button>
          <button className={"btn sm toggle" + (showHelp ? " active" : "")} onClick={() => setShowHelp(!showHelp)}><Icon name="info" /> {t("help.button")}</button>
          <button className="btn ghost icon-btn" onClick={close} title={t("ch.close")}>×</button>
        </div>

        {showHelp && (
          <div className="note help-box">
            <Icon name="info" />
            <div>
              <b>{t("ch.help.title")}</b>
              <ol>
                {[1, 2, 3, 4, 5, 6].map((i) => <li key={i}>{t("ch.help." + i)}</li>)}
              </ol>
            </div>
          </div>
        )}

        {/* ---------- preview ---------- */}
        <div className="row" style={{ gap: 8 }}>
          <button className="btn sm ghost icon-btn" onClick={() => setPaused(!paused)} title={paused ? t("ch.play") : t("ch.pause")}>
            <Icon name={paused ? "play" : "pause"} />
          </button>
          <div className="preview-strip ch-strip grow" ref={stripRef} title={t("ch.previewHint")}>
            {Array.from({ length: previewLamps }, (_, i) => <div key={i} />)}
          </div>
        </div>
        <p className="hint" style={{ marginTop: -8 }}>
          {t("ch.previewInfo", { lamps: previewLamps, steps: c.steps.length, cycle: cycleMs ? (cycleMs / 1000).toFixed(1) + " s" : t("ch.onBeat") })}
          {order0 !== c.steps.length && " · " + t("ch.played", { n: order0 })}
        </p>

        <div className="ch-body">
          {/* ---------- grid ---------- */}
          <div className="ch-left">
            <div className="ch-brushes">
              <span className="muted small">{t("ch.brush")}</span>
              <button className={"sw off" + (brush === null ? " picked" : "")} onClick={() => setBrush(null)} title={t("ch.eraser")}>✕</button>
              {palette.map((h) => (
                <button key={h} className={"sw" + (brush === h ? " picked" : "")} style={{ background: h }} title={h}
                  onClick={() => setBrush(h)} onContextMenu={(e) => { e.preventDefault(); setBrush2(h); }} />
              ))}
              <label className="sw pick" title={t("ch.ownColor")} style={{ background: brush ?? "#333" }}>
                +<ColorInput value={brush ?? "#FF0000"} onPick={(h) => setBrush(h)} />
              </label>
              <span className="muted small" style={{ marginLeft: 8 }}>{t("ch.second")}</span>
              <label className="sw pick" title={t("ch.secondHint")} style={{ background: brush2, color: contrast(brush2) }}>
                B<ColorInput value={brush2} onPick={setBrush2} />
              </label>
              <Help k="ch.brushHelp" />
            </div>

            <div className="ch-grid" ref={gridRef} style={{ ["--cols" as string]: c.columns }} onPointerMove={move}>
              <div className="ch-row head">
                <span className="ch-n">#</span>
                <div className="ch-cells">{Array.from({ length: c.columns }, (_, i) => <span key={i} className="ch-colnum">{i + 1}</span>)}</div>
                <span className="ch-sm">{t("ch.time")}</span>
                <span className="ch-sm">{t("ch.level")}</span>
                <span className="ch-sm">{t("ch.fade")}</span>
                <span className="ch-tools" />
              </div>
              {c.steps.map((s, si) => (
                <div key={si} className="ch-row" data-i={si}>
                  <span className="ch-n">{si + 1}</span>
                  <div className="ch-cells">
                    {s.colors.map((col, k) => (
                      <span key={k} className={"ch-cell" + (col ? " on" : "")}
                        style={col ? { background: col, opacity: 0.35 + 0.65 * (s.level / 100) } : undefined}
                        data-s={si} data-c={k} onPointerDown={(e) => { lastCell.current = si + ":" + k; down(e, si, k); }} />
                    ))}
                  </div>
                  <NumberField className="input ch-sm" min={20} max={600000} allowEmpty placeholder={String(c.stepMs)} title={t("ch.timeHint")}
                    value={s.durationMs ?? null} disabled={c.syncToBeat} onCommit={(v) => setStep(si, { durationMs: v })} />
                  <NumberField className="input ch-sm" min={0} max={100} title={t("ch.levelHint")}
                    value={s.level} onCommit={(v) => setStep(si, { level: v ?? 100 })} />
                  <NumberField className="input ch-sm" min={0} max={100} allowEmpty placeholder={String(c.fadePercent)} title={t("ch.fadeHint")}
                    value={s.fadePercent ?? null} onCommit={(v) => setStep(si, { fadePercent: v })} />
                  <span className="ch-tools">
                    <button className="btn sm ghost" title={t("ch.up")} disabled={si === 0} onClick={() => moveStep(si, -1)}>↑</button>
                    <button className="btn sm ghost" title={t("ch.down")} disabled={si === c.steps.length - 1} onClick={() => moveStep(si, 1)}>↓</button>
                    <button className="btn sm ghost" title={t("ch.fillRow")} onClick={() => setStep(si, { colors: s.colors.map(() => brush) })}>■</button>
                    <button className="btn sm ghost" title={t("ch.dupStep")} onClick={() => dupStep(si)}>⧉</button>
                    <button className="btn sm ghost" title={t("ch.delStep")} disabled={c.steps.length <= 1} onClick={() => delStep(si)}><Icon name="trash" /></button>
                  </span>
                </div>
              ))}
              <div className="row" style={{ gap: 6, marginTop: 6 }}>
                <button className="btn sm" onClick={addStep} disabled={c.steps.length >= 256}><Icon name="plus" /> {t("ch.addStep")}</button>
                <button className="btn sm ghost" onClick={() => update({ ...c, steps: [...c.steps, emptyStep(c.columns)] })} disabled={c.steps.length >= 256}>{t("ch.addEmpty")}</button>
                <span className="hint">{t("ch.addHint")}</span>
              </div>
            </div>

            <h3 className="ch-h">{t("ch.tools")} <Help k="ch.toolsHelp" /></h3>
            <div className="row wrap" style={{ gap: 6 }}>
              <button className="btn sm" onClick={() => update(tools.shift(c, -1))} title={t("ch.shiftHint")}>← {t("ch.shift")}</button>
              <button className="btn sm" onClick={() => update(tools.shift(c, 1))} title={t("ch.shiftHint")}>{t("ch.shift")} →</button>
              <button className="btn sm" onClick={() => update(tools.mirror(c))}>{t("ch.mirror")}</button>
              <button className="btn sm" onClick={() => update(tools.reverseSteps(c))}>{t("ch.reverseSteps")}</button>
              <button className="btn sm" onClick={() => update(tools.invert(c, brush ?? "#FFFFFF"))}>{t("ch.invert")}</button>
              <button className="btn sm" onClick={() => update(tools.recolor(c, brush ?? "#FFFFFF"))}>{t("ch.recolor")}</button>
              <button className="btn sm" onClick={() => update(tools.addShiftedCopies(c))} title={t("ch.moveHint")}>{t("ch.move")}</button>
              <button className="btn sm off" onClick={() => update(tools.clear(c))}>{t("ch.clear")}</button>
            </div>

            <h3 className="ch-h">{t("ch.templates")} <Help k="ch.templatesHelp" /></h3>
            <div className="row wrap" style={{ gap: 6 }}>
              {TEMPLATES.map((k) => <button key={k} className="btn sm ghost" onClick={() => applyTemplate(k)}>{t("ch.tpl." + k)}</button>)}
            </div>
          </div>

          {/* ---------- settings ---------- */}
          <div className="ch-right stack">
            <Setting label={t("ch.columns")} help="ch.columnsHelp">
              <div className="row" style={{ gap: 4 }}>
                <button className="btn sm" onClick={() => update(withColumns(c, c.columns - 1))} disabled={c.columns <= 1}>−</button>
                <NumberField min={1} max={64} value={c.columns} onCommit={(v) => update(withColumns(c, v ?? c.columns))} />
                <button className="btn sm" onClick={() => update(withColumns(c, c.columns + 1))} disabled={c.columns >= 64}>+</button>
                {full!.rig.heads > 0 && c.columns !== full!.rig.heads && (
                  <button className="btn sm ghost" onClick={() => update(withColumns(c, full!.rig.heads))}>{t("ch.asLamps", { n: full!.rig.heads })}</button>
                )}
              </div>
            </Setting>

            <label className="check">
              <input type="checkbox" checked={c.syncToBeat} onChange={(e) => update({ ...c, syncToBeat: e.target.checked })} />
              <span className="box" /><span>{t("ch.sync")}</span><Help k="ch.syncHelp" />
            </label>
            {c.syncToBeat ? (
              <Setting label={t("ch.beatsPerStep")}>
                <div className="chips">
                  {BEATS.map((b) => (
                    <span key={b} className={"chip" + (c.beatsPerStep === b ? " active" : "")} onClick={() => update({ ...c, beatsPerStep: b })}>
                      {b < 1 ? "1/" + 1 / b : b}
                    </span>
                  ))}
                </div>
                {!live.music?.following && !live.music?.listening && <p className="hint">{t("ch.syncNoBeat")}</p>}
              </Setting>
            ) : (
              <Setting label={t("ch.stepTime")} help="ch.stepTimeHelp">
                <Slider value={c.stepMs} min={20} max={5000} log unit="ms" onChange={(v) => update({ ...c, stepMs: v })} />
              </Setting>
            )}
            <Setting label={t("ch.speed")} help="ch.speedHelp">
              <Slider value={c.speedPercent} min={10} max={500} log unit="%" onChange={(v) => update({ ...c, speedPercent: v })} />
            </Setting>
            <Setting label={t("ch.fadeAll")} help="ch.fadeHelp">
              <Slider value={c.fadePercent} min={0} max={100} unit="%" onChange={(v) => update({ ...c, fadePercent: v })} />
            </Setting>
            <Setting label={t("ch.direction")} help="ch.directionHelp">
              <div className="seg">
                {DIRECTIONS.map((d) => <button key={d} className={c.direction === d ? "active" : ""} onClick={() => update({ ...c, direction: d })}>{t("ch.dir." + d)}</button>)}
              </div>
            </Setting>
            <Setting label={t("ch.mapping")} help="ch.mappingHelp">
              <div className="seg">
                {MAPPINGS.map((m) => <button key={m} className={c.mapping === m ? "active" : ""} onClick={() => update({ ...c, mapping: m })}>{t("ch.map." + m)}</button>)}
              </div>
              <p className="hint">{t("ch.map." + c.mapping + ".hint")}</p>
            </Setting>
            <Setting label={t("ch.wave")} help="ch.waveHelp">
              <div className="row" style={{ gap: 4 }}>
                <button className="btn sm" onClick={() => update({ ...c, repeatOffset: Math.max(-16, c.repeatOffset - 1) })}>−</button>
                <NumberField min={-16} max={16} value={c.repeatOffset} onCommit={(v) => update({ ...c, repeatOffset: v ?? 0 })} />
                <button className="btn sm" onClick={() => update({ ...c, repeatOffset: Math.min(16, c.repeatOffset + 1) })}>+</button>
                <span className="hint">{t("ch.steps")}</span>
              </div>
            </Setting>
            <label className="check">
              <input type="checkbox" checked={!!c.background} onChange={(e) => update({ ...c, background: e.target.checked ? brush2 : null })} />
              <span className="box" /><span>{t("ch.background")}</span><Help k="ch.backgroundHelp" />
              {c.background && (
                <span className="sw pick" style={{ background: c.background, marginLeft: 6 }}>
                  <ColorInput value={c.background} onPick={(h) => update({ ...c, background: h })} />
                </span>
              )}
            </label>
            <label className="check">
              <input type="checkbox" checked={c.audioLevel} onChange={(e) => update({ ...c, audioLevel: e.target.checked })} />
              <span className="box" /><span>{t("ch.audio")}</span><Help k="ch.audioHelp" />
            </label>

            <div className="ch-run stack">
              {groups.length > 0 && (
                <label className="field">
                  {t("ch.runOn")}
                  <select className="input" value={target} onChange={(e) => setTarget(e.target.value)}>
                    <option value="">{t("fx.allLamps")}</option>
                    {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
                  </select>
                </label>
              )}
              <button className="btn primary" onClick={runOnLamps}><Icon name="play" /> {running ? t("ch.update") : t("ch.runNow")}</button>
              {running && <p className="hint">{t("ch.liveHint")}</p>}
              <div className="row" style={{ gap: 6 }}>
                <button className="btn grow" disabled={!dirty && exists} onClick={() => save().then(() => setMsg(t("ch.savedMsg")))}><Icon name="save" /> {t("ch.save")}</button>
                <button className="btn" onClick={duplicate} title={t("ch.duplicate")}>⧉</button>
                <button className="btn off" onClick={remove} title={t("ch.delete")}><Icon name="trash" /></button>
              </div>
              {msg && <p className="hint">{msg}</p>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function Setting({ label, help, children }: { label: string; help?: string; children: React.ReactNode }) {
  return (
    <div className="field ch-setting">
      <span className="row" style={{ gap: 4 }}>{label}{help && <Help k={help} />}</span>
      {children}
    </div>
  );
}

/** Slider (optionally logarithmic) plus number box; reports on release / Enter. */
function Slider({ value, min, max, log, unit, onChange }: { value: number; min: number; max: number; log?: boolean; unit: string; onChange: (v: number) => void }) {
  const [v, setV] = useState(value);
  useEffect(() => setV(value), [value]);
  const lo = Math.log(Math.max(1, min)), hi = Math.log(max);
  const toPos = (x: number) => (log ? Math.round(((Math.log(Math.max(1, x)) - lo) / (hi - lo)) * 1000) : Math.round(((x - min) / (max - min)) * 1000));
  const fromPos = (p: number) => {
    const raw = log ? Math.exp(lo + (p / 1000) * (hi - lo)) : min + (p / 1000) * (max - min);
    return Math.round(raw / (raw > 200 ? 10 : 1)) * (raw > 200 ? 10 : 1);
  };
  const commit = (n: number) => { const x = Math.min(max, Math.max(min, Number.isFinite(n) ? n : min)); setV(x); if (x !== value) onChange(x); };
  return (
    <div className="row" style={{ gap: 8 }}>
      <input type="range" className="grow" min={0} max={1000} value={toPos(v)} onChange={(e) => setV(fromPos(+e.target.value))}
        onPointerUp={() => commit(v)} onKeyUp={() => commit(v)} />
      <input className="input num" type="number" value={v} min={min} max={max}
        onChange={(e) => setV(+e.target.value)} onBlur={() => commit(v)} onKeyDown={(e) => e.key === "Enter" && commit(v)} />
      <span className="muted small">{unit}</span>
    </div>
  );
}

function usedColors(c: CustomChase) {
  return [...new Set(c.steps.flatMap((s) => s.colors.filter((x): x is string => !!x).map((x) => x.toUpperCase())))];
}
function firstColor(c: CustomChase) { return usedColors(c)[0] ?? null; }
