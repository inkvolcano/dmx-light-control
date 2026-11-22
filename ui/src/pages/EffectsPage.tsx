import { useEffect, useMemo, useState } from "react";
import { api, type EffectInfo, type EffectParam, type EffectSettings } from "../api";
import { type CustomChase, newChase } from "../chase";
import { ChaseEditor } from "../components/ChaseEditor";
import { ColorInput, contrast } from "../components/ColorPicker";
import { FxPreview } from "../components/FxPreview";
import type { DragStart } from "../components/LiveTools";
import { toast } from "../components/Toast";
import { Help } from "../components/Help";
import { Icon } from "../components/Icon";
import { ScreenSyncPanel } from "../components/ScreenSyncPanel";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

// Grouping for the category filter. Effects not listed here show under "All" only.
const categories: Record<string, string[]> = {
  onoff: ["blink", "chase-delay", "alternate-onoff", "random-blink", "strobe-white", "lightning", "heartbeat"],
  chase: ["chase-right", "chase-left", "bounce", "build-up", "build-down", "fill-empty", "outside-in", "inside-out",
    "two-chasers", "comet", "double-chase-ab", "bouncing-ball", "rainbow-chase", "scanner", "theater-chase", "meteor"],
  smooth: ["color-cycle", "rainbow-smooth", "breathe-delay", "wave-a", "color-wave-ab", "breathe-wave", "pulse-ab",
    "gradient-ab", "gradient-rotate", "random-breathe"],
  color: ["alternate-ab", "all-ab", "rainbow-steps", "random-flash", "random-colors", "random-ab", "disco", "sparkle"],
  theme: ["police", "fire", "snow", "sunset", "ambulance", "christmas", "fire-brigade", "waterfall", "candle", "aurora", "ocean"],
  nature: [],
  party: [],
  sound: ["audio-beat-colors", "audio-beat-ab", "audio-beat-random", "audio-beat-chase", "audio-spectrum", "audio-bands", "audio-vu",
    "audio-vu-center", "audio-volume-color", "audio-bass-flash", "audio-pitch-color", "audio-sparkle", "audio-fire", "audio-surge-strobe"],
};


// The filter shows a few broad groups; each effect's own category falls into one of them.
const groupOf: Record<string, string> = {
  onoff: "rhythm", chase: "rhythm", smooth: "calm", nature: "calm", color: "colors", theme: "colors", party: "colors", sound: "sound",
};
const filters = ["all", "fav", "recent", "rhythm", "calm", "colors", "sound", "own"];

/** A list of effect keys kept on this device (favourites, recently used). */
function useKeyList(name: string, max = 200): [string[], (next: string[]) => void] {
  const [list, setList] = useState<string[]>(() => { try { return JSON.parse(localStorage.getItem(name) ?? "[]"); } catch { return []; } });
  const save = (next: string[]) => {
    const v = next.slice(0, max);
    setList(v);
    try { localStorage.setItem(name, JSON.stringify(v)); } catch { /* ignore */ }
  };
  return [list, save];
}

/** The effects page is now the Effecten tab of Bediening; kept for old links. */
export function EffectsPage() {
  return <EffectsPanel />;
}

/**
 * Effects: pick one, its colours and speed. With lamps chosen on the stage (`selected`) the effect runs on
 * those lamps only; otherwise on all lamps (or on the group chosen at "for").
 */
export function EffectsPanel({ selected = [], onDrag, requireSelection = false }: { selected?: string[]; onDrag?: DragStart; requireSelection?: boolean }) {
  const t = useT();
  const { full, boot } = useShow();
  const { heads } = useLive();
  const [cat, setCat] = useState("all");
  const [query, setQuery] = useState("");
  const [local, setLocal] = useState<EffectSettings | null>(null);
  const [target, setTarget] = useState("");  // "" = all fixtures, else a group id
  const [editing, setEditing] = useState<CustomChase | null>(null);
  const [favs, setFavs] = useKeyList("fxFavorites");
  const [recent, setRecent] = useKeyList("fxRecent", 12);
  const chases = full!.state.chases ?? [];

  const groups = full!.groups;
  const layers = full!.state.layers;
  // lamps chosen on the stage: their own effect (the layer that holds exactly these lamps)
  const sel = selected.length > 0 && selected.length < full!.patch.length ? selected : [];
  const selKey = [...sel].sort().join(",");
  const selLayer = sel.length ? layers.find((l) => !l.source && l.fixtureIds && [...l.fixtureIds].sort().join(",") === selKey) : undefined;
  const layer = sel.length ? selLayer : target ? layers.find((l) => l.groupId === target) : undefined;
  const server = layer?.effect ?? full!.state.effect;
  // follow server changes unless the user is mid-edit (local is cleared after each send)
  useEffect(() => setLocal(null), [server, target, selKey]);
  useEffect(() => { if (target && !groups.some((g) => g.id === target)) setTarget(""); }, [groups, target]);
  const fx = local ?? server;
  const running = sel.length ? !!selLayer : target ? !!layer : full!.state.mode === "effect";
  const effects = boot!.effects;
  const screen = !!fx.screenSync;
  const chase = !screen && fx.chaseId ? chases.find((c) => c.id === fx.chaseId) : undefined;
  const current = chase || screen ? undefined : effects[fx.effectIndex];
  const uses = new Set<EffectParam>(current?.uses ?? []);

  const name = (e: EffectInfo) => t("fxname." + e.key, undefined, e.name);
  const groupOfEffect = (e: EffectInfo) => {
    const c = e.category ?? Object.keys(categories).find((k) => categories[k].includes(e.key));
    return c ? groupOf[c] : undefined;
  };
  const list = useMemo(() => {
    const hit = (e: EffectInfo) => !query || name(e).toLowerCase().includes(query.toLowerCase());
    if (cat === "own") return [];
    if (cat === "fav") return favs.map((k) => effects.find((e) => e.key === k)).filter((e): e is EffectInfo => !!e && hit(e));
    if (cat === "recent") return recent.map((k) => effects.find((e) => e.key === k)).filter((e): e is EffectInfo => !!e && hit(e));
    return effects.filter((e) => (cat === "all" || groupOfEffect(e) === cat) && hit(e));
  }, [effects, cat, query, favs, recent, full?.settings.language]); // eslint-disable-line react-hooks/exhaustive-deps
  const toggleFav = (k: string) => setFavs(favs.includes(k) ? favs.filter((x) => x !== k) : [k, ...favs]);

  const send = (next: EffectSettings, start = running) => {
    setLocal(next);
    // on Bediening nothing chosen means nothing happens (choose lamps, or "Alles kiezen")
    if (requireSelection && selected.length === 0) { if (start) toast(t("live.pickFirst"), false); return; }
    if (sel.length) { if (start) api.selectionEffect(sel, next); }
    else if (!target) api.effect(next, start);
    else if (start) api.startLayer(target, next);
  };
  const stop = () => (sel.length ? api.selectionEffect(sel, null) : target ? api.stopLayer(target) : api.mode("static"));

  const choose = (e: EffectInfo) => {
    if (running && !chase && !screen && e.index === fx.effectIndex) stop();
    else {
      setRecent([e.key, ...recent.filter((k) => k !== e.key)]);
      send({ ...fx, effectIndex: e.index, chaseId: null, screenSync: false }, true);
    }
  };
  const chooseChase = (c: CustomChase) => {
    if (running && chase?.id === c.id) stop();
    else send({ ...fx, chaseId: c.id, screenSync: false }, true);
  };
  const ownList = cat === "all" || cat === "own" ? chases.filter((c) => !query || c.name.toLowerCase().includes(query.toLowerCase())) : [];
  const create = () => setEditing(newChase(t("ch.newName", { n: chases.length + 1 }), Math.min(8, Math.max(full!.rig.heads, 4))));
  const chooseScreen = () => (running && screen ? stop() : send({ ...fx, screenSync: true, chaseId: null }, true));
  const layerName = (e: EffectSettings) => e.screenSync ? t("fx.screen") : e.chaseId ? (chases.find((c) => c.id === e.chaseId)?.name ?? "?") : effects[e.effectIndex] ? name(effects[e.effectIndex]) : "";

  const usedList = (["ColorA", "ColorB", "Step", "OnOff", "Delay"] as EffectParam[])
    .filter((p) => uses.has(p))
    .map((p) => (p === "ColorA" ? (fx.colorPerLamp ? t("fx.lampColors") : t("fx.colorA")) : p === "ColorB" ? t("fx.colorB")
      : p === "Step" ? t("fx.step") : p === "OnOff" ? t("fx.on") + "/" + t("fx.off") : t("fx.delay")));

  const layerTitle = (id: string) => id.startsWith("scene:")
    ? t("rail.sceneOnSelection", { name: full!.scenes.find((x) => "scene:" + x.id === id)?.name ?? "?" })
    : id.startsWith("sel:") ? t("rail.selectionLayer", { n: layers.find((l) => l.groupId === id)?.fixtureIds?.length ?? 0 })
    : groups.find((g) => g.id === id)?.name ?? "?";

  return (
    <div className="fx-layout">
      {/* the chosen effect on top, in one wide bar: name and start/stop, what the lamps do, colours and speed */}
      <div className="card fx-settings">
        <div className="fx-set-top">
          <h2 className="fx-set-name">{screen ? t("fx.screen") : chase ? chase.name : current ? name(current) : ""}</h2>
          <Help k="fx.panelHelp" />
          {running
            ? <button className="btn off active" onClick={stop}><Icon name="stop" /> {t("fx.stop")}</button>
            : <button className="btn primary" onClick={() => send(fx, true)}><Icon name="play" /> {t("fx.start")}</button>}
          <div className="preview-strip fx-set-strip" title={t("fx.preview")}>
            {(heads.length ? heads : ["#000000"]).map((h, i) => <div key={i} style={{ background: h }} />)}
          </div>
        </div>
        {screen ? (
          <ScreenSyncPanel />
        ) : chase ? (
          <div className="row">
            <p className="hint grow" style={{ margin: 0 }}>{t("ch.panelHint")}</p>
            <button className="btn" onClick={() => setEditing(chase)}><Icon name="edit" /> {t("ch.edit")}</button>
          </div>
        ) : (
          <>
            {/* left: the colours and "colour per lamp"; right: speed and every time, under each other */}
            <div className="fx-set-body">
              <div className="fx-set-colors">
                <div className="fx-set-ab">
                  <ColorField label={t("fx.colorA")} value={fx.colorA} disabled={!uses.has("ColorA") || fx.colorPerLamp}
                    onPick={(hex) => send({ ...fx, colorA: hex })} />
                  <ColorField label={t("fx.colorB")} value={fx.colorB} disabled={!uses.has("ColorB")}
                    onPick={(hex) => send({ ...fx, colorB: hex })} />
                </div>
                <label className="check">
                  <input type="checkbox" checked={fx.colorPerLamp} disabled={!uses.has("ColorA")}
                    onChange={(e) => send({ ...fx, colorPerLamp: e.target.checked })} />
                  <span className="box" />
                  <span className={uses.has("ColorA") ? "" : "muted"}>{t("fx.perLamp")}</span>
                </label>
                {fx.colorPerLamp && uses.has("ColorA") && (
                  <div className="lampcolors">
                    {Array.from({ length: Math.max(full!.rig.heads, 1) }, (_, i) => {
                      const c = fx.lampColors[i] ?? "#FFFFFF";
                      return (
                        <label key={i} className="colorbtn" style={{ background: c, color: contrast(c) }}>
                          {i + 1}
                          <ColorInput value={c} onPick={(hex) => {
                            const lc = [...fx.lampColors];
                            while (lc.length <= i) lc.push("#FFFFFF");
                            lc[i] = hex;
                            send({ ...fx, lampColors: lc });
                          }} />
                        </label>
                      );
                    })}
                  </div>
                )}
              </div>
              <div className="fx-set-sliders">
                {(uses.has("Step") || uses.has("OnOff") || uses.has("Delay")) && (
                  <SpeedParam fx={fx} uses={uses} onChange={(next) => send(next)} beat={full!.state.music.syncEffects} />
                )}
                <TimeParam label={t("fx.step")} value={fx.stepMs} min={40} max={5000} enabled={uses.has("Step")} onChange={(v) => send({ ...fx, stepMs: v })} />
                <TimeParam label={t("fx.on")} value={fx.onMs} min={20} max={10000} enabled={uses.has("OnOff")} onChange={(v) => send({ ...fx, onMs: v })} />
                <TimeParam label={t("fx.off")} value={fx.offMs} min={20} max={10000} enabled={uses.has("OnOff")} onChange={(v) => send({ ...fx, offMs: v })} />
                <TimeParam label={t("fx.delay")} value={fx.delayMs} min={0} max={10000} enabled={uses.has("Delay")} onChange={(v) => send({ ...fx, delayMs: v })} />
              </div>
            </div>
            <p className="hint" style={{ margin: 0 }}>{usedList.length ? t("fx.uses", { list: usedList.join(", ") }) : t("fx.usesNone")}</p>
          </>
        )}
        {layers.length > 0 && (
          <div className="stack fx-set-layers" style={{ gap: 6 }}>
            <h3>{t("fx.layers")}</h3>
            {layers.map((l) => (
              <div key={l.groupId} className="layer-row">
                <b>{layerTitle(l.groupId)}</b>
                <span className="grow muted">{layerName(l.effect)}</span>
                {!l.groupId.includes(":") && <button className="btn sm" onClick={() => setTarget(l.groupId)}>⋯</button>}
                <button className="btn sm off" onClick={() => api.stopLayer(l.groupId)}>{t("fx.layerStop")}</button>
              </div>
            ))}
          </div>
        )}
      </div>
      <div>
        <div className="page-head fx-head">
          {sel.length > 0
            ? <span className="chip ok"><span className="dot" />{t("fx.forSelection", { n: sel.length })}</span>
            : <span className="chip">{t("fx.forAll")}</span>}
          {sel.length === 0 && groups.length > 0 && (
            <label className="row" style={{ gap: 8 }}>
              <span className="muted">{t("fx.target")}</span>
              <select className="input" value={target} onChange={(e) => setTarget(e.target.value)}>
                <option value="">{t("fx.allLamps")}</option>
                {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </label>
          )}
          <div className="grow" />
          <div className="row" style={{ position: "relative" }}>
            <input className="input" style={{ width: 200 }} placeholder={t("fx.search")} value={query} onChange={(e) => setQuery(e.target.value)} />
          </div>
        </div>
        <div className="chips" style={{ marginBottom: 14 }}>
          {filters.map((c) => (
            <span key={c} className={"chip" + (cat === c ? " active" : "")} onClick={() => setCat(c)}>
              {c === "fav" ? "★ " : ""}{t("fxcat." + c)}{c === "fav" && favs.length ? ` (${favs.length})` : ""}
            </span>
          ))}
        </div>
        {cat === "fav" && !favs.length && <p className="hint" style={{ marginBottom: 10 }}>{t("fx.favEmpty")}</p>}
        {cat === "recent" && !recent.length && <p className="hint" style={{ marginBottom: 10 }}>{t("fx.recentEmpty")}</p>}
        {cat === "own" && <p className="hint" style={{ marginBottom: 10 }}>{t("ch.intro")}</p>}
        <div className="fx-grid">
          {(cat === "own" || (cat === "all" && !query)) && (
            <button className="fx-card fx-new-chase" onClick={create}>
              <span className="name"><Icon name="plus" /> {t("ch.new")}</span>
              <span className="hint">{t("ch.newHint")}</span>
            </button>
          )}
          {(cat === "all" || cat === "own") && !query && (
            <button className={"fx-card screen" + (running && screen ? " active" : "")} onClick={chooseScreen}>
              <span className="name"><Icon name="monitor" /> {t("fx.screen")}</span>
              <span className="bar" style={{ ["--fxbar" as string]: "linear-gradient(90deg,#1a3a8a,#e0702a,#2a9a5a,#7a2ac0)" }} />
              {running && screen && <span className="badge">{t("fx.running")}</span>}
            </button>
          )}
          {ownList.map((c) => {
            const active = running && chase?.id === c.id;
            const cols = [...new Set(c.steps.flatMap((s) => s.colors.filter(Boolean) as string[]))].slice(0, 6);
            const bar = cols.length > 1 ? `linear-gradient(90deg, ${cols.join(",")})` : cols[0] ?? "var(--line)";
            return (
              <button key={c.id} className={"fx-card own" + (active ? " active" : "")} onClick={() => chooseChase(c)}
                onPointerDown={onDrag ? (ev) => onDrag({ type: "effect", fx: effects[0], chase: { id: c.id, name: c.name } }, ev) : undefined}>
                <span className="name">{c.name}</span>
                <span className="bar" style={{ ["--fxbar" as string]: bar }} />
                {active && <span className="badge">{t("fx.running")}</span>}
                <span className="fx-edit" role="button" title={t("ch.edit")} onClick={(e) => { e.stopPropagation(); setEditing(c); }}><Icon name="edit" /></span>
              </button>
            );
          })}
          {list.map((e) => {
            const active = running && !chase && !screen && e.index === fx.effectIndex;
            return (
              <button key={e.key} className={"fx-card" + (active ? " active" : "")} onClick={() => choose(e)}
                onPointerDown={onDrag ? (ev) => onDrag({ type: "effect", fx: e }, ev) : undefined}>
                <span className="name">{name(e)}</span>
                {/* always the moving example (while on screen), in the current colours and speed */}
                <FxPreview index={e.index} fx={fx} lamps={Math.min(8, Math.max(4, full!.rig.heads))} />
                {active ? <span className="badge">{t("fx.running")}</span> : e.uses.includes("Audio") || e.uses.includes("Beat") ? <span className="fx-new">♪</span> : null}
                <span className={"fx-fav" + (favs.includes(e.key) ? " on" : "")} role="button" aria-label={t("fx.fav")} title={t("fx.fav")}
                  onClick={(ev) => { ev.stopPropagation(); toggleFav(e.key); }}>★</span>
              </button>
            );
          })}
        </div>
      </div>

      {editing && <ChaseEditor key={editing.id} initial={editing} target={target} onClose={() => setEditing(null)} />}
    </div>
  );
}

function ColorField({ label, value, disabled, onPick }: { label: string; value: string; disabled: boolean; onPick: (hex: string) => void }) {
  return (
    <label className="field">
      {label}
      <span className={"colorbtn" + (disabled ? " disabled" : "")} style={{ background: value, color: contrast(value) }}>
        {value}
        <ColorInput value={value} onPick={onPick} />
      </span>
    </label>
  );
}

/**
 * One "speed" slider instead of four times: it makes every time the effect uses shorter or longer by the same
 * factor, so the effect keeps its character (an even blink stays even). Slow on the left, fast on the right.
 */
function SpeedParam({ fx, uses, onChange, beat }: { fx: EffectSettings; uses: Set<EffectParam>; onChange: (fx: EffectSettings) => void; beat: boolean }) {
  const t = useT();
  // the time that sets the pace: the step, else one on/off cycle, else the delay
  const base = uses.has("Step") ? fx.stepMs : uses.has("OnOff") ? fx.onMs + fx.offMs : Math.max(fx.delayMs, 20);
  const lo = Math.log(40), hi = Math.log(5000);
  const toPos = (ms: number) => Math.round((1 - (Math.log(Math.min(5000, Math.max(40, ms))) - lo) / (hi - lo)) * 1000);
  const [pos, setPos] = useState(toPos(base));
  useEffect(() => setPos(toPos(base)), [base]); // eslint-disable-line react-hooks/exhaustive-deps
  const commit = () => {
    const ms = Math.exp(lo + (1 - pos / 1000) * (hi - lo));
    const f = ms / Math.max(1, base);
    const sc = (v: number, min: number, max: number) => Math.min(max, Math.max(min, Math.round((v * f) / 10) * 10));
    onChange({ ...fx, stepMs: sc(fx.stepMs, 40, 5000), onMs: sc(fx.onMs, 20, 10000), offMs: sc(fx.offMs, 20, 10000), delayMs: fx.delayMs ? sc(fx.delayMs, 0, 10000) : 0 });
  };
  return (
    <div className="stack" style={{ gap: 4 }}>
      <div className="param">
        <span className="lbl">{t("fx.speed")}</span>
        <input type="range" min={0} max={1000} value={pos} onChange={(e) => setPos(+e.target.value)} onPointerUp={commit} onKeyUp={commit} />
        <span className="mono muted param-val">{(1000 / Math.max(1, base)).toFixed(base > 1000 ? 2 : 1)}/s</span>
      </div>
      {beat && <p className="hint">{t("fx.speedBeat")}</p>}
    </div>
  );
}

/** Slider on a log scale (fine control for short times) plus a number box. */
function TimeParam({ label, value, min, max, enabled, onChange }:
  { label: string; value: number; min: number; max: number; enabled: boolean; onChange: (v: number) => void }) {
  const lo = Math.log(Math.max(min, 10)), hi = Math.log(max);
  const toPos = (v: number) => Math.round(((Math.log(Math.max(v, Math.max(min, 10))) - lo) / (hi - lo)) * 1000);
  const fromPos = (p: number) => Math.round(Math.exp(lo + (p / 1000) * (hi - lo)) / 10) * 10;
  const [v, setV] = useState(value);
  useEffect(() => setV(value), [value]);
  const commit = (n: number) => { const c = Math.min(max, Math.max(min, n || min)); setV(c); onChange(c); };
  return (
    <div className={"param" + (enabled ? "" : " disabled")}>
      <span className="lbl">{label}</span>
      <input type="range" min={0} max={1000} value={toPos(v)} onChange={(e) => setV(fromPos(+e.target.value))}
        onPointerUp={() => commit(v)} onKeyUp={() => commit(v)} />
      <input className="input num" type="number" value={v} min={min} max={max} step={10}
        onChange={(e) => setV(+e.target.value)} onBlur={() => commit(v)} onKeyDown={(e) => e.key === "Enter" && commit(v)} />
    </div>
  );
}
