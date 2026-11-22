import { useEffect, useState, type PointerEvent as RPointerEvent } from "react";
import { api, type ColorRef, type EffectInfo, type PaletteColor, type SceneInfo } from "../api";
import { useT } from "../i18n";
import { EffectsPanel } from "../pages/EffectsPage";
import { useShow } from "../store";
import { contrast } from "./ColorPicker";
import { Icon } from "./Icon";
import { ScenesManager } from "./ScenesManager";
import { toast } from "./Toast";

/** Something dragged out of the tools (onto the timeline): a preset, an effect (or own pattern) or a colour. */
export type DragItem =
  | { type: "scene"; scene: SceneInfo }
  | { type: "effect"; fx: EffectInfo; chase?: { id: string; name: string } }
  | { type: "color"; hex: string; name: string };
export type DragStart = (item: DragItem, e: RPointerEvent) => void;

type Tab = "colors" | "effects" | "presets";

/** "Picking a colour also turns the lamp on" - remembered per device. */
function useColorTurnsOn(): [boolean, (v: boolean) => void] {
  const [v, setV] = useState(() => {
    try { return localStorage.getItem("colorTurnsOn") !== "0"; } catch { return true; }
  });
  useEffect(() => { try { localStorage.setItem("colorTurnsOn", v ? "1" : "0"); } catch { /* ignore */ } }, [v]);
  return [v, setV];
}

/**
 * The tools under the stage (Bediening) and under the timeline: Kleuren, Effecten and Presets. A click works
 * on the lamps now (the chosen ones, or all); with `onDrag` (the timeline) colours, effects and presets can
 * also be dragged out.
 */
export function LiveTools({ selected, onDrag, storageKey = "liveTab" }: { selected: string[]; onDrag?: DragStart; storageKey?: string }) {
  const t = useT();
  const { full, boot } = useShow();
  const [colorTurnsOn, setColorTurnsOn] = useColorTurnsOn();
  const [recentColors, setRecentColors] = useState<string[]>(() => { try { return JSON.parse(localStorage.getItem("recentColors") ?? "[]"); } catch { return []; } });
  const [tab, setTabState] = useState<Tab>(() => { try { const v = localStorage.getItem(storageKey); return v === "effects" || v === "presets" ? v : "colors"; } catch { return "colors"; } });
  const setTab = (v: Tab) => { setTabState(v); try { localStorage.setItem(storageKey, v); } catch { /* ignore */ } };
  // another page can open a tab on Bediening ("dmx-livetab")
  useEffect(() => {
    if (storageKey !== "liveTab") return;
    const on = (e: Event) => { const v = (e as CustomEvent<Tab>).detail; if (v === "colors" || v === "effects" || v === "presets") setTabState(v); };
    window.addEventListener("dmx-livetab", on);
    return () => window.removeEventListener("dmx-livetab", on);
  }, [storageKey]);
  if (!full || !boot) return null;

  const pick = (color: ColorRef) => {
    // nothing chosen = nothing happens (choose lamps on the stage, or "Alles kiezen")
    if (!selected.length) { toast(t("live.pickFirst"), false); return; }
    // remember it at the front of "recently used" (this device)
    const id = color.key ? "k:" + color.key : "h:" + color.hex;
    const next = [id, ...recentColors.filter((x) => x !== id)].slice(0, 12);
    setRecentColors(next);
    try { localStorage.setItem("recentColors", JSON.stringify(next)); } catch { /* ignore */ }
    // every lamp chosen = all lamps (a running effect then stops for the colour, as with the top buttons)
    const ids = selected.length >= full.patch.length ? [] : selected;
    return api.looks(ids, colorTurnsOn ? true : undefined, color);
  };
  const allColors = [...boot.palette.fan, ...boot.palette.specials];
  const recent = recentColors
    .map((id) => id.startsWith("k:") ? allColors.find((c) => c.key === id.slice(2)) : { key: "", name: id.slice(2), hex: id.slice(2) })
    .filter((c): c is PaletteColor => !!c);
  const drag = onDrag && ((c: PaletteColor, e: RPointerEvent) => onDrag({ type: "color", hex: c.hex, name: t("color." + c.key, undefined, c.name) }, e));

  return (
    <div className="card live-tools">
      <div className="row live-tabs">
        <div className="seg page-tabs" role="tablist">
          {(["colors", "effects", "presets"] as Tab[]).map((v) => (
            <button key={v} role="tab" aria-selected={tab === v} className={tab === v ? "active" : ""} onClick={() => setTab(v)}>
              <Icon name={v === "colors" ? "bulb" : v === "effects" ? "sparkles" : "save"} size={15} /> {t("live.tab." + v)}
              {v === "presets" && <span className="muted"> ({full.scenes.length})</span>}
            </button>
          ))}
        </div>
        <div className="grow" />
        {onDrag ? <span className="muted">{t("live.dragHint")}</span>
          : <span className={selected.length ? "muted" : "warn-text"}>{selected.length ? t("live.forSelected", { n: selected.length }) : t("live.pickFirst")}</span>}
      </div>
      {tab === "effects" && <EffectsPanel selected={selected} onDrag={onDrag} requireSelection />}
      {tab === "presets" && <ScenesManager selected={selected} onDrag={onDrag} requireSelection />}
      {tab === "colors" && (
        <div className="palette-panel">
          <label className="check" style={{ marginBottom: 12 }}>
            <input type="checkbox" checked={colorTurnsOn} onChange={(e) => setColorTurnsOn(e.target.checked)} />
            <span className="box" /> {t("live.colorTurnsOn")}
          </label>
          {recent.length > 0 && (
            <div className="palette-section">
              <h3>{t("live.recentColors")}</h3>
              <Swatches colors={recent} cls="fan" onPick={(c) => pick(c.key ? { key: c.key } : { hex: c.hex })} onDrag={drag} />
            </div>
          )}
          <div className="palette-section">
            <h3>{t("live.palette")}</h3>
            <Swatches colors={boot.palette.fan} cls="fan" onPick={(c) => pick({ key: c.key })} onDrag={drag} />
          </div>
          <div className="palette-section">
            <h3>{t("live.specials")}</h3>
            <Swatches colors={boot.palette.specials} cls="special" onPick={(c) => pick({ key: c.key })} onDrag={drag} />
          </div>
        </div>
      )}
    </div>
  );
}

function Swatches({ colors, cls, onPick, onDrag }: {
  colors: PaletteColor[]; cls: string; onPick: (c: PaletteColor) => void; onDrag?: (c: PaletteColor, e: RPointerEvent) => void;
}) {
  const t = useT();
  return (
    <div className={"swatches " + cls + (onDrag ? " draggable" : "")}>
      {colors.map((c, i) => (
        <button key={c.key || c.hex + i} className="swatch" style={{ background: c.hex, color: contrast(c.hex) }}
          title={t("color." + c.key, undefined, c.name)} onClick={() => onPick(c)}
          onPointerDown={onDrag ? (e) => onDrag(c, e) : undefined}>
          {c.key === "uv" ? "UV" : ""}
        </button>
      ))}
    </div>
  );
}
