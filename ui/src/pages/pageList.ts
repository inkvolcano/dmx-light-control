/** The menu's pages, in order. Settings can never be hidden (otherwise you could not show the others again). */
export const pages = [
  { id: "live", icon: "bulb" },
  { id: "effects", icon: "sparkles" },
  { id: "screen", icon: "monitor" },
  { id: "timeline", icon: "timeline" },
  { id: "schedule", icon: "clock" },
  { id: "music", icon: "music" },
  { id: "faders", icon: "sliders" },
  { id: "patch", icon: "plug" },
  { id: "test", icon: "grid" },
  { id: "integrations", icon: "link" },
  { id: "settings", icon: "gear" },
] as const;
export type PageId = (typeof pages)[number]["id"];

/** "easy" is the Easy-mode button at the bottom of the menu; it can be hidden like a page. */
export const hideable = [...pages.map((p) => p.id).filter((id) => id !== "settings"), "easy"] as const;

export function isShown(id: string, hidden: string[] | undefined | null) {
  return id === "settings" || !(hidden ?? []).includes(id);
}
