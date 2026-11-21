import { useState } from "react";
import { api, type EffectInfo, type EffectSettings } from "../api";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";
import { ColorInput, contrast } from "./ColorPicker";
import { Icon } from "./Icon";

// colour hint per effect card
const bars: Record<string, string> = {
  "audio-beat-colors": "linear-gradient(90deg,#f33,#fb3,#3f6,#3cf,#93f)",
  "audio-beat-random": "linear-gradient(90deg,#3cf,#f3c,#fb3,#3f6)",
  "audio-beat-chase": "linear-gradient(90deg,#222 0 60%,#fb3 60% 80%,#222 80%)",
  "audio-spectrum": "linear-gradient(90deg,#f33,#3f6,#36f)",
  "audio-bands": "linear-gradient(90deg,#f33 0 33%,#3f6 33% 66%,#36f 66%)",
  "audio-pitch-color": "linear-gradient(90deg,#f33,#fb3,#3f6,#36f)",
  "audio-sparkle": "repeating-linear-gradient(90deg,#fff 0 3px,#223 3px 12px)",
  "audio-fire": "linear-gradient(90deg,#f40,#fa0,#f60)",
  "audio-surge-strobe": "repeating-linear-gradient(90deg,#fff 0 6px,#222 6px 12px)",
};

/** Sound-reactive effects: start on all fixtures or a group, colours A/B, live preview. */
export function SoundEffectsCard() {
  const t = useT();
  const { full, boot } = useShow();
  const { heads } = useLive();
  const [target, setTarget] = useState("");   // "" = all fixtures, else group id
  const s = full!.state;
  const groups = full!.groups;
  const effects = boot!.effects.filter((e) => e.uses.includes("Audio") || e.uses.includes("Beat"));
  const layer = target ? s.layers.find((l) => l.groupId === target && !l.source) : undefined;
  const current: EffectSettings = layer?.effect ?? s.effect;
  const runningFx = target ? layer?.effect : s.mode === "effect" ? s.effect : undefined;
  const runningIndex = runningFx && !runningFx.chaseId && !runningFx.screenSync ? runningFx.effectIndex : undefined;
  const runningInfo = effects.find((e) => e.index === runningIndex);
  const needsListen = !!runningInfo?.uses.includes("Audio") && !s.music.enabled;
  const name = (e: EffectInfo) => t("fxname." + e.key, undefined, e.name);

  const start = (next: EffectSettings) => (target ? api.startLayer(target, next) : api.effect(next, true));
  const choose = (e: EffectInfo) => {
    if (runningIndex === e.index) { if (target) api.stopLayer(target); else api.mode("static"); return; }
    start({ ...current, effectIndex: e.index, chaseId: null, screenSync: false });
  };
  const setColor = (k: "colorA" | "colorB", hex: string) => {
    const next = { ...current, [k]: hex };
    if (runningIndex !== undefined) start(next);
    else if (!target) api.effect(next, false);
  };

  return (
    <div className="card stack sound-fx">
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="sparkles" /> {t("sfx.title")}</h2>
        {groups.length > 0 && (
          <label className="row" style={{ gap: 8 }}>
            <span className="muted">{t("fx.target")}</span>
            <select className="input" value={target} onChange={(e) => setTarget(e.target.value)}>
              <option value="">{t("fx.allLamps")}</option>
              {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
            </select>
          </label>
        )}
        <span className="row" style={{ gap: 6, flexWrap: "nowrap" }}>
          A <span className="colorbtn" style={{ background: current.colorA, width: 44, height: 30 }}><ColorInput value={current.colorA} onPick={(h) => setColor("colorA", h)} /></span>
          B <span className="colorbtn" style={{ background: current.colorB, width: 44, height: 30 }}><ColorInput value={current.colorB} onPick={(h) => setColor("colorB", h)} /></span>
        </span>
      </div>
      <p className="hint">{t("sfx.hint")}</p>
      {needsListen && (
        <div className="note warn"><Icon name="warn" /><span>{t("sfx.needListen")}{" "}
          <button className="btn sm" onClick={() => api.music({ ...s.music, enabled: true })}>{t("music.listen")}</button></span></div>
      )}
      <div className="preview-strip">{(heads.length ? heads : ["#000000"]).map((h, i) => <div key={i} style={{ background: h }} />)}</div>
      <div className="fx-grid">
        {effects.map((e) => {
          const active = runningIndex === e.index;
          const usesAB = e.uses.includes("ColorB");
          const bar = bars[e.key] ?? (usesAB ? `linear-gradient(90deg, ${current.colorA}, ${current.colorB})` : current.colorA);
          return (
            <button key={e.key} className={"fx-card" + (active ? " active" : "")} onClick={() => choose(e)}>
              <span className="name">{name(e)}</span>
              <span className="bar" style={{ ["--fxbar" as string]: bar }} />
              <span className={"sfx-badge " + (e.uses.includes("Audio") ? "listen" : "beat")}>
                <Icon name={e.uses.includes("Audio") ? "mic" : "music"} size={12} /> {e.uses.includes("Audio") ? t("sfx.listen") : t("sfx.beat")}
              </span>
              {active && <span className="badge" style={{ background: "var(--accent)", color: contrast("#fcb24e") }}>{t("fx.running")}</span>}
            </button>
          );
        })}
      </div>
    </div>
  );
}
