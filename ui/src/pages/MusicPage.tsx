import { useEffect, useState } from "react";
import { api, type AudioModulation, type MusicSettings } from "../api";
import { Icon } from "../components/Icon";
import { SoundEffectsCard } from "../components/SoundEffectsCard";
import { useT } from "../i18n";
import { useLive, useShow } from "../store";

export function MusicPage() {
  const t = useT();
  const { full } = useShow();
  const { music: live } = useLive();
  const m = full!.state.music;
  const [devices, setDevices] = useState<{ id: string; name: string }[]>([]);
  const [bpmText, setBpmText] = useState<string | null>(null);

  useEffect(() => { api.audioDevices().then(setDevices).catch(() => {}); }, []);

  // space bar = tap while this page is open
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.code === "Space" && !(e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement)) {
        e.preventDefault();
        api.tap();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const save = (change: Partial<MusicSettings>) => api.music({ ...m, ...change });
  const devName = (d: { id: string; name: string }) =>
    d.id === "loopback" ? t("music.loopback") : d.id === "" ? t("music.defaultMic") : d.name;
  const beatInBar = Math.floor(live.beat) % 4;
  const phase = live.beat - Math.floor(live.beat);
  const lv = live.levels;
  const isMic = m.deviceId !== "loopback";

  return (
    <div>
      <div className="page-head">
        <h1>{t("music.title")}</h1>
        <span className={"chip " + (live.listening ? "ok" : "")}><span className="dot" />
          {live.listening ? (live.following ? t("music.following") : t("music.listening")) : t("music.off")}
        </span>
      </div>

      <SoundEffectsCard />
      <div className="settings-grid" style={{ marginTop: 14 }}>
        <div className="card stack">
          <div className="row">
            <h2 style={{ flex: 1 }}><Icon name="mic" /> {t("music.input")}</h2>
            <label className="check">
              <input type="checkbox" checked={m.enabled} onChange={(e) => save({ enabled: e.target.checked })} />
              <span className="box" /> {t("music.listen")}
            </label>
          </div>
          <label className="field">
            {t("music.device")}
            <select className="input" value={m.deviceId ?? ""} onChange={(e) => save({ deviceId: e.target.value })}>
              {devices.map((d) => <option key={d.id} value={d.id}>{devName(d)}</option>)}
            </select>
          </label>
          <p className="hint">{m.deviceId === "loopback" ? t("music.loopbackHint") : t("music.micHint")}</p>
          {live.error && (
            <div className="note warn"><Icon name="warn" />
              <span>{live.error}{isMic && <><br />{t("music.privacy")}</>}</span>
            </div>
          )}
          <div className="meters">
            {([["level", lv.level], ["bass", lv.bass], ["mid", lv.mid], ["high", lv.high]] as const).map(([k, v]) => (
              <div key={k} className="meter">
                <div className="meter-bar"><div className={"meter-fill " + k} style={{ height: Math.round(v * 100) + "%" }} /></div>
                <span>{t("music." + k)}</span>
              </div>
            ))}
          </div>
          <div className="param">
            <span className="lbl">{t("music.sensitivity")}</span>
            <input type="range" min={0.25} max={4} step={0.05} value={m.sensitivity} onChange={(e) => save({ sensitivity: +e.target.value })} />
            <span className="mono muted">{m.sensitivity.toFixed(2)}×</span>
          </div>
        </div>

        <div className="card stack">
          <h2>{t("music.tempo")}</h2>
          <div className="tempo">
            <div className="bpm">
              <input className="bpm-input" value={bpmText ?? live.bpm.toFixed(1)}
                onFocus={() => setBpmText(live.bpm.toFixed(1))}
                onChange={(e) => setBpmText(e.target.value)}
                onBlur={() => { const v = parseFloat((bpmText ?? "").replace(",", ".")); if (v >= 30 && v <= 300) api.bpm(v); setBpmText(null); }}
                onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
              <span>BPM</span>
            </div>
            <div className="beats">
              {[0, 1, 2, 3].map((i) => <div key={i} className={"beat-dot" + (i === beatInBar ? " on" : "")} style={i === beatInBar ? { opacity: 1 - phase * 0.7 } : undefined} />)}
            </div>
          </div>
          <div className="row">
            <button className="btn primary lg tap" onClick={() => api.tap()}>TAP</button>
            <button className="btn" onClick={() => api.bpm(live.bpm - 1)}>−1</button>
            <button className="btn" onClick={() => api.bpm(live.bpm + 1)}>+1</button>
            <button className="btn ghost" onClick={() => api.bpm(live.bpm / 2)}>½</button>
            <button className="btn ghost" onClick={() => api.bpm(live.bpm * 2)}>×2</button>
          </div>
          <p className="hint">{t("music.tapHint")}</p>
        </div>

        <div className="card stack">
          <div className="row">
            <h2 style={{ flex: 1 }}>{t("music.sync")}</h2>
            <label className="check">
              <input type="checkbox" checked={m.syncEffects} onChange={(e) => save({ syncEffects: e.target.checked })} />
              <span className="box" />
            </label>
          </div>
          <p className="hint">{t("music.syncHint")}</p>
          <div className="seg">
            {[0.5, 1, 2, 4].map((v) => (
              <button key={v} className={m.stepsPerBeat === v ? "active" : ""} onClick={() => save({ stepsPerBeat: v })} disabled={!m.syncEffects}>
                {t("music.perBeat", { n: v === 0.5 ? "½" : v })}
              </button>
            ))}
          </div>
        </div>

        <div className="card stack">
          <h2>{t("music.modulation")}</h2>
          <div className="seg">
            {(["none", "level", "bass", "beatPulse"] as AudioModulation[]).map((v) => (
              <button key={v} className={m.modulation === v ? "active" : ""} onClick={() => save({ modulation: v })}>{t("music.mod." + v)}</button>
            ))}
          </div>
          <p className="hint">{t("music.modHint." + m.modulation)}</p>
          {(m.modulation === "level" || m.modulation === "bass") && !m.enabled && (
            <div className="note"><Icon name="info" />{t("music.needListen")}</div>
          )}
          <div className={"param" + (m.modulation === "none" ? " disabled" : "")}>
            <span className="lbl">{t("music.floor")}</span>
            <input type="range" min={0} max={90} value={m.floorPercent} onChange={(e) => save({ floorPercent: +e.target.value })} />
            <span className="mono muted">{m.floorPercent}%</span>
          </div>
        </div>
      </div>
    </div>
  );
}
