import { useEffect, useState } from "react";
import { api, type MqttSettings } from "../api";
import { useT } from "../i18n";
import { updateSettings, useShow } from "../store";
import { Help } from "./Help";
import { Icon } from "./Icon";

const DEFAULTS: MqttSettings = { enabled: false, host: "", port: 1883, user: "", password: null, prefix: "dmxlicht", discovery: "homeassistant", name: "DMX Light Control" };

/** Home Assistant over MQTT: connection settings, status, steps and examples. */
export function HomeAssistantCard() {
  const t = useT();
  const { full } = useShow();
  const settings = full!.settings;
  const saved = { ...DEFAULTS, ...(settings.mqtt ?? {}) };
  const [d, setD] = useState<MqttSettings>(saved);
  const [password, setPassword] = useState("");
  const [status, setStatus] = useState<{ status: string; error: string | null } | null>(null);
  const [advanced, setAdvanced] = useState(false);
  const savedKey = JSON.stringify(saved);
  useEffect(() => setD(saved), [savedKey]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    let alive = true;
    const load = () => api.mqttStatus().then((s) => alive && setStatus(s)).catch(() => {});
    load();
    const h = setInterval(load, 2000);
    return () => { alive = false; clearInterval(h); };
  }, []);

  const save = (c: Partial<MqttSettings> = {}) => {
    const next = { ...d, ...c, password: password ? password : null };   // null = keep the saved password
    setD(next);
    setPassword("");
    updateSettings({ mqtt: next });
  };
  const chip = status?.status === "connected" ? "ok" : status?.status === "off" ? "" : "warn";
  const p = d.prefix || "dmxlicht";

  return (
    <div className="card stack">
      <div className="row">
        <h2 style={{ flex: 1 }}><Icon name="home" /> Home Assistant</h2>
        <span className={"chip " + chip}><span className="dot" />{t("ha.status." + (status?.status ?? "off"))}</span>
        <Help k="ha.help" />
      </div>
      <p className="hint">{t("ha.intro")}</p>
      <label className="check">
        <input type="checkbox" checked={d.enabled} onChange={(e) => save({ enabled: e.target.checked })} />
        <span className="box" /> <b>{t("ha.enable")}</b>
      </label>
      <div className="row" style={{ alignItems: "flex-end" }}>
        <label className="field" style={{ flex: 3, minWidth: 160 }}>{t("ha.host")}
          <input className="input" placeholder="homeassistant.local" value={d.host} onChange={(e) => setD({ ...d, host: e.target.value })} onBlur={() => save()} />
        </label>
        <label className="field" style={{ width: 90 }}>{t("int.port")}
          <input className="input num" type="number" value={d.port} onChange={(e) => setD({ ...d, port: +e.target.value || 1883 })} onBlur={() => save()} />
        </label>
      </div>
      <div className="row">
        <label className="field" style={{ flex: 1, minWidth: 140 }}>{t("ha.user")}
          <input className="input" autoComplete="off" value={d.user} onChange={(e) => setD({ ...d, user: e.target.value })} onBlur={() => save()} />
        </label>
        <label className="field" style={{ flex: 1, minWidth: 140 }}>{t("ha.password")}
          <input className="input" type="password" autoComplete="new-password" value={password}
            placeholder={settings.hasMqttPassword ? t("ha.passwordSaved") : ""}
            onChange={(e) => setPassword(e.target.value)} onBlur={() => password && save()} />
        </label>
      </div>
      {status?.error && <div className="note warn"><Icon name="warn" /><span>{status.error}</span></div>}
      <button className="btn sm ghost" style={{ alignSelf: "flex-start" }} onClick={() => setAdvanced(!advanced)}>{advanced ? "▾" : "▸"} {t("ha.advanced")}</button>
      {advanced && (
        <div className="row">
          <label className="field" style={{ flex: 1 }}>{t("ha.name")}
            <input className="input" value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} onBlur={() => save()} /></label>
          <label className="field" style={{ flex: 1 }}>{t("ha.prefix")}
            <input className="input mono" value={d.prefix} onChange={(e) => setD({ ...d, prefix: e.target.value })} onBlur={() => save()} /></label>
          <label className="field" style={{ flex: 1 }}>{t("ha.discovery")}
            <input className="input mono" value={d.discovery} onChange={(e) => setD({ ...d, discovery: e.target.value })} onBlur={() => save()} /></label>
        </div>
      )}

      <h3>{t("ha.stepsTitle")}</h3>
      <ol className="steps">{[1, 2, 3, 4, 5].map((i) => <li key={i}>{t("ha.step" + i)}</li>)}</ol>
      <h3>{t("ha.whatTitle")}</h3>
      <ul className="tips">{[1, 2, 3, 4, 5, 6].map((i) => <li key={i}>{t("ha.what" + i)}</li>)}</ul>
      <h3>{t("ha.exampleTitle")}</h3>
      <p className="hint">{t("ha.exampleHint")}</p>
      <pre className="log">{`# ${t("ha.ex1")}
action: select.select_option
target:
  entity_id: select.dmxlicht_scene
data:
  option: "Sfeer"

# ${t("ha.ex2")}
action: mqtt.publish
data:
  topic: ${p}/command
  payload: flash green 5

# ${t("ha.ex3")}
action: light.turn_on
target:
  entity_id: light.dmxlicht_light
data:
  rgb_color: [255, 120, 0]
  brightness_pct: 60`}</pre>
    </div>
  );
}
