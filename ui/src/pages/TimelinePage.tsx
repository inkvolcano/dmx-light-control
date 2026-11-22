import { useEffect, useRef, useState, type PointerEvent as RPointerEvent } from "react";
import { api, type EffectInfo, type EffectSettings, type SceneInfo, type Timeline, type TimelineClip, type TimelineTrack } from "../api";
import { ColorInput, contrast } from "../components/ColorPicker";
import { Icon } from "../components/Icon";
import { LiveTools, type DragItem, type DragStart } from "../components/LiveTools";
import { useT } from "../i18n";
import { onLinux, useLive, useShow } from "../store";
import { toast } from "../components/Toast";

const uid = () => Math.random().toString(16).slice(2, 10);
const fmt = (ms: number) => {
  const s = Math.max(0, ms) / 1000;
  return `${Math.floor(s / 60)}:${(s % 60).toFixed(1).padStart(4, "0")}`;
};

type Drag =
  | { kind: "move"; clipId: string; row: string; startX: number; origStart: number; moved: boolean }
  | { kind: "resize-l" | "resize-r"; clipId: string; startX: number; origStart: number; origLen: number }
  | { kind: "palette"; item: DragItem; x: number; y: number }
  | { kind: "seek" }
  | { kind: "flag"; which: "start" | "end" }
  | { kind: "marker"; id: string; startX: number; orig: number; moved: boolean };

/**
 * A row of the editor: all lamps on top, then one per lamp of the patch. Effect rows on a group (older
 * timelines) are shown under them so nothing gets lost. `key` is what the lanes carry in data-track.
 */
type Row = { key: string; name: string; kind: "all" | "lamp" | "group"; target: string; track?: TimelineTrack };
const ROW_H = 30; // px per line of clips in a row (overlapping clips go on a line of their own)

/**
 * Older timelines had a scene row and effect rows for all lamps: those become the one "all lamps" row
 * (which then holds scenes, effects and colours), always the first row.
 */
function normalize(tl: Timeline): Timeline {
  const isAll = (k: TimelineTrack) => k.kind === "all" || k.kind === "scenes" || (k.kind === "effects" && (k.target ?? "*") === "*");
  const merged = tl.tracks.filter(isAll);
  const main: TimelineTrack = merged.find((k) => k.kind === "all") ?? { id: uid(), name: "", kind: "all", target: "*", clips: [] };
  const clips = merged.flatMap((k) => k.clips).sort((a, b) => a.startMs - b.startMs);
  return { ...tl, tracks: [{ ...main, kind: "all", target: "*", clips }, ...tl.tracks.filter((k) => !isAll(k))] };
}

/** Puts overlapping clips of a row on separate lines: clip id -> line, and the number of lines. */
function lines(clips: TimelineClip[]): { line: Map<string, number>; count: number } {
  const ends: number[] = [];
  const line = new Map<string, number>();
  for (const c of [...clips].sort((a, b) => a.startMs - b.startMs || a.id.localeCompare(b.id))) {
    let i = ends.findIndex((e) => e <= c.startMs + 0.5);
    if (i < 0) { i = ends.length; ends.push(0); }
    ends[i] = c.startMs + c.lengthMs;
    line.set(c.id, i);
  }
  return { line, count: Math.max(1, ends.length) };
}

/** Starts dragging a colour, effect or preset from the tools towards the timeline (wherever it is shown). */
export const startTimelineDrag: DragStart = (item, e) => {
  e.preventDefault();
  window.dispatchEvent(new CustomEvent("dmx-tl-drag", { detail: { item, x: e.clientX, y: e.clientY } }));
};

/** The timeline page (old links): the editor with the tools under it. */
export function TimelinePage() {
  return (
    <div className="stack" style={{ gap: 16 }}>
      <TimelineEditor />
      <LiveTools selected={[]} onDrag={startTimelineDrag} storageKey="tlTab" />
    </div>
  );
}

/**
 * The timeline: transport, tracks and clips. Colours, effects and presets are dragged in from the tools
 * (Kleuren / Effecten / Presets) shown under it; `embedded` = inside Bediening (no page title).
 */
export function TimelineEditor({ embedded = false }: { embedded?: boolean }) {
  const t = useT();
  const { full, boot } = useShow();
  const { timeline: live } = useLive();
  const server = full!.timelines;
  const [currentId, setCurrentId] = useState<string | null>(() => server[0]?.id ?? null);
  const [tl, setTl] = useState<Timeline | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [pxPerSec, setPxPerSec] = useState(40);
  const [peaks, setPeaks] = useState<number[] | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const [uploading, setUploading] = useState<string | null>(null);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef(false);
  const saveSeq = useRef(0);
  const lanesRef = useRef<HTMLDivElement>(null);

  // follow the server copy unless we have unsaved edits
  useEffect(() => {
    if (!currentId && server[0]) setCurrentId(server[0].id);
    const s = server.find((x) => x.id === currentId) ?? null;
    if (!pending.current) setTl(s ? normalize(structuredClone(s)) : null);
  }, [server, currentId]);

  useEffect(() => {
    setPeaks(null);
    if (!tl?.audioFile) return;
    fetch("api/audio/peaks?file=" + encodeURIComponent(tl.audioFile)).then((r) => r.json()).then((d) => setPeaks(d.peaks)).catch(() => {});
  }, [tl?.audioFile]);

  const save = (next: Timeline, now = false) => {
    setTl(next);
    pending.current = true;
    if (saveTimer.current) clearTimeout(saveTimer.current);
    const seq = ++saveSeq.current;
    saveTimer.current = setTimeout(async () => {
      saveTimer.current = null;
      try { await api.saveTimeline(next); }
      catch { /* e.g. locked: follow the server again */ }
      // only the newest edit releases "pending": an older save finishing must not drop newer edits
      finally { if (seq === saveSeq.current) pending.current = false; }
    }, now ? 0 : 350);
  };

  const create = async () => {
    const n: Timeline = {
      id: uid(), name: t("tl.new", { n: server.length + 1 }), lengthMs: 60000, bpm: 120, snap: true, loop: false, audioFile: null,
      tracks: [{ id: uid(), name: "", kind: "all", target: "*", clips: [] }],
    };
    await api.saveTimeline(n);
    setCurrentId(n.id);
  };

  const scenes = full!.scenes;
  const effects = boot!.effects;
  const fxName = (e?: EffectInfo) => (e ? t("fxname." + e.key, undefined, e.name) : "?");
  const pxPerMs = pxPerSec / 1000;
  const beatMs = tl ? 60000 / tl.bpm : 500;
  const snap = (ms: number) => (tl?.snap ? Math.round(ms / beatMs) * beatMs : Math.round(ms / 10) * 10);
  const isMine = live.id && tl && live.id === tl.id;
  const playhead = isMine ? live.positionMs : tl?.startMs ?? 0;
  const waiting = isMine && live.waiting != null;
  const markers = tl?.markers ?? [];
  const setMarkers = (m: typeof markers) => tl && save({ ...tl, markers: m.sort((a, b) => a.atMs - b.atMs) });

  // keyboard: space = play/pause, Enter = GO, Home = stop, arrows = previous/next point
  useEffect(() => {
    if (!tl) return;
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement;
      if (el instanceof HTMLInputElement || el instanceof HTMLSelectElement || el instanceof HTMLTextAreaElement || e.ctrlKey || e.metaKey) return;
      if (e.code === "Space") { e.preventDefault(); if (isMine && live.playing) api.pauseTimeline(); else api.playTimeline(tl.id); }
      else if (e.key === "Enter") { e.preventDefault(); api.goTimeline(tl.id); }
      else if (e.key === "Home") { e.preventDefault(); api.stopTimeline(); }
      else if (e.key === "ArrowLeft") { e.preventDefault(); api.prevTimeline(tl.id); }
      else if (e.key === "ArrowRight") { e.preventDefault(); api.nextTimeline(tl.id); }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [tl?.id, isMine, live.playing]);
  const findClip = (id: string | null) => tl?.tracks.flatMap((k) => k.clips.map((c) => ({ track: k, clip: c }))).find((x) => x.clip.id === id);
  const sel = findClip(selected);

  // the rows: all lamps, every lamp of the patch, then group rows of older timelines
  const groups = full!.groups;
  const rows: Row[] = !tl ? [] : [
    { key: "all", name: t("fx.allLamps"), kind: "all", target: "*", track: tl.tracks.find((k) => k.kind === "all") },
    ...full!.patch.map((f): Row => ({ key: "lamp:" + f.id, name: f.name, kind: "lamp", target: f.id, track: tl.tracks.find((k) => k.kind === "lamp" && k.target === f.id) })),
    ...tl.tracks.filter((k) => k.kind === "effects" && k.target !== "*")
      .map((k): Row => ({ key: "track:" + k.id, name: groups.find((g) => g.id === k.target)?.name ?? k.name, kind: "group", target: k.target, track: k })),
  ];
  const rowOf = (track: TimelineTrack) => rows.find((r) => r.track?.id === track.id);
  /** The rows a preset lands on: those of the lamps it was saved for, else "all lamps". */
  const sceneRows = (scene: SceneInfo) => {
    const lamps = scene.lamps?.filter((id) => full!.patch.some((f) => f.id === id));
    return lamps?.length ? rows.filter((r) => r.kind === "lamp" && lamps.includes(r.target)) : rows.filter((r) => r.kind === "all");
  };
  /** The track of a row, made when the row gets its first clip. */
  const trackFor = (next: Timeline, row: Row): [Timeline, string] => {
    const have = next.tracks.find((k) => (row.track && k.id === row.track.id) || (row.kind === "lamp" && k.kind === "lamp" && k.target === row.target) || (row.kind === "all" && k.kind === "all"));
    if (have) return [next, have.id];
    const k: TimelineTrack = { id: uid(), name: row.name, kind: row.kind === "lamp" ? "lamp" : "all", target: row.target, clips: [] };
    return [{ ...next, tracks: row.kind === "all" ? [k, ...next.tracks] : [...next.tracks, k] }, k.id];
  };
  /** A lamp row whose last clip went is no longer stored. */
  const tidy = (next: Timeline): Timeline => ({ ...next, tracks: next.tracks.filter((k) => k.kind !== "lamp" || k.clips.length > 0) });

  const updateClip = (clipId: string, change: Partial<TimelineClip>, toRow?: Row) => {
    if (!tl) return;
    const found = findClip(clipId);
    if (!found) return;
    const clip = { ...found.clip, ...change };
    let next = tl, target = found.track.id;
    if (toRow) [next, target] = trackFor(tl, toRow);
    const tracks = next.tracks.map((k) => {
      let clips = k.clips.filter((c) => c.id !== clipId);
      if (k.id === target) clips = [...clips, clip].sort((a, b) => a.startMs - b.startMs);
      return { ...k, clips };
    });
    save(tidy({ ...next, tracks }));
  };

  const solidIndex = effects.findIndex((e) => e.key === "all-ab");
  const addClip = (rowKey: string, startMs: number, item: Extract<Drag, { kind: "palette" }>["item"]) => {
    if (!tl) return;
    const len = beatMs * 8;
    // a preset lands on the rows of its own lamps: "all lamps", or each lamp it was saved for
    if (item.type === "scene") {
      const targets = sceneRows(item.scene);
      let next = tl, first: string | null = null;
      for (const row of targets) {
        const clip: TimelineClip = { id: uid(), startMs, lengthMs: len, kind: "scene", sceneId: item.scene.id, effect: null, fadeMs: -1 };
        let trackId: string;
        [next, trackId] = trackFor(next, row);
        next = { ...next, tracks: next.tracks.map((k) => (k.id === trackId ? { ...k, clips: [...k.clips, clip].sort((a, b) => a.startMs - b.startMs) } : k)) };
        first ??= clip.id;
      }
      if (!first) return;
      save({ ...next, lengthMs: Math.max(next.lengthMs, startMs + len) });
      setSelected(first);
      return;
    }
    const row = rows.find((r) => r.key === rowKey);
    if (!row) return;
    let clip: TimelineClip | null = null;
    if (item.type === "effect")
      clip = { id: uid(), startMs, lengthMs: len, kind: "effect", sceneId: null, effect: { ...full!.state.effect, effectIndex: item.fx.index, chaseId: item.chase?.id ?? null, screenSync: false }, fadeMs: -1 };
    // a colour = "all lamps in colour A" with A and B the same: the lamps of the row simply take that colour
    if (item.type === "color" && solidIndex >= 0)
      clip = { id: uid(), startMs, lengthMs: len, kind: "effect", sceneId: null,
        effect: { ...full!.state.effect, effectIndex: solidIndex, chaseId: null, screenSync: false, colorA: item.hex, colorB: item.hex, colorPerLamp: false }, fadeMs: -1 };
    if (!clip) return;
    const [next, trackId] = trackFor(tl, row);
    const lengthMs = Math.max(next.lengthMs, startMs + len);
    save({ ...next, lengthMs, tracks: next.tracks.map((k) => (k.id === trackId ? { ...k, clips: [...k.clips, clip!].sort((a, b) => a.startMs - b.startMs) } : k)) });
    setSelected(clip.id);
  };

  // ---------- pointer handling (one drag at a time) ----------
  const laneX = (clientX: number) => {
    const el = lanesRef.current!;
    return clientX - el.getBoundingClientRect().left + el.scrollLeft - 160; // 160 = track header width
  };
  const trackAt = (clientX: number, clientY: number) =>
    (document.elementsFromPoint(clientX, clientY).find((e) => (e as HTMLElement).dataset?.track) as HTMLElement | undefined)?.dataset.track;

  useEffect(() => {
    if (!drag) return;
    const move = (e: PointerEvent) => {
      if (!tl) return;
      if (drag.kind === "palette") setDrag({ ...drag, x: e.clientX, y: e.clientY });
      else if (drag.kind === "seek") api.seekTimeline(Math.max(0, laneX(e.clientX) / pxPerMs));
      else if (drag.kind === "flag") {
        const ms = Math.max(0, Math.min(tl.lengthMs, snap(laneX(e.clientX) / pxPerMs)));
        if (drag.which === "start") save({ ...tl, startMs: Math.min(ms, (tl.endMs ?? tl.lengthMs) - beatMs / 2) });
        else save({ ...tl, endMs: Math.max(ms, (tl.startMs ?? 0) + beatMs / 2) });
      }
      else if (drag.kind === "marker") {
        if (!drag.moved && Math.abs(e.clientX - drag.startX) < 4) return;
        drag.moved = true;
        const at = Math.max(0, Math.min(tl.lengthMs, snap(drag.orig + (e.clientX - drag.startX) / pxPerMs)));
        setMarkers(markers.map((m) => (m.id === drag.id ? { ...m, atMs: at } : m)));
      }
      else if (drag.kind === "move") {
        const dx = (e.clientX - drag.startX) / pxPerMs;
        if (!drag.moved && Math.abs(e.clientX - drag.startX) < 4) return;
        drag.moved = true;
        // colours and effects move to any row; a preset stays on the rows of its lamps (only in time)
        const over = rows.find((r) => r.key === trackAt(e.clientX, e.clientY));
        const isScene = findClip(drag.clipId)?.clip.kind === "scene";
        const to = over && over.key !== drag.row && !isScene ? over : undefined;
        updateClip(drag.clipId, { startMs: Math.max(0, snap(drag.origStart + dx)) }, to);
        if (to) setDrag({ ...drag, row: to.key });
      } else {
        const dx = (e.clientX - drag.startX) / pxPerMs;
        if (drag.kind === "resize-r") updateClip(drag.clipId, { lengthMs: Math.max(beatMs / 2, snap(drag.origStart + drag.origLen + dx) - drag.origStart) });
        else {
          const start = Math.min(drag.origStart + drag.origLen - beatMs / 2, Math.max(0, snap(drag.origStart + dx)));
          updateClip(drag.clipId, { startMs: start, lengthMs: drag.origStart + drag.origLen - start });
        }
      }
    };
    const up = (e: PointerEvent) => {
      if (drag.kind === "marker" && !drag.moved) {
        const m = markers.find((x) => x.id === drag.id);
        if (m) { if (isMine) api.seekTimeline(m.atMs); else api.playTimeline(tl!.id, m.atMs).then(() => api.pauseTimeline()); }
      }
      if (drag.kind === "palette") {
        const track = trackAt(e.clientX, e.clientY);
        if (track) addClip(track, Math.max(0, snap(laneX(e.clientX) / pxPerMs)), drag.item);
      }
      setDrag(null);
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
  });

  // a drag started in the tools (they live outside this component)
  useEffect(() => {
    const on = (e: Event) => { const d = (e as CustomEvent<{ item: DragItem; x: number; y: number }>).detail; setDrag({ kind: "palette", item: d.item, x: d.x, y: d.y }); };
    window.addEventListener("dmx-tl-drag", on);
    return () => window.removeEventListener("dmx-tl-drag", on);
  }, []);

  if (!tl) {
    return (
      <div className="empty">
        <Icon name="timeline" size={40} />
        <div>{t("tl.empty")}</div>
        <button className="btn primary" onClick={create}><Icon name="plus" /> {t("tl.create")}</button>
      </div>
    );
  }

  const width = tl.lengthMs * pxPerMs;
  const beatPx = beatMs * pxPerMs;

  const upload = async (file: File) => {
    setUploading(file.name);
    try {
      const r = await api.uploadAudio(file);
      save({ ...tl, audioFile: r.file, lengthMs: Math.max(tl.lengthMs, Math.ceil(r.lengthMs / 1000) * 1000) }, true);
    } catch (e) { alert(String(e)); }
    setUploading(null);
  };

  return (
    <div className="tl-page">
      <div className="page-head">
        {!embedded && <h1>{t("nav.timeline")}</h1>}
        <select className="input" value={tl.id} onChange={(e) => { pending.current = false; setCurrentId(e.target.value); setSelected(null); }}>
          {server.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <input className="input" style={{ width: 160 }} value={tl.name} onChange={(e) => save({ ...tl, name: e.target.value })} />
        <button className="btn sm" onClick={create}><Icon name="plus" /> {t("tl.create")}</button>
        <button className="btn sm ghost danger icon-btn" title={t("scenes.delete")} onClick={() => { if (confirm(t("tl.confirmDelete", { name: tl.name }))) { api.removeTimeline(tl.id); setCurrentId(null); toast(t("toast.timelineRemoved", { name: tl.name })); } }}><Icon name="trash" /></button>
      </div>

      <div className="tl-transport card">
        <button className="btn lg icon-btn" style={{ width: 44 }} title={t("tl.prev") + " (←)"} onClick={() => api.prevTimeline(tl.id)}>|◀</button>
        {waiting
          ? <button className="btn lg go-btn" onClick={() => api.goTimeline(tl.id)}><Icon name="play" /> GO{live.waiting ? ` · ${live.waiting}` : ""}</button>
          : isMine && live.playing
            ? <button className="btn lg" onClick={() => api.pauseTimeline()}><Icon name="pause" /> {t("tl.pause")}</button>
            : <button className="btn lg primary" onClick={() => api.playTimeline(tl.id)}><Icon name="play" /> {t("tl.play")}</button>}
        <button className="btn lg" onClick={() => api.stopTimeline()} disabled={!isMine} title="Home"><Icon name="stop" /> {t("tl.stop")}</button>
        <button className="btn lg icon-btn" style={{ width: 44 }} title={t("tl.next") + " (→)"} onClick={() => api.nextTimeline(tl.id)}>▶|</button>
        <span className="tl-time mono">{fmt(playhead)} <span className="muted">/ {fmt(tl.lengthMs)}</span></span>
        <label className="check"><input type="checkbox" checked={tl.loop} onChange={(e) => save({ ...tl, loop: e.target.checked })} /><span className="box" /> {t("tl.loop")}</label>
        <label className="row" style={{ gap: 6 }}><span className="muted">BPM</span>
          <input className="input num" type="number" min={30} max={300} value={tl.bpm} onChange={(e) => save({ ...tl, bpm: Math.max(30, Math.min(300, +e.target.value || 120)) })} />
        </label>
        <label className="check"><input type="checkbox" checked={tl.snap} onChange={(e) => save({ ...tl, snap: e.target.checked })} /><span className="box" /> {t("tl.snap")}</label>
        <label className="row" style={{ gap: 6 }}><span className="muted">{t("tl.length")}</span>
          <input className="input num" type="number" min={1} value={Math.round(tl.lengthMs / 1000)} onChange={(e) => save({ ...tl, lengthMs: Math.max(1000, (+e.target.value || 60) * 1000) })} />
          <span className="muted">s</span>
        </label>
        <div style={{ flex: 1 }} />
        {onLinux() ? <span className="hint" title={t("linux.noSongs")}><Icon name="music" /> {tl.audioFile ? tl.audioFile + " · " : ""}{t("linux.noSongsShort")}</span> : (
        <label className="btn sm" title={t("tl.audioHint")}>
          <Icon name="music" /> {uploading ? t("tl.uploading") : tl.audioFile ? tl.audioFile : t("tl.addAudio")}
          <input type="file" accept=".mp3,.wav,.m4a,.aac,.wma,.flac,audio/*" style={{ display: "none" }}
            onChange={(e) => { const f = e.target.files?.[0]; if (f) upload(f); e.target.value = ""; }} />
        </label>)}
        {tl.audioFile && <button className="btn sm ghost icon-btn" title={t("tl.removeAudio")} onClick={() => save({ ...tl, audioFile: null }, true)}>×</button>}
        <div className="tl-points">
          <button className="btn sm" title={t("tl.setStartHint")} onClick={() => save({ ...tl, startMs: Math.min(snap(playhead), (tl.endMs ?? tl.lengthMs) - beatMs / 2) })}><span className="flag start" /> {t("tl.setStart")}</button>
          <button className="btn sm" title={t("tl.setEndHint")} onClick={() => save({ ...tl, endMs: Math.max(snap(playhead), (tl.startMs ?? 0) + beatMs / 2) })}><span className="flag end" /> {t("tl.setEnd")}</button>
          <button className="btn sm" title={t("tl.waitHint")} onClick={() => setMarkers([...markers, { id: Math.random().toString(16).slice(2, 10), atMs: snap(playhead), name: t("tl.waitName", { n: markers.filter((m) => m.kind === "wait").length + 1 }), kind: "wait" }])}><span className="mk wait" /> {t("tl.addWait")}</button>
          <button className="btn sm" title={t("tl.cueHint")} onClick={() => setMarkers([...markers, { id: Math.random().toString(16).slice(2, 10), atMs: snap(playhead), name: t("tl.cueName", { n: markers.filter((m) => m.kind === "cue").length + 1 }), kind: "cue" }])}><span className="mk cue" /> {t("tl.addCue")}</button>
          {((tl.startMs ?? 0) > 0 || tl.endMs != null) && <button className="btn sm ghost" onClick={() => save({ ...tl, startMs: 0, endMs: null })}>{t("tl.clearRegion")}</button>}
        </div>
        <label className="row" style={{ gap: 6 }} title="Zoom"><Icon name="search" />
          <input type="range" min={8} max={200} value={pxPerSec} onChange={(e) => setPxPerSec(+e.target.value)} style={{ width: 110 }} />
        </label>
      </div>

      <div className="tl-body">
        <div className="tl-main card">
          <div className="tl-lanes" ref={lanesRef}>
            <div style={{ width: width + 160, position: "relative" }}>
              <div className="tl-ruler" onPointerDown={(e) => { api.seekTimeline(Math.max(0, laneX(e.clientX) / pxPerMs)); setDrag({ kind: "seek" }); }}>
                <div className="tl-head sticky">{t("tl.time")}</div>
                <div className="tl-ruler-lane" style={{ width, ["--beat" as string]: beatPx + "px" }}>
                  <span className="tl-flag start" style={{ left: (tl.startMs ?? 0) * pxPerMs }} title={t("tl.setStart")}
                    onPointerDown={(e) => { e.stopPropagation(); setDrag({ kind: "flag", which: "start" }); }} />
                  <span className="tl-flag end" style={{ left: (tl.endMs ?? tl.lengthMs) * pxPerMs }} title={t("tl.setEnd")}
                    onPointerDown={(e) => { e.stopPropagation(); setDrag({ kind: "flag", which: "end" }); }} />
                  {markers.map((m) => (
                    <span key={m.id} className={"tl-marker " + m.kind + (isMine && live.waiting != null && Math.abs(live.positionMs - m.atMs) < 30 && m.kind === "wait" ? " waiting" : "")}
                      style={{ left: m.atMs * pxPerMs }} title={(m.name ?? "") + " · " + t(m.kind === "wait" ? "tl.waitPoint" : "tl.cuePoint") + " · " + t("tl.markerHint")}
                      onPointerDown={(e) => { e.stopPropagation(); setDrag({ kind: "marker", id: m.id, startX: e.clientX, orig: m.atMs, moved: false }); }}
                      onDoubleClick={(e) => { e.stopPropagation(); const n = prompt(t("scenes.name"), m.name ?? ""); if (n != null) setMarkers(markers.map((x) => (x.id === m.id ? { ...x, name: n } : x))); }}
                      onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); setMarkers(markers.filter((x) => x.id !== m.id)); }}>
                      <i>{m.name}</i>
                    </span>
                  ))}
                  {Array.from({ length: Math.floor(tl.lengthMs / 1000 / tickEvery(pxPerSec)) + 1 }, (_, i) => {
                    const sec = i * tickEvery(pxPerSec);
                    return <span key={i} style={{ left: sec * pxPerSec }}>{fmt(sec * 1000).replace(".0", "")}</span>;
                  })}
                </div>
              </div>
              {tl.audioFile && (
                <div className="tl-row wave-row">
                  <div className="tl-head sticky"><Icon name="music" /> <span className="ellipsis">{tl.audioFile}</span></div>
                  <Waveform peaks={peaks} width={width} />
                </div>
              )}
              {rows.map((r) => (
                <TrackRow key={r.key} row={r} width={width} beatPx={beatPx} pxPerMs={pxPerMs} scenes={scenes} effects={effects}
                  selected={selected} fxName={fxName}
                  dropOk={drag?.kind === "palette" && (drag.item.type !== "scene" || sceneRows(drag.item.scene).includes(r))}
                  solidIndex={solidIndex}
                  onSelect={setSelected}
                  onRemoveTrack={r.kind === "group" ? () => save({ ...tl, tracks: tl.tracks.filter((x) => x.id !== r.track!.id) }) : undefined}
                  onClipDown={(e, c, part) => {
                    e.stopPropagation(); e.preventDefault();
                    setSelected(c.id);
                    if (part === "body") setDrag({ kind: "move", clipId: c.id, row: r.key, startX: e.clientX, origStart: c.startMs, moved: false });
                    else setDrag({ kind: part === "l" ? "resize-l" : "resize-r", clipId: c.id, startX: e.clientX, origStart: c.startMs, origLen: c.lengthMs });
                  }} />
              ))}
              {(tl.startMs ?? 0) > 0 && <div className="tl-outside" style={{ left: 160, width: (tl.startMs ?? 0) * pxPerMs }} />}
              {tl.endMs != null && <div className="tl-outside" style={{ left: 160 + tl.endMs * pxPerMs, width: Math.max(0, (tl.lengthMs - tl.endMs) * pxPerMs) }} />}
              {markers.filter((m) => m.kind === "wait").map((m) => <div key={m.id} className="tl-waitline" style={{ left: 160 + m.atMs * pxPerMs }} />)}
              <div className="tl-playhead" style={{ left: 160 + playhead * pxPerMs }} />
            </div>
          </div>
          <p className="hint" style={{ margin: "8px 0 0" }}>{t("tl.rowsHint")}</p>
        </div>
      </div>

      {sel && (
        <ClipInspector clip={sel.clip} rowName={rowOf(sel.track)?.name ?? sel.track.name} scenes={scenes} effects={effects} fxName={fxName}
          onChange={(c) => updateClip(sel.clip.id, c)}
          onDelete={() => { save(tidy({ ...tl, tracks: tl.tracks.map((k) => ({ ...k, clips: k.clips.filter((c) => c.id !== sel.clip.id) })) })); setSelected(null); }} />
      )}

      {drag?.kind === "palette" && (
        <div className="drag-overlay" style={{ position: "fixed", left: drag.x + 10, top: drag.y + 10, pointerEvents: "none", zIndex: 100 }}>
          {drag.item.type === "color" && <span className="drag-swatch" style={{ background: drag.item.hex }} />}
          + {drag.item.type === "scene" ? drag.item.scene.name : drag.item.type === "color" ? drag.item.name : drag.item.chase?.name ?? fxName(drag.item.fx)}
        </div>
      )}
    </div>
  );
}

function tickEvery(pxPerSec: number) {
  for (const s of [1, 2, 5, 10, 15, 30, 60]) if (s * pxPerSec >= 70) return s;
  return 120;
}

function Waveform({ peaks, width }: { peaks: number[] | null; width: number }) {
  const ref = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const c = ref.current;
    if (!c) return;
    const w = Math.max(1, Math.round(width)), h = 48;
    c.width = w; c.height = h;
    const g = c.getContext("2d")!;
    g.clearRect(0, 0, w, h);
    if (!peaks) return;
    g.fillStyle = "rgba(252,178,78,0.55)";
    for (let x = 0; x < w; x++) {
      const v = peaks[Math.floor((x / w) * peaks.length)] ?? 0;
      const bar = Math.max(1, v * (h - 4));
      g.fillRect(x, (h - bar) / 2, 1, bar);
    }
  }, [peaks, width]);
  return <canvas ref={ref} className="tl-wave" style={{ width }} />;
}

function TrackRow({ row, width, beatPx, pxPerMs, scenes, effects, selected, dropOk, solidIndex, fxName, onSelect, onRemoveTrack, onClipDown }: {
  row: Row; width: number; beatPx: number; pxPerMs: number; scenes: SceneInfo[]; effects: EffectInfo[]; solidIndex: number;
  selected: string | null; dropOk: boolean; fxName: (e?: EffectInfo) => string;
  onSelect: (id: string | null) => void; onRemoveTrack?: () => void;
  onClipDown: (e: RPointerEvent, c: TimelineClip, part: "body" | "l" | "r") => void;
}) {
  const chases = useShow().full?.state.chases ?? [];
  const t = useT();
  const clips = row.track?.clips ?? [];
  const { line, count } = lines(clips);
  return (
    <div className={"tl-row " + row.kind + (dropOk ? " drop-ok" : "")} style={{ height: count * ROW_H + 6 }}>
      <div className="tl-head sticky" title={row.name}>
        {row.kind === "all" ? <Icon name="grid" size={14} /> : row.kind === "lamp" ? <Icon name="bulb" size={14} /> : <Icon name="users" size={14} />}
        <span className="tl-row-name ellipsis">{row.name}</span>
        {onRemoveTrack && <button className="tl-del" title={t("scenes.delete")} onClick={onRemoveTrack}>×</button>}
      </div>
      <div className="tl-lane" data-track={row.key} style={{ width, ["--beat" as string]: beatPx + "px" }}
        onPointerDown={() => onSelect(null)}>
        {clips.map((c) => {
          const scene = c.kind === "scene" ? scenes.find((s) => s.id === c.sceneId) : undefined;
          const fx = c.kind === "effect" && c.effect ? effects[c.effect.effectIndex] : undefined;
          const bg = scene ? scene.color : `linear-gradient(90deg, ${c.effect?.colorA ?? "#555"}, ${c.effect?.colorB ?? "#333"})`;
          // a colour block (all lamps colour A, A = B) shows as that colour
          const solid = !scene && !!c.effect && !c.effect.chaseId && c.effect.effectIndex === solidIndex && c.effect.colorA.toLowerCase() === c.effect.colorB.toLowerCase();
          const label = scene ? scene.name : solid ? t("tl.colorClip") : c.effect?.chaseId ? "★ " + (chases.find((x) => x.id === c.effect!.chaseId)?.name ?? "?") : fxName(fx);
          return (
            <div key={c.id} className={"tl-clip" + (selected === c.id ? " selected" : "")}
              style={{ left: c.startMs * pxPerMs, width: Math.max(6, c.lengthMs * pxPerMs), top: 3 + (line.get(c.id) ?? 0) * ROW_H, height: ROW_H - 4, background: bg, color: scene ? contrast(scene.color) : "#fff" }}
              onPointerDown={(e) => onClipDown(e, c, "body")} title={label}>
              <span className="grip l" onPointerDown={(e) => onClipDown(e, c, "l")} />
              <span className="tl-clip-label">{label}</span>
              <span className="grip r" onPointerDown={(e) => onClipDown(e, c, "r")} />
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ClipInspector({ clip, rowName, scenes, effects, fxName, onChange, onDelete }: {
  clip: TimelineClip; rowName: string; scenes: SceneInfo[]; effects: EffectInfo[]; fxName: (e?: EffectInfo) => string;
  onChange: (c: Partial<TimelineClip>) => void; onDelete: () => void;
}) {
  const t = useT();
  const fx = clip.effect;
  const setFx = (c: Partial<EffectSettings>) => onChange({ effect: { ...fx!, ...c } });
  const chases = useShow().full?.state.chases ?? [];
  return (
    <div className="card tl-inspector">
      <div className="row">
        <h2 style={{ marginRight: 8 }}>{clip.kind === "scene" ? t("tl.sceneClip") : t("tl.effectClip")}</h2>
        <span className="muted">{rowName}</span>
        <div style={{ flex: 1 }} />
        <button className="btn sm ghost danger" onClick={onDelete}><Icon name="trash" /> {t("scenes.delete")}</button>
      </div>
      <div className="row" style={{ marginTop: 10, gap: 14, alignItems: "flex-end" }}>
        <label className="field">{t("tl.start")}
          <input className="input num" type="number" step={0.1} min={0} value={(clip.startMs / 1000).toFixed(1)} onChange={(e) => onChange({ startMs: Math.max(0, +e.target.value * 1000) })} />
        </label>
        <label className="field">{t("tl.length")}
          <input className="input num" type="number" step={0.1} min={0.1} value={(clip.lengthMs / 1000).toFixed(1)} onChange={(e) => onChange({ lengthMs: Math.max(50, +e.target.value * 1000) })} />
        </label>
        {clip.kind === "scene" ? (
          <>
            <label className="field">{t("tl.scene")}
              <select className="input" value={clip.sceneId ?? ""} onChange={(e) => onChange({ sceneId: e.target.value })}>
                {scenes.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </label>
            <label className="field">{t("scenes.fade")}
              <input className="input num" type="number" step={0.1} min={-1} value={clip.fadeMs < 0 ? "" : clip.fadeMs / 1000}
                placeholder={t("tl.sceneFade")} onChange={(e) => onChange({ fadeMs: e.target.value === "" ? -1 : Math.max(0, +e.target.value * 1000) })} />
            </label>
          </>
        ) : fx && (
          <>
            <label className="field">{t("tl.effect")}
              <select className="input" value={fx.chaseId ? "c:" + fx.chaseId : String(fx.effectIndex)}
                onChange={(e) => e.target.value.startsWith("c:") ? setFx({ chaseId: e.target.value.slice(2), screenSync: false }) : setFx({ effectIndex: +e.target.value, chaseId: null, screenSync: false })}>
                {chases.length > 0 && (
                  <optgroup label={t("fxcat.own")}>
                    {chases.map((c) => <option key={c.id} value={"c:" + c.id}>★ {c.name}</option>)}
                  </optgroup>
                )}
                <optgroup label={t("nav.effects")}>
                  {effects.map((e) => <option key={e.index} value={e.index}>{fxName(e)}</option>)}
                </optgroup>
              </select>
            </label>
            <label className="field">{t("fx.colorA")}
              <span className="colorbtn" style={{ background: fx.colorA, width: 90 }}><ColorInput value={fx.colorA} onPick={(h) => setFx({ colorA: h })} /></span>
            </label>
            <label className="field">{t("fx.colorB")}
              <span className="colorbtn" style={{ background: fx.colorB, width: 90 }}><ColorInput value={fx.colorB} onPick={(h) => setFx({ colorB: h })} /></span>
            </label>
            <label className="field">{t("fx.step")} (ms)
              <input className="input num" type="number" min={40} max={10000} step={10} value={fx.stepMs} onChange={(e) => setFx({ stepMs: Math.max(40, +e.target.value) })} />
            </label>
          </>
        )}
      </div>
    </div>
  );
}
