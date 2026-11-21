import { useEffect, useMemo, useState } from "react";
import { api, throttle, type ColorRef, type EffectSettings } from "../api";
import { contrast } from "../components/ColorPicker";
import { Icon } from "../components/Icon";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

// A short list of effects that look good without settings; own chases come on top.
const QUICK_FX = ["rainbow-smooth", "rainbow-chase", "color-cycle", "chase-right", "bounce", "breathe-wave", "fire", "police",
  "disco", "sparkle", "christmas", "audio-beat-colors"];

/**
 * Easy mode: big buttons for a phone or for someone who just wants to use the light. Also the locked
 * operator view (then "Ontgrendelen" instead of "Alles"). What it shows is set in Settings > Eenvoudige bediening.
 */
export function EasyView({ locked, onFull, onHelp }: { locked: boolean; onFull: () => void; onHelp: () => void }) {
  const t = useT();
  const { full, boot } = useShow();
  const { output, music, timeline, heads } = useLive();
  const s = full!.state;
  const easy = full!.settings.easy ?? { colors: true, effects: true, groups: true, timelines: true, music: true, strobe: true };
  const [target, setTarget] = useState("");   // "" = all lamps, else group id
  const [bright, setBright] = useState<number | null>(null);
  const sendBright = useMemo(() => throttle((v: number) => api.master({ brightness: v })), []);
  const [strobing, setStrobing] = useState(false);
  const strobe = (on: boolean) => { setStrobing(on); api.strobe({ active: on }); };
  const [toast, setToast] = useState<string | null>(null);
  const [moreColors, setMoreColors] = useState(false);
  useEffect(() => { if (!toast) return; const h = setTimeout(() => setToast(null), 1400); return () => clearTimeout(h); }, [toast]);

  const groups = full!.groups;
  const group = groups.find((g) => g.id === target);
  const ids = group ? group.fixtureIds : [];
  const layer = target ? s.layers.find((l) => l.groupId === target && !l.source) : undefined;
  const fx: EffectSettings = layer?.effect ?? s.effect;
  const effectRunning = target ? !!layer : s.mode === "effect";
  const chases = s.chases ?? [];
  const effects = boot!.effects;
  const quick = QUICK_FX.map((k) => effects.find((e) => e.key === k)).filter((e): e is NonNullable<typeof e> => !!e);
  const fxName = (key: string, name: string) => t("fxname." + key, undefined, name);
  const palette = [...boot!.palette.fan, ...boot!.palette.specials];
  const custom = s.customColors ?? [];

  const color = (c: ColorRef, label: string) => {
    // a colour should be visible: stop the effect that would paint over it
    if (target) { if (layer) api.stopLayer(target); }
    else if (s.layers.some((l) => !l.source)) api.stopLayer(null);
    api.looks(ids, true, c);
    setToast(label);
  };
  const startFx = (next: EffectSettings, label: string) => {
    if (target) api.startLayer(target, next); else api.effect(next, true);
    setToast(label);
  };
  const stopFx = () => (target ? api.stopLayer(target) : api.mode("static"));
  const isActiveFx = (index: number, chaseId?: string) =>
    effectRunning && !fx.screenSync && (chaseId ? fx.chaseId === chaseId : !fx.chaseId && fx.effectIndex === index);
  const speed = Math.round(100 - ((Math.log(Math.max(40, fx.stepMs)) - Math.log(40)) / (Math.log(2000) - Math.log(40))) * 100);
  const setSpeed = useMemo(() => throttle((p: number) => {
    const ms = Math.round(Math.exp(Math.log(40) + ((100 - p) / 100) * (Math.log(2000) - Math.log(40))) / 10) * 10;
    const next = { ...fx, stepMs: ms };
    if (target) api.startLayer(target, next); else api.effect(next, true);
  }, 150), [fx, target]);

  const tl = full!.timelines;
  const tlPlaying = timeline.playing ? tl.find((x) => x.id === timeline.id) : undefined;

  return (
    <div className="easy">
      <header className="easy-top">
        <div className="brand-dot" />
        <b className="easy-title">DmxLicht</b>
        <span className={"chip " + (output.connected ? "ok" : "warn")} title={output.connected ? "" : t("fault." + (output.fault ?? "Other"))}>
          <span className="dot" />{output.connected ? output.name : t("easy.noDongle")}
        </span>
        <div className="grow" />
        <button className="btn ghost icon-btn" onClick={onHelp} aria-label={t("help.button")}><Icon name="info" /></button>
        <button className="btn ghost" onClick={onFull}>{locked ? <><Icon name="lock" /> {t("op.unlock")}</> : <><Icon name="layout" /> {t("easy.full")}</>}</button>
      </header>

      {/* live preview of the lamps */}
      {heads.length > 0 && (
        <div className="easy-preview">{heads.slice(0, 64).map((h, i) => <span key={i} style={{ background: h }} />)}</div>
      )}

      <div className="easy-main">
        <button className="btn easy-big on" onClick={() => api.looks(ids, true)}><Icon name="power" size={24} />{t("top.on")}</button>
        <button className="btn easy-big off" onClick={() => api.looks(ids, false)}>{t("top.off")}</button>
        <button className={"btn easy-big blackout" + (s.blackout ? " active" : "")} onClick={() => api.master({ blackout: !s.blackout })}>
          <Icon name="moon" size={24} />{t("top.blackout")}
        </button>
        {easy.strobe && (
          <button className={"btn easy-big strobe" + (strobing ? " active" : "")}
            onPointerDown={(e) => { (e.target as Element).setPointerCapture?.(e.pointerId); strobe(true); }}
            onPointerUp={() => strobe(false)} onPointerCancel={() => strobe(false)} onContextMenu={(e) => e.preventDefault()}>
            <Icon name="bolt" size={24} />{t("top.strobe")}
          </button>
        )}
      </div>

      <div className="easy-slider card">
        <Icon name="bulb" />
        <input type="range" min={0} max={100} value={bright ?? s.brightnessPercent} aria-label={t("top.master")}
          onChange={(e) => { setBright(+e.target.value); sendBright(+e.target.value); }}
          onPointerUp={() => setBright(null)} onTouchEnd={() => setBright(null)} />
        <b>{bright ?? s.brightnessPercent}%</b>
      </div>

      {easy.groups && groups.length > 0 && (
        <div className="easy-targets">
          <span className={"chip" + (!target ? " active" : "")} onClick={() => setTarget("")}>{t("easy.all")}</span>
          {groups.map((g) => <span key={g.id} className={"chip" + (target === g.id ? " active" : "")} onClick={() => setTarget(g.id)}>{g.name}</span>)}
        </div>
      )}

      <section>
        <h3>{t("scenes.title")}</h3>
        {full!.scenes.length === 0 && <p className="hint">{t("op.noScenes")}</p>}
        <div className="easy-scenes">
          {full!.scenes.map((sc) => (
            <button key={sc.id} className={"easy-scene" + (s.activeScene === sc.id ? " active" : "")}
              style={{ background: sc.color, color: contrast(sc.color) }} onClick={() => { api.recallScene(sc.id); setToast(sc.name); }}>
              {sc.name}
            </button>
          ))}
        </div>
      </section>

      {easy.colors && (
        <section>
          <h3>{t("live.palette")}{group ? " · " + group.name : ""}</h3>
          <div className="easy-colors">
            {(moreColors ? palette : palette.slice(0, 12)).map((c) => (
              <button key={c.key} className="easy-color" style={{ background: c.hex, color: contrast(c.hex) }}
                onClick={() => color({ key: c.key }, t("color." + c.key, undefined, c.name))}>
                <span>{t("color." + c.key, undefined, c.name)}</span>
              </button>
            ))}
            {custom.map((h) => (
              <button key={h} className="easy-color" style={{ background: h, color: contrast(h) }} onClick={() => color({ hex: h }, h)}><span /></button>
            ))}
          </div>
          {palette.length > 12 && (
            <button className="btn ghost sm" style={{ marginTop: 8 }} onClick={() => setMoreColors(!moreColors)}>
              {moreColors ? t("easy.lessColors") : t("easy.moreColors", { n: palette.length - 12 })}
            </button>
          )}
        </section>
      )}

      {easy.effects && (
        <section>
          <div className="row">
            <h3 className="grow">{t("nav.effects")}{group ? " · " + group.name : ""}</h3>
            {effectRunning && <button className="btn sm off" onClick={stopFx}><Icon name="stop" /> {t("fx.stop")}</button>}
          </div>
          <div className="easy-fx">
            {chases.map((c) => {
              const cols = [...new Set(c.steps.flatMap((st) => st.colors.filter(Boolean) as string[]))].slice(0, 5);
              return (
                <button key={c.id} className={"easy-fx-btn" + (isActiveFx(-1, c.id) ? " active" : "")}
                  onClick={() => (isActiveFx(-1, c.id) ? stopFx() : startFx({ ...fx, chaseId: c.id, screenSync: false }, c.name))}>
                  <span>★ {c.name}</span>
                  <i style={{ background: cols.length > 1 ? `linear-gradient(90deg, ${cols.join(",")})` : cols[0] ?? "var(--line)" }} />
                </button>
              );
            })}
            <button className={"easy-fx-btn" + (effectRunning && fx.screenSync ? " active" : "")}
              onClick={() => (effectRunning && fx.screenSync ? stopFx() : startFx({ ...fx, screenSync: true, chaseId: null }, t("fx.screen")))}>
              <span>{t("fx.screen")}</span>
            </button>
            {quick.map((e) => (
              <button key={e.key} className={"easy-fx-btn" + (isActiveFx(e.index) ? " active" : "")}
                onClick={() => (isActiveFx(e.index) ? stopFx() : startFx({ ...fx, effectIndex: e.index, chaseId: null, screenSync: false }, fxName(e.key, e.name)))}>
                <span>{e.uses.includes("Audio") || e.uses.includes("Beat") ? "♪ " : ""}{fxName(e.key, e.name)}</span>
              </button>
            ))}
          </div>
          {effectRunning && !fx.chaseId && (
            <div className="easy-slider card">
              <span className="muted">{t("easy.slow")}</span>
              <input type="range" min={0} max={100} defaultValue={speed} key={fx.effectIndex + ":" + target} aria-label={t("easy.speed")}
                onChange={(e) => setSpeed(+e.target.value)} />
              <span className="muted">{t("easy.fast")}</span>
            </div>
          )}
        </section>
      )}

      {easy.music && (
        <section>
          <h3>{t("nav.music")}</h3>
          <div className="easy-music">
            <button className="btn easy-tap" onClick={() => api.tap()}>TAP</button>
            <div className="easy-bpm"><b>{music.bpm ? Math.round(music.bpm) : "–"}</b><span className="muted">BPM</span></div>
            <p className="hint">{t("easy.tapHint")}</p>
          </div>
        </section>
      )}

      {easy.timelines && tl.length > 0 && (
        <section>
          <h3>{t("nav.timeline")}</h3>
          <div className="stack" style={{ gap: 8 }}>
            {tl.map((x) => {
              const mine = timeline.id === x.id;
              return (
                <div key={x.id} className={"easy-tl card" + (mine && timeline.playing ? " playing" : "")}>
                  <b className="grow">{x.name}</b>
                  {mine && timeline.waiting != null && <button className="btn primary" onClick={() => api.goTimeline(x.id)}>GO</button>}
                  {mine && timeline.playing
                    ? <button className="btn" onClick={() => api.pauseTimeline()}><Icon name="pause" /></button>
                    : <button className="btn" onClick={() => api.playTimeline(x.id)}><Icon name="play" /></button>}
                  {mine && <button className="btn off" onClick={() => api.stopTimeline()}><Icon name="stop" /></button>}
                </div>
              );
            })}
          </div>
          {tlPlaying && <p className="hint">{t("easy.tlPlaying", { name: tlPlaying.name })}</p>}
        </section>
      )}

      {toast && <div className="easy-toast">{toast}</div>}
    </div>
  );
}
