import { useEffect, useMemo, useState } from "react";
import { api, type EffectInfo, type EffectParam, type EffectSettings } from "../api";
import { type CustomChase, newChase } from "../chase";
import { ChaseEditor } from "../components/ChaseEditor";
import { ColorInput, contrast } from "../components/ColorPicker";
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
  sound: ["audio-beat-colors", "audio-beat-ab", "audio-beat-random", "audio-beat-chase", "audio-spectrum", "audio-bands", "audio-vu",
    "audio-vu-center", "audio-volume-color", "audio-bass-flash", "audio-pitch-color", "audio-sparkle", "audio-fire", "audio-surge-strobe"],
};

// Little colour hint on each card.
const bars: Record<string, string> = {
  police: "linear-gradient(90deg,#f33 50%,#33f 50%)", ambulance: "linear-gradient(90deg,#33f 50%,#fff 50%)",
  "fire-brigade": "linear-gradient(90deg,#f33 50%,#fff 50%)", christmas: "linear-gradient(90deg,#f33,#3c3,#fff)",
  fire: "linear-gradient(90deg,#f40,#fa0,#f60)", sunset: "linear-gradient(90deg,#fd0,#f60,#a00)",
  waterfall: "linear-gradient(90deg,#0ff,#00f)", ocean: "linear-gradient(90deg,#03f,#0df,#03f)",
  aurora: "linear-gradient(90deg,#3f9,#0cf,#93f)", candle: "linear-gradient(90deg,#f80,#fb4,#f80)",
  snow: "linear-gradient(90deg,#fff,#889,#fff)", lightning: "linear-gradient(90deg,#222,#fff,#222)",
  "strobe-white": "repeating-linear-gradient(90deg,#fff 0 6px,#222 6px 12px)",
};
const rainbow = "linear-gradient(90deg,#f33,#fb3,#3f6,#3cf,#93f,#f3c)";

export function EffectsPage() {
  const t = useT();
  const { full, boot } = useShow();
  const { heads } = useLive();
  const [cat, setCat] = useState("all");
  const [query, setQuery] = useState("");
  const [local, setLocal] = useState<EffectSettings | null>(null);
  const [target, setTarget] = useState("");  // "" = all fixtures, else a group id
  const [editing, setEditing] = useState<CustomChase | null>(null);
  const chases = full!.state.chases ?? [];

  const groups = full!.groups;
  const layers = full!.state.layers;
  const layer = target ? layers.find((l) => l.groupId === target) : undefined;
  const server = layer?.effect ?? full!.state.effect;
  // follow server changes unless the user is mid-edit (local is cleared after each send)
  useEffect(() => setLocal(null), [server, target]);
  useEffect(() => { if (target && !groups.some((g) => g.id === target)) setTarget(""); }, [groups, target]);
  const fx = local ?? server;
  const running = target ? !!layer : full!.state.mode === "effect";
  const effects = boot!.effects;
  const screen = !!fx.screenSync;
  const chase = !screen && fx.chaseId ? chases.find((c) => c.id === fx.chaseId) : undefined;
  const current = chase || screen ? undefined : effects[fx.effectIndex];
  const uses = new Set<EffectParam>(current?.uses ?? []);

  const name = (e: EffectInfo) => t("fxname." + e.key, undefined, e.name);
  const list = useMemo(() => effects.filter((e) =>
    cat !== "own" && cat !== "screen" && (cat === "all" || categories[cat]?.includes(e.key)) &&
    (!query || name(e).toLowerCase().includes(query.toLowerCase()))), [effects, cat, query, full?.settings.language]);

  const send = (next: EffectSettings, start = running) => {
    setLocal(next);
    if (!target) api.effect(next, start);
    else if (start) api.startLayer(target, next);
  };
  const stop = () => (target ? api.stopLayer(target) : api.mode("static"));

  const choose = (e: EffectInfo) => {
    if (running && !chase && !screen && e.index === fx.effectIndex) stop();
    else send({ ...fx, effectIndex: e.index, chaseId: null, screenSync: false }, true);
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

  return (
    <div className="fx-layout">
      <div>
        <div className="page-head">
          <h1>{t("fx.title")}</h1>
          {groups.length > 0 && (
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
          {["all", "own", "screen", ...Object.keys(categories)].map((c) => (
            <span key={c} className={"chip" + (cat === c ? " active" : "")} onClick={() => setCat(c)}>{t("fxcat." + c)}</span>
          ))}
        </div>
        {cat === "own" && <p className="hint" style={{ marginBottom: 10 }}>{t("ch.intro")}</p>}
        <div className="fx-grid">
          {(cat === "own" || (cat === "all" && !query)) && (
            <button className="fx-card fx-new-chase" onClick={create}>
              <span className="name"><Icon name="plus" /> {t("ch.new")}</span>
              <span className="hint">{t("ch.newHint")}</span>
            </button>
          )}
          {(cat === "all" || cat === "screen") && !query && (
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
              <button key={c.id} className={"fx-card own" + (active ? " active" : "")} onClick={() => chooseChase(c)}>
                <span className="name">{c.name}</span>
                <span className="bar" style={{ ["--fxbar" as string]: bar }} />
                {active && <span className="badge">{t("fx.running")}</span>}
                <span className="fx-edit" role="button" title={t("ch.edit")} onClick={(e) => { e.stopPropagation(); setEditing(c); }}><Icon name="edit" /></span>
              </button>
            );
          })}
          {list.map((e) => {
            const active = running && !chase && !screen && e.index === fx.effectIndex;
            const bar = bars[e.key] ?? (e.key.includes("rainbow") || e.key === "disco" || e.key.startsWith("random-col") ? rainbow
              : e.uses.includes("ColorB") ? `linear-gradient(90deg, ${fx.colorA}, ${fx.colorB})`
              : e.uses.includes("ColorA") ? fx.colorA : rainbow);
            return (
              <button key={e.key} className={"fx-card" + (active ? " active" : "") + (!running && e.index === fx.effectIndex ? " " : "")} onClick={() => choose(e)}>
                <span className="name">{name(e)}</span>
                <span className="bar" style={{ ["--fxbar" as string]: bar }} />
                {active ? <span className="badge">{t("fx.running")}</span> : e.index >= 40 ? <span className="fx-new">{e.uses.includes("Audio") || e.uses.includes("Beat") ? "♪" : "NEW"}</span> : null}
              </button>
            );
          })}
        </div>
      </div>

      <div className="card palette-panel">
        <div className="card-head">
          <h2 className="grow">{screen ? t("fx.screen") : chase ? chase.name : current ? name(current) : ""}</h2>
          <Help k="fx.panelHelp" />
          {running
            ? <button className="btn off active" onClick={stop}><Icon name="stop" /> {t("fx.stop")}</button>
            : <button className="btn primary" onClick={() => send(fx, true)}><Icon name="play" /> {t("fx.start")}</button>}
        </div>
        {layers.length > 0 && (
          <div className="stack" style={{ gap: 6, marginBottom: 14 }}>
            <h3>{t("fx.layers")}</h3>
            {layers.map((l) => (
              <div key={l.groupId} className="layer-row">
                <b>{groups.find((g) => g.id === l.groupId)?.name ?? "?"}</b>
                <span className="grow muted">{layerName(l.effect)}</span>
                <button className="btn sm" onClick={() => setTarget(l.groupId)}>⋯</button>
                <button className="btn sm off" onClick={() => api.stopLayer(l.groupId)}>{t("fx.layerStop")}</button>
              </div>
            ))}
            <p className="hint">{t("fx.layerHint")}</p>
          </div>
        )}
        <h3 style={{ marginBottom: 8 }}>{t("fx.preview")}</h3>
        <div className="preview-strip">
          {(heads.length ? heads : ["#000000"]).map((h, i) => <div key={i} style={{ background: h }} />)}
        </div>
        {screen ? (
          <div style={{ marginBottom: 14 }}><ScreenSyncPanel /></div>
        ) : chase ? (
          <div className="stack" style={{ marginBottom: 14 }}>
            <p className="hint">{t("ch.panelHint")}</p>
            <button className="btn" onClick={() => setEditing(chase)}><Icon name="edit" /> {t("ch.edit")}</button>
          </div>
        ) : (
          <p className="hint" style={{ marginBottom: 14 }}>
            {usedList.length ? t("fx.uses", { list: usedList.join(", ") }) : t("fx.usesNone")}
          </p>
        )}

        {!chase && !screen && <div className="stack">
          <div className="row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
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
          <TimeParam label={t("fx.step")} value={fx.stepMs} min={40} max={5000} enabled={uses.has("Step")} onChange={(v) => send({ ...fx, stepMs: v })} />
          <TimeParam label={t("fx.on")} value={fx.onMs} min={20} max={10000} enabled={uses.has("OnOff")} onChange={(v) => send({ ...fx, onMs: v })} />
          <TimeParam label={t("fx.off")} value={fx.offMs} min={20} max={10000} enabled={uses.has("OnOff")} onChange={(v) => send({ ...fx, offMs: v })} />
          <TimeParam label={t("fx.delay")} value={fx.delayMs} min={0} max={10000} enabled={uses.has("Delay")} onChange={(v) => send({ ...fx, delayMs: v })} />
        </div>}
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
