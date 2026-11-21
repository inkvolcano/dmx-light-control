import { useEffect, useMemo, useRef, useState } from "react";
import { api, type ScheduleItem, type ScheduleSettings, type ScheduleTimeKind } from "../api";
import { Help } from "../components/Help";
import { Icon } from "../components/Icon";
import { NumberField } from "../components/NumberField";
import { useLang, useT } from "../i18n";
import { useShow } from "../store";

const CITIES: [string, number, number][] = [
  ["Amsterdam", 52.37, 4.9], ["Rotterdam", 51.92, 4.48], ["Den Haag", 52.08, 4.31], ["Utrecht", 52.09, 5.12],
  ["Eindhoven", 51.44, 5.47], ["Groningen", 53.22, 6.57], ["Zwolle", 52.52, 6.08], ["Maastricht", 50.85, 5.69],
  ["Middelburg", 51.5, 3.61], ["Leeuwarden", 53.2, 5.8], ["Brussel", 50.85, 4.35], ["Antwerpen", 51.22, 4.4], ["Gent", 51.05, 3.72],
];
const newId = () => Math.random().toString(16).slice(2, 10);

/** Planned actions: on days and times, or around sunrise/sunset. */
export function SchedulePage() {
  const t = useT();
  const lang = useLang();
  const { full, boot } = useShow();
  const server = full!.schedule;
  const [plan, setPlan] = useState<ScheduleSettings>(server);
  const pending = useRef(false);
  const seq = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [info, setInfo] = useState<Awaited<ReturnType<typeof api.scheduleInfo>> | null>(null);
  const [msg, setMsg] = useState<Record<string, string>>({});
  const [custom, setCustom] = useState<Record<string, boolean>>({});   // items where the user types an own command

  useEffect(() => { if (!pending.current) setPlan(server); }, [server]);
  useEffect(() => {
    let alive = true;
    const load = () => api.scheduleInfo().then((i) => alive && setInfo(i)).catch(() => {});
    load();
    const h = setInterval(load, 10000);
    return () => { alive = false; clearInterval(h); };
  }, [server]);

  const save = (next: ScheduleSettings) => {
    setPlan(next);
    pending.current = true;
    const n = ++seq.current;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(async () => {
      try { await api.saveSchedule(next); } catch { /* locked */ } finally { if (n === seq.current) pending.current = false; }
    }, 400);
  };
  const setItem = (id: string, c: Partial<ScheduleItem>) => save({ ...plan, items: plan.items.map((i) => (i.id === id ? { ...i, ...c } : i)) });
  const add = (i: Partial<ScheduleItem>) =>
    save({ ...plan, items: [...plan.items, { id: newId(), name: "", enabled: true, days: [], date: null, kind: "time", time: "18:00", offsetMinutes: 0, command: "on", ...i }] });
  const remove = (id: string) => save({ ...plan, items: plan.items.filter((i) => i.id !== id) });
  const test = async (i: ScheduleItem) => {
    const r = await api.runSchedule(i.id).catch((e) => ({ result: String(e) }));
    setMsg((m) => ({ ...m, [i.id]: r.result === "ok" ? "✓ " + t("sch.ran") : "✗ " + r.result }));
  };

  // ready-made actions; anything else can be typed as a command
  const actions = useMemo(() => {
    const q = (s: string) => `"${s}"`;
    const list: { label: string; cmd: string; group: string }[] = [
      { group: t("sch.grp.basic"), label: t("int.act.on"), cmd: "on" },
      { group: t("sch.grp.basic"), label: t("int.act.off"), cmd: "off" },
      { group: t("sch.grp.basic"), label: t("sch.act.blackoutOn"), cmd: "blackout on" },
      { group: t("sch.grp.basic"), label: t("sch.act.blackoutOff"), cmd: "blackout off" },
      { group: t("sch.grp.basic"), label: t("int.act.stop"), cmd: "stop" },
      { group: t("sch.grp.basic"), label: t("sch.act.master", { n: 30 }), cmd: "master 30" },
      { group: t("sch.grp.basic"), label: t("sch.act.master", { n: 100 }), cmd: "master 100" },
      { group: t("sch.grp.basic"), label: t("fx.screen"), cmd: "effect screen" },
      ...full!.scenes.map((s) => ({ group: t("scenes.title"), label: s.name, cmd: "scene " + q(s.name) })),
      ...full!.timelines.map((x) => ({ group: t("nav.timeline"), label: t("sch.act.tlPlay", { name: x.name }), cmd: "timeline play " + q(x.name) })),
      ...(full!.timelines.length ? [{ group: t("nav.timeline"), label: t("sch.act.tlStop"), cmd: "timeline stop" }] : []),
      ...(full!.state.chases ?? []).map((c) => ({ group: t("fxcat.own"), label: c.name, cmd: "effect " + q(c.name) })),
      ...boot!.effects.map((e) => ({ group: t("nav.effects"), label: t("fxname." + e.key, undefined, e.name), cmd: "effect " + e.key })),
      ...boot!.palette.fan.slice(0, 12).map((c) => ({ group: t("live.palette"), label: t("color." + c.key, undefined, c.name), cmd: "color " + c.key })),
    ];
    return list;
  }, [full, boot, lang]);
  const groups = [...new Set(actions.map((a) => a.group))];

  const days = [1, 2, 3, 4, 5, 6, 7];
  const dayName = (d: number) => t("sch.day." + d);
  const fmt = (iso: string) => new Date(iso).toLocaleString(lang === "en" ? "en-GB" : "nl-NL", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
  const hm = (iso: string | null) => (iso ? new Date(iso).toLocaleTimeString(lang === "en" ? "en-GB" : "nl-NL", { hour: "2-digit", minute: "2-digit" }) : "–");
  const city = CITIES.find(([, la, lo]) => Math.abs(la - plan.latitude) < 0.01 && Math.abs(lo - plan.longitude) < 0.01)?.[0] ?? "";

  return (
    <div>
      <div className="page-head">
        <h1>{t("sch.title")}</h1>
        <Help k="sch.help" />
        <div className="grow" />
        <label className="check">
          <input type="checkbox" checked={plan.enabled} onChange={(e) => save({ ...plan, enabled: e.target.checked })} />
          <span className="box" /> <b>{t("sch.enabled")}</b>
        </label>
      </div>
      {!plan.enabled && <div className="note warn" style={{ marginBottom: 12 }}><Icon name="warn" />{t("sch.disabledNote")}</div>}

      <div className="sch-layout">
        <div className="stack">
          {plan.items.length === 0 && (
            <div className="card stack">
              <p className="hint">{t("sch.empty")}</p>
            </div>
          )}
          {plan.items.map((i) => {
            const preset = actions.find((a) => a.cmd === i.command);
            return (
              <div key={i.id} className={"card stack sch-item" + (i.enabled ? "" : " off")}>
                <div className="row">
                  <label className="check" title={t("sch.itemOn")}>
                    <input type="checkbox" checked={i.enabled} onChange={(e) => setItem(i.id, { enabled: e.target.checked })} /><span className="box" />
                  </label>
                  <TextField className="input grow" placeholder={t("sch.namePlaceholder")} value={i.name} onCommit={(v) => setItem(i.id, { name: v })} />
                  <button className="btn sm" onClick={() => test(i)} title={t("sch.testHint")}><Icon name="play" /> {t("sch.test")}</button>
                  <button className="btn sm ghost danger icon-btn" onClick={() => remove(i.id)} title={t("scenes.delete")}><Icon name="trash" /></button>
                </div>

                <div className="row sch-when">
                  <div className="seg">
                    {(["time", "sunset", "sunrise"] as ScheduleTimeKind[]).map((k) => (
                      <button key={k} className={i.kind === k ? "active" : ""} onClick={() => setItem(i.id, { kind: k })}>{t("sch.kind." + k)}</button>
                    ))}
                  </div>
                  {i.kind === "time" ? (
                    <input className="input" type="time" value={i.time} onChange={(e) => setItem(i.id, { time: e.target.value })} />
                  ) : (
                    <label className="row" style={{ gap: 6 }}>
                      <NumberField min={-720} max={720} value={i.offsetMinutes} onCommit={(v) => setItem(i.id, { offsetMinutes: v ?? 0 })} />
                      <span className="muted">{t("sch.minutes")}</span>
                    </label>
                  )}
                </div>

                <div className="row sch-days">
                  <label className="check">
                    <input type="checkbox" checked={i.date != null}
                      onChange={(e) => setItem(i.id, { date: e.target.checked ? localToday() : null })} />
                    <span className="box" /> {t("sch.once")}
                  </label>
                  {i.date != null ? (
                    <input className="input" type="date" value={i.date} onChange={(e) => setItem(i.id, { date: e.target.value || null })} />
                  ) : (
                    <>
                      <div className="chips">
                        {days.map((d) => (
                          <span key={d} className={"chip" + (i.days.length === 0 || i.days.includes(d) ? " active" : "")}
                            onClick={() => {
                              const cur = i.days.length === 0 ? days : i.days;
                              const next = cur.includes(d) ? cur.filter((x) => x !== d) : [...cur, d].sort();
                              setItem(i.id, { days: next.length === 7 ? [] : next });
                            }}>{dayName(d)}</span>
                        ))}
                      </div>
                      <span className="muted small">{i.days.length === 0 ? t("sch.everyDay") : ""}</span>
                    </>
                  )}
                </div>

                <FadeInput seconds={i.fadeSeconds ?? 0} onChange={(v) => setItem(i.id, { fadeSeconds: v })} />

                <div className="row" style={{ flexWrap: "nowrap" }}>
                  <span className="muted">{t("sch.then")}</span>
                  <select className="input grow" value={preset && !custom[i.id] ? preset.cmd : "__custom"}
                    onChange={(e) => {
                      const own = e.target.value === "__custom";
                      setCustom((m) => ({ ...m, [i.id]: own }));
                      if (!own) setItem(i.id, { command: e.target.value });
                    }}>
                    {groups.map((g) => (
                      <optgroup key={g} label={g}>
                        {actions.filter((a) => a.group === g).map((a) => <option key={a.group + a.cmd} value={a.cmd}>{a.label}</option>)}
                      </optgroup>
                    ))}
                    <option value="__custom">{t("sch.custom")}</option>
                  </select>
                </div>
                {(!preset || custom[i.id]) && (
                  <CommandInput value={i.command} onChange={(v) => setItem(i.id, { command: v })} />
                )}
                {msg[i.id] && <p className="hint">{msg[i.id]}</p>}
              </div>
            );
          })}
          <div className="row">
            <button className="btn primary" onClick={() => add({})}><Icon name="plus" /> {t("sch.add")}</button>
            <span className="muted">{t("sch.examples")}</span>
            <button className="btn sm ghost" onClick={() => add({ name: t("sch.ex.sunsetName"), kind: "sunset", offsetMinutes: -15, command: "on" })}>{t("sch.ex.sunset")}</button>
            <button className="btn sm ghost" onClick={() => add({ name: t("sch.ex.nightName"), time: "23:30", command: "off" })}>{t("sch.ex.night")}</button>
            <button className="btn sm ghost" onClick={() => add({ name: t("sch.ex.workName"), time: "08:00", days: [1, 2, 3, 4, 5], command: "on" })}>{t("sch.ex.work")}</button>
            <button className="btn sm ghost" onClick={() => add({ name: t("sch.ex.wakeName"), time: "07:00", days: [1, 2, 3, 4, 5], command: "color soft-white", fadeSeconds: 1800 })}>{t("sch.ex.wake")}</button>
            <button className="btn sm ghost" onClick={() => add({ name: t("sch.ex.dimName"), time: "23:00", command: "off", fadeSeconds: 900 })}>{t("sch.ex.dim")}</button>
          </div>
        </div>

        <div className="stack">
          <div className="card stack">
            <h2><Icon name="clock" /> {t("sch.upcoming")}</h2>
            {!info?.upcoming.length && <p className="hint">{t("sch.nothing")}</p>}
            <ul className="sch-next">
              {info?.upcoming.map((u, k) => (
                <li key={k}><b>{fmt(u.at)}</b><span>{u.name || u.command}</span></li>
              ))}
            </ul>
            {info?.last && (
              <p className="hint">{t("sch.last", { when: fmt(info.last.at), what: info.last.name || info.last.command, result: info.last.result })}</p>
            )}
          </div>
          <div className="card stack">
            <h2>☀ {t("sch.sun")}</h2>
            <p>{t("sch.sunToday", { rise: hm(info?.sunrise ?? null), set: hm(info?.sunset ?? null) })}</p>
            <label className="field">{t("sch.place")}
              <select className="input" value={city} onChange={(e) => {
                const c = CITIES.find((x) => x[0] === e.target.value);
                if (c) save({ ...plan, latitude: c[1], longitude: c[2] });
              }}>
                <option value="">{t("sch.placeOther")}</option>
                {CITIES.map(([n]) => <option key={n} value={n}>{n}</option>)}
              </select>
            </label>
            <div className="row">
              <label className="field" style={{ flex: 1 }}>{t("sch.lat")}
                <NumberField className="input" step={0.01} min={-90} max={90} value={plan.latitude} onCommit={(v) => save({ ...plan, latitude: v ?? 0 })} /></label>
              <label className="field" style={{ flex: 1 }}>{t("sch.lon")}
                <NumberField className="input" step={0.01} min={-180} max={180} value={plan.longitude} onCommit={(v) => save({ ...plan, longitude: v ?? 0 })} /></label>
            </div>
            {"geolocation" in navigator && (
              <button className="btn sm ghost" style={{ alignSelf: "flex-start" }} onClick={() => navigator.geolocation.getCurrentPosition(
                (p) => save({ ...plan, latitude: Math.round(p.coords.latitude * 100) / 100, longitude: Math.round(p.coords.longitude * 100) / 100 }),
                () => {}, { timeout: 10000 })}>{t("sch.myPlace")}</button>
            )}
          </div>
          <div className="note"><Icon name="info" /><span>{t("sch.pcOn")}</span></div>
        </div>
      </div>
    </div>
  );
}

/** Today as yyyy-mm-dd in local time (toISOString would give the UTC date: yesterday after midnight). */
function localToday() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

/** Text box that reports on blur/Enter, so the server trimming a name never eats a space you are typing. */
function TextField({ value, onCommit, className, placeholder }: { value: string; onCommit: (v: string) => void; className?: string; placeholder?: string }) {
  const [v, setV] = useState(value);
  const [focus, setFocus] = useState(false);
  useEffect(() => { if (!focus) setV(value); }, [value, focus]);
  return (
    <input className={className} placeholder={placeholder} value={v} onFocus={() => setFocus(true)}
      onChange={(e) => setV(e.target.value)}
      onBlur={() => { setFocus(false); if (v !== value) onCommit(v); }}
      onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()} />
  );
}

/** Fade time: seconds or minutes ("0" = the action's own transition). */
function FadeInput({ seconds, onChange }: { seconds: number; onChange: (s: number) => void }) {
  const t = useT();
  const minutes = seconds >= 60 && seconds % 60 === 0;
  const [unit, setUnit] = useState<"s" | "m">(minutes ? "m" : "s");
  const value = unit === "m" ? Math.round(seconds / 60) : seconds;
  return (
    <div className="row" style={{ gap: 6 }}>
      <span className="muted">{t("sch.fade")}</span>
      <NumberField min={0} max={unit === "m" ? 240 : 14400} value={value} onCommit={(v) => onChange((v ?? 0) * (unit === "m" ? 60 : 1))} />
      <select className="input" value={unit} onChange={(e) => {
        const u = e.target.value as "s" | "m";
        setUnit(u);
        onChange(u === "m" ? value * 60 : value);
      }}>
        <option value="s">{t("sch.seconds")}</option>
        <option value="m">{t("sch.minutesUnit")}</option>
      </select>
      <span className="hint">{seconds > 0 ? t("sch.fadeHint") : t("sch.fadeNone")}</span>
    </div>
  );
}

/** Free command with a live check ("scene not found: ..."). */
function CommandInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const t = useT();
  const [v, setV] = useState(value);
  const [check, setCheck] = useState<string | null>(null);
  useEffect(() => setV(value), [value]);
  useEffect(() => {
    const h = setTimeout(() => api.checkCommand(v).then((r) => setCheck(r.ok)).catch(() => {}), 300);
    return () => clearTimeout(h);
  }, [v]);
  return (
    <div className="stack" style={{ gap: 4 }}>
      <input className={"input mono" + (check ? " invalid" : "")} value={v} placeholder='scene "Sfeer"'
        onChange={(e) => setV(e.target.value)} onBlur={() => v !== value && onChange(v)} onKeyDown={(e) => e.key === "Enter" && onChange(v)} />
      <span className={"small " + (check ? "warn-text" : "muted")}>{check ?? t("sch.commandOk")}</span>
    </div>
  );
}
