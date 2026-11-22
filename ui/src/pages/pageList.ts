/**
 * The app's pages, in menu order. Settings can never be hidden (otherwise you could not show the others again).
 * Sound and screen are not in the menu: the right column shows them on every page, and its gear opens these
 * pages for the setup.
 */
export const pages = [
  // playing the light: always in the menu
  { id: "live", icon: "bulb", menu: true, group: "play" },
  // effects are a tab on Bediening now; the id stays so old links (and "go to effects") still land there
  { id: "effects", icon: "sparkles", menu: false, group: "play" },
  // the timeline folds open on Bediening; the id stays so old links land there
  { id: "timeline", icon: "timeline", menu: false, group: "play" },
  // setting up: behind "Opbouwen" in the menu
  { id: "patch", icon: "plug", menu: true, group: "build" },
  { id: "test", icon: "grid", menu: true, group: "build" },
  { id: "faders", icon: "sliders", menu: true, group: "build" },
  { id: "schedule", icon: "clock", menu: true, group: "build" },
  { id: "integrations", icon: "link", menu: true, group: "build" },
  { id: "settings", icon: "gear", menu: true, group: "build" },
  // opened from the right column
  { id: "screen", icon: "monitor", menu: false, group: "build" },
  { id: "music", icon: "music", menu: false, group: "build" },
] as const;
export type PageId = (typeof pages)[number]["id"];

/** "easy" is the Easy-mode button at the bottom of the menu, "rail" the right column; both can be hidden like a page. */
export const hideable = [...pages.filter((p) => p.menu).map((p) => p.id).filter((id) => id !== "settings"), "rail", "easy"] as const;

export function isShown(id: string, hidden: string[] | undefined | null) {
  return id === "settings" || !(hidden ?? []).includes(id);
}
