import { useEffect, useState } from "react";
import { api, type ControlSettings, type MidiMapping } from "../api";
import { CompanionCard } from "../components/CompanionCard";
import { HomeAssistantCard } from "../components/HomeAssistantCard";
import { OscGuide } from "../components/OscGuide";
import { Help } from "../components/Help";
import { Icon } from "../components/Icon";
import { QrCode } from "../components/QrCode";
import { useT } from "../i18n";
import { updateSettings, useLive, useShow } from "../store";

const examples = ["scene 1", "flash green 5", "flash red 10 solid", "blackout", "color blue", "effect fire", "stop", "master 50", "tap", "timeline play"];

export function IntegrationsPage() {
  const t = useT();
  const { full } = useShow();
  const { control } = useLive();
  const settings = full!.settings;
  const c = settings.control;
  const [info, setInfo] = useState<{ commands: string[]; addresses: string[] }>({ commands: [], addresses: [] });
  const [devices, setDevices] = useState<string[]>([]);
  const [tryLine, setTryLine] = useState("flash green 3");
  const [result, setResult] = useState<string | null>(null);
  const [copied, setCopied] = useState<string | null>(null);
  const [preferred, setPreferred] = useState<string[]>([]);

  useEffect(() => {
    api.controlInfo().then(setInfo).catch(() => {});
    api.phone().then((ph) => setPreferred(ph.addresses.map((a) => a.ip))).catch(() => {});
    api.midiDevices().then(setDevices).catch(() => {});
  }, []);

  const save = (change: Partial<ControlSettings>) => updateSettings({ control: { ...c, ...change } });
  const port = settings.port;
  // Wi-Fi / Ethernet first (as the phone card), virtual adapters last
  const lan = (preferred.length ? preferred : info.addresses).filter((a) => !a.startsWith("127.") && !a.startsWith("169.254."));
  const urlFor = (line: string, host: string, withKey: boolean) =>
    `http://${host}:${port}/api/cmd/${line.split(" ").map(encodeURIComponent).join("/")}${withKey ? "?key=" + c.triggerKey : ""}`;
  const copy = (text: string) => { navigator.clipboard?.writeText(text); setCopied(text); setTimeout(() => setCopied(null), 1200); };
  const tryIt = async () => {
    const r = await fetch("api/cmd?c=" + encodeURIComponent(tryLine));
    setResult(r.ok ? "✓ ok" : "✗ " + (await r.text()));
  };

  // MIDI learn: when a message arrives, add a mapping for it
  useEffect(() => {
    const l = control?.midi.learned;
    if (!l || !learnFor) return;
    const mappings: MidiMapping[] = [...c.midiMappings, { id: Math.random().toString(16).slice(2, 10), type: l.type, channel: l.channel, number: l.number, command: learnFor }];
    setLearnFor(null);
    save({ midiMappings: mappings });
  }, [control?.midi.learned]);
  const [learnFor, setLearnFor] = useState<string | null>(null);
  const [newCommand, setNewCommand] = useState("scene 1");

  const sample = lan[0] ? urlFor("flash green 5", lan[0], true) : urlFor("flash green 5", "127.0.0.1", false);
  // phone automation: pick what should happen, get the link (and a QR code to try it)
  const [os, setOs] = useState<"iphone" | "android">(() => (/android/i.test(navigator.userAgent) ? "android" : "iphone"));
  const actions = [
    ...full!.scenes.map((s) => ({ label: t("int.act.scene", { name: s.name }), line: `scene "${s.name}"` })),
    { label: t("int.act.flash"), line: "flash green 5" },
    { label: t("int.act.flashRed"), line: "flash red 10" },
    { label: t("int.act.on"), line: "on" },
    { label: t("int.act.off"), line: "off" },
    { label: t("int.act.blackout"), line: "blackout" },
    { label: t("int.act.color"), line: "color blue" },
    { label: t("int.act.effect"), line: "effect rainbow-smooth" },
    { label: t("int.act.stop"), line: "stop" },
    { label: t("int.act.timeline"), line: "timeline play" },
  ];
  const [actLine, setActLine] = useState(actions[0]?.line ?? "flash green 5");
  const autoUrl = lan[0] ? urlFor(actLine, lan[0], true) : "";

  return (
    <div>
      <div className="page-head"><h1>{t("int.title")}</h1></div>
      <div className="settings-grid">
        <HomeAssistantCard />
        <CompanionCard />
        <div className="card stack">
          <h2><Icon name="link" /> {t("int.commands")}</h2>
          <p className="hint">{t("int.commandsHint")}</p>
          <div className="row" style={{ flexWrap: "nowrap" }}>
            <input className="input mono" style={{ flex: 1 }} value={tryLine} onChange={(e) => setTryLine(e.target.value)} onKeyDown={(e) => e.key === "Enter" && tryIt()} />
            <button className="btn primary" onClick={tryIt}>{t("int.try")}</button>
          </div>
          {result && <span className={result.startsWith("✓") ? "muted" : "warn-text"}>{result}</span>}
          <div className="chips">{examples.map((x) => <span key={x} className="chip mono" onClick={() => setTryLine(x)}>{x}</span>)}</div>
          <h3>{t("int.urls")}</h3>
          {!settings.allowNetwork && <div className="note"><Icon name="info" />{t("int.needNetwork")}</div>}
          <div className="url-list">
            {[...(settings.allowNetwork ? lan : []), "127.0.0.1"].map((host) => {
              const u = urlFor(tryLine || "scene 1", host, host !== "127.0.0.1");
              return (
                <div key={host} className="url-row">
                  <code>{u}</code>
                  <button className="btn sm ghost" onClick={() => copy(u)}>{copied === u ? "✓" : t("int.copy")}</button>
                </div>
              );
            })}
          </div>
          <div className="row">
            <span className="muted">{t("int.key")}</span><code>{c.triggerKey}</code>
            <button className="btn sm ghost" onClick={() => save({ triggerKey: Math.random().toString(36).slice(2, 12) })}>{t("int.newKey")}</button>
          </div>
        </div>

        <div className="card stack">
          <div className="row">
            <h2 style={{ flex: 1 }}><Icon name="chat" /> {t("int.phoneAuto")}</h2>
            <Help k="int.phoneAutoHelp" />
          </div>
          <p className="hint">{t("int.phoneAutoHint")}</p>
          {!settings.allowNetwork && <div className="note warn"><Icon name="warn" />{t("int.needNetwork")}</div>}
          <label className="field">{t("int.whatShould")}
            <select className="input" value={actLine} onChange={(e) => setActLine(e.target.value)}>
              {actions.map((a) => <option key={a.line} value={a.line}>{a.label}</option>)}
            </select>
          </label>
          {autoUrl && (
            <div className="phone-qr">
              <QrCode text={autoUrl} size={130} />
              <div className="stack" style={{ gap: 6, minWidth: 0 }}>
                <div className="url-row"><code className="wrap">{autoUrl}</code>
                  <button className="btn sm ghost" onClick={() => copy(autoUrl)}>{copied === autoUrl ? "✓" : t("int.copy")}</button></div>
                <p className="hint" style={{ margin: 0 }}>{t("int.qrTry")}</p>
              </div>
            </div>
          )}
          <div className="seg" style={{ alignSelf: "flex-start" }}>
            <button className={os === "iphone" ? "active" : ""} onClick={() => setOs("iphone")}>iPhone / iPad</button>
            <button className={os === "android" ? "active" : ""} onClick={() => setOs("android")}>Android</button>
          </div>
          {os === "android" ? (
            <>
              <h3>{t("int.androidTitle")}</h3>
              <ol className="steps">
                <li>{t("int.wa1")}</li>
                <li>{t("int.wa2")}</li>
                <li>{t("int.wa3")}</li>
                <li>{t("int.wa4")}</li>
                <li>{t("int.wa5")}</li>
              </ol>
              <p className="hint">{t("int.androidMore")}</p>
            </>
          ) : (
            <>
              <h3>{t("int.iosTitle")}</h3>
              <ol className="steps">{[1, 2, 3, 4, 5].map((i) => <li key={i}>{t("int.ios" + i)}</li>)}</ol>
              <h3>{t("int.iosAutoTitle")}</h3>
              <ol className="steps">{[1, 2, 3, 4].map((i) => <li key={i}>{t("int.iosAuto" + i)}</li>)}</ol>
              <p className="hint">{t("int.iosTriggers")}</p>
              <div className="note"><Icon name="info" /><span>{t("int.iosWhatsapp")}</span></div>
            </>
          )}
          <h3>IFTTT / Zapier / Home Assistant (REST)</h3>
          <p className="hint">{t("int.ha")}</p>
          <pre className="log">{`rest_command:\n  dmx_flash:\n    url: "${sample}"`}</pre>
        </div>

        <div className="card stack wide-card">
          <div className="row">
            <h2 style={{ flex: 1 }}>OSC</h2>
            <Help k="osc.help" />
            <label className="check"><input type="checkbox" checked={c.oscEnabled} onChange={(e) => save({ oscEnabled: e.target.checked })} /><span className="box" /></label>
          </div>
          <p className="hint">{t("int.oscHint")}</p>
          <label className="row" style={{ gap: 8 }}><span className="muted">{t("int.port")}</span>
            <input className="input num" type="number" min={1} max={65535} value={c.oscPort} onChange={(e) => save({ oscPort: +e.target.value || 8000 })} />
          </label>
          {c.oscEnabled && control && (
            <p className="hint">{control.osc.error ? <span className="warn-text">{control.osc.error}</span> : t("int.listening", { port: control.osc.port })}
              {control.osc.last && <> · {t("int.last")}: <code>{control.osc.last}</code></>}</p>
          )}
          <OscGuide port={c.oscPort} host={lan[0] ?? "127.0.0.1"} />
        </div>

        <div className="card stack">
          <div className="row">
            <h2 style={{ flex: 1 }}>MIDI</h2>
            <label className="check"><input type="checkbox" checked={c.midiEnabled} onChange={(e) => save({ midiEnabled: e.target.checked })} /><span className="box" /></label>
          </div>
          <label className="field">{t("int.midiDevice")}
            <select className="input" value={c.midiDevice ?? ""} onChange={(e) => save({ midiDevice: e.target.value || null })}>
              <option value="">{devices.length ? t("int.midiFirst") : t("int.midiNone")}</option>
              {devices.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </label>
          {c.midiEnabled && control?.midi.error && <div className="note warn"><Icon name="warn" />{control.midi.error}</div>}
          {control?.midi.last && <p className="hint">{t("int.last")}: {control.midi.last.type} ch{control.midi.last.channel} #{control.midi.last.number} = {control.midi.last.value}</p>}
          <table className="map-table">
            <tbody>
              {c.midiMappings.map((m) => (
                <tr key={m.id}>
                  <td className="mono">{m.type} {m.channel ? "ch" + m.channel : ""} #{m.number}</td>
                  <td><input className="input mono" style={{ width: "100%" }} defaultValue={m.command}
                    onBlur={(e) => save({ midiMappings: c.midiMappings.map((x) => (x.id === m.id ? { ...x, command: e.target.value } : x)) })} /></td>
                  <td><button className="btn sm ghost danger icon-btn" onClick={() => save({ midiMappings: c.midiMappings.filter((x) => x.id !== m.id) })}><Icon name="trash" /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row" style={{ flexWrap: "nowrap" }}>
            <input className="input mono" style={{ flex: 1 }} value={newCommand} onChange={(e) => setNewCommand(e.target.value)} />
            <button className={"btn" + (learnFor ? " primary" : "")} disabled={!c.midiEnabled}
              onClick={() => { setLearnFor(newCommand); api.midiLearn(); }}>
              {learnFor ? t("int.learning") : t("int.learn")}
            </button>
          </div>
          <p className="hint">{t("int.midiHint")}</p>
        </div>
      </div>
    </div>
  );
}
