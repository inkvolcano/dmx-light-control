import { useEffect, useRef, useState } from "react";
import { api, type EffectInfo } from "../api";
import { useT } from "../i18n";
import { onLinux, useLive, useShow } from "../store";
import { Icon } from "./Icon";

/** Opens a page by id (the setup behind a card). */
const go = (page: string) => window.dispatchEvent(new CustomEvent("dmx-goto", { detail: page }));

const search = () => window.dispatchEvent(new Event("dmx-search"));

/**
 * The column on the right of every page: search on top, then what the light listens to and looks at. The
 * settings behind each card (input, keystone, markers) open with the gear; during a show this is all you need.
 * What runs now sits in the top bar (<see NowButton>). Folded in, only search and the sound meters stay.
 */
export function LiveRail({ folded, onFold }: { folded: boolean; onFold: () => void; page?: string; selected?: string[] }) {
  const t = useT();
  return (
    <aside className={"rail" + (folded ? " folded" : "")}>
      <div className="rail-top">
        <button className="rail-fold" onClick={onFold} title={folded ? t("rail.unfold") : t("rail.fold")} aria-label={folded ? t("rail.unfold") : t("rail.fold")}>
          <Icon name={folded ? "chevron-left" : "chevron-right"} size={16} />
        </button>
        {folded
          ? <button className="rail-fold" onClick={search} title={t("cmd.title") + " (Ctrl+K)"} aria-label={t("cmd.title")}><Icon name="search" size={16} /></button>
          : <button className="btn rail-search" onClick={search} title={t("cmd.title") + " (Ctrl+K)"}>
              <Icon name="search" /> <span className="grow">{t("cmd.short")}</span> <kbd>Ctrl K</kbd>
            </button>}
      </div>
      {folded ? <Meters small /> : (
        <>
          <SoundCard />
          {!onLinux() && <ScreenCard />}
        </>
      )}
    </aside>
  );
}

/** "Nu actief" in the top bar: what runs now in one line; a click opens the full list (stop buttons included). */
export function NowButton() {
  const t = useT();
  const { full, boot } = useShow();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false); };
    const esc = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    window.addEventListener("pointerdown", away);
    window.addEventListener("keydown", esc);
    return () => { window.removeEventListener("pointerdown", away); window.removeEventListener("keydown", esc); };
  }, [open]);
  if (!full || !boot) return null;
  const s = full.state;
  const fx = s.effect;
  const chase = fx.chaseId ? (s.chases ?? []).find((c) => c.id === fx.chaseId) : undefined;
  const info = boot.effects[fx.effectIndex];
  const main = s.mode !== "effect" ? null
    : fx.screenSync ? t("rail.screen")
    : chase ? chase.name
    : info ? t("fxname." + info.key, undefined, info.name) : null;
  const scene = full.scenes.find((x) => x.id === s.activeScene);
  const more = s.layers.filter((l) => !l.source).length;
  const text = s.blackout ? t("rail.blackout") : main ?? (scene ? scene.name : s.mode === "faders" ? t("rail.faders") : t("now.static"));
  return (
    <div className="now-wrap" ref={ref}>
      <button className={"btn ghost now-btn" + (main || more ? " running" : "") + (open ? " active" : "")} onClick={() => setOpen(!open)}
        title={t("rail.now")} aria-expanded={open}>
        <Icon name="sparkles" size={16} />
        <span className="now-label">{t("rail.now")}:</span>
        <span className="now-text">{text}</span>
        {more > 0 && <span className="chip now-more">+{more}</span>}
      </button>
      {open && <div className="now-pop"><NowCard /></div>}
    </div>
  );
}

function Meters({ small }: { small?: boolean }) {
  const t = useT();
  const lv = useLive().music.levels;
  return (
    <div className={"rail-meters" + (small ? " small" : "")}>
      {([["bass", lv.bass], ["mid", lv.mid], ["high", lv.high]] as const).map(([k, v]) => (
        <div key={k} className="rail-meter" title={t("music." + k)}>
          <div className="meter-bar"><div className={"meter-fill " + k} style={{ height: Math.round(Math.min(1, v) * 100) + "%" }} /></div>
          {!small && <span>{t("music." + k)}</span>}
        </div>
      ))}
    </div>
  );
}

function SoundCard() {
  const t = useT();
  const { full } = useShow();
  const { music: live } = useLive();
  const m = full!.state.music;
  const beatInBar = Math.floor(live.beat) % 4;
  return (
    <section className="rail-card">
      <div className="rail-head">
        <Icon name="music" size={16} />
        <b>{t("rail.sound")}</b>
        <div className="grow" />
        <button className="btn sm ghost icon-btn" onClick={() => go("music")} title={t("rail.soundSetup")} aria-label={t("rail.soundSetup")}><Icon name="gear" size={16} /></button>
      </div>
      <label className="check rail-listen">
        <input type="checkbox" checked={m.enabled} onChange={(e) => api.music({ ...m, enabled: e.target.checked })} />
        <span className="box" />
        <span className={live.listening ? "ok-text" : "muted"}>
          {live.listening ? (live.following ? t("music.following") : t("music.listening")) : t("music.listen")}
        </span>
      </label>
      {live.error && m.enabled && <p className="hint warn-text">{live.error}</p>}
      <Meters />
      <label className="check rail-listen" title={t("rail.beatHint")}>
        <input type="checkbox" checked={m.syncEffects} onChange={(e) => api.music({ ...m, syncEffects: e.target.checked })} />
        <span className="box" />
        <span className={m.syncEffects ? "ok-text" : "muted"}>{t("rail.beatSwitch")}</span>
      </label>
      <div className={"rail-tempo" + (m.syncEffects || m.enabled ? "" : " dim")}>
        <span className="rail-bpm">{Math.round(live.bpm)}<small> BPM</small></span>
        <div className="beats">
          {[0, 1, 2, 3].map((i) => <div key={i} className={"beat-dot" + (i === beatInBar ? " on" : "")} />)}
        </div>
      </div>
      <div className="row rail-row">
        <button className="btn primary tap grow" onClick={() => api.tap()}>TAP</button>
        <button className="btn ghost sm" onClick={() => api.bpm(live.bpm / 2)}>½</button>
        <button className="btn ghost sm" onClick={() => api.bpm(live.bpm * 2)}>×2</button>
      </div>
    </section>
  );
}

function ScreenCard() {
  const t = useT();
  const { full } = useShow();
  const { heads } = useLive();
  const s = full!.state;
  const set = s.screenSync;
  const camera = set?.source === "camera";
  const running = s.mode === "effect" && !!s.effect.screenSync;
  const start = () => api.effect({ ...s.effect, screenSync: true, chaseId: null }, true);
  const stop = () => api.mode("static");
  return (
    <section className="rail-card">
      <div className="rail-head">
        <Icon name="monitor" size={16} />
        <b>{t("rail.screen")}</b>
        <div className="grow" />
        <button className="btn sm ghost icon-btn" onClick={() => go("screen")} title={t("rail.screenSetup")} aria-label={t("rail.screenSetup")}><Icon name="gear" size={16} /></button>
      </div>
      <div className="preview-strip rail-strip">
        {(running && heads.length ? heads : ["#000000"]).map((h, i) => <div key={i} style={{ background: h }} />)}
      </div>
      <div className="seg rail-seg">
        <button className={!camera ? "active" : ""} onClick={() => set && api.screenSync({ ...set, source: "screen" })}>{t("cam.fromScreen")}</button>
        <button className={camera ? "active" : ""} onClick={() => set && api.screenSync({ ...set, source: "camera" })}>{t("cam.fromCamera")}</button>
      </div>
      {running
        ? <button className="btn off active" onClick={stop}><Icon name="stop" /> {t("fx.stop")}</button>
        : <button className="btn" onClick={start}><Icon name="play" /> {t("ss.start")}</button>}
    </section>
  );
}

export function NowCard() {
  const t = useT();
  const { full, boot } = useShow();
  const s = full!.state;
  const name = (e: EffectInfo) => t("fxname." + e.key, undefined, e.name);
  const fx = s.effect;
  const chase = fx.chaseId ? (s.chases ?? []).find((c) => c.id === fx.chaseId) : undefined;
  const info = boot!.effects[fx.effectIndex];
  const main = s.mode !== "effect" ? null
    : fx.screenSync ? t("rail.screen")
    : chase ? chase.name
    : info ? name(info) : null;
  const scene = full!.scenes.find((x) => x.id === s.activeScene);
  const groups = full!.groups;
  const layers = s.layers.filter((l) => !l.source);
  const layerName = (fxIndex: number) => { const e = boot!.effects[fxIndex]; return e ? name(e) : "?"; };
  const whoName = (id: string) => id.startsWith("scene:")
    ? t("rail.sceneOnSelection", { name: full!.scenes.find((x) => "scene:" + x.id === id)?.name ?? "?" })
    : id.startsWith("sel:") ? t("rail.selectionLayer", { n: layers.find((l) => l.groupId === id)?.fixtureIds?.length ?? 0 })
    : groups.find((g) => g.id === id)?.name ?? "?";
  return (
    <section className="rail-card">
      <div className="rail-head">
        <Icon name="sparkles" size={16} />
        <b>{t("rail.now")}</b>
        <div className="grow" />
        <button className="btn sm ghost icon-btn" onClick={() => go("effects")} title={t("nav.effects")} aria-label={t("nav.effects")}><Icon name="chevron-right" size={16} /></button>
      </div>
      {s.blackout && <div className="note warn"><Icon name="moon" /> {t("rail.blackout")}</div>}
      {(s.held?.length ?? 0) > 0 && s.mode === "effect" && <p className="hint">{t("rail.held", { n: s.held!.length })}</p>}
      {main
        ? <div className="rail-now"><span className="grow">{main}</span>
            <button className="btn sm off" title={t("rail.stopHint")} onClick={() => api.mode("static")}><Icon name="stop" /> {t("fx.stop")}</button></div>
        : <p className="hint">{scene ? t("rail.scene", { name: scene.name }) : s.mode === "faders" ? t("rail.faders") : t("rail.static")}</p>}
      {layers.map((l) => (
        <div key={l.groupId} className="rail-now small">
          <span className="grow">{whoName(l.groupId)}: {l.effect.chaseId ? t("rail.chase") : layerName(l.effect.effectIndex)}</span>
          <button className="btn sm ghost" onClick={() => api.stopLayer(l.groupId)} aria-label={t("fx.stop")}><Icon name="stop" size={14} /></button>
        </div>
      ))}
      {(s.music.syncEffects || s.music.modulation !== "none") && (
        <p className="hint">{[s.music.syncEffects ? t("rail.onBeat") : "", s.music.modulation !== "none" ? t("music.mod." + s.music.modulation) : ""].filter(Boolean).join(" · ")}</p>
      )}
    </section>
  );
}
