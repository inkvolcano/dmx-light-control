import { useEffect, useRef, useState } from "react";
import { HelpPanel } from "./components/Help";
import { LiveRail } from "./components/LiveRail";
import { CommandPalette } from "./components/CommandPalette";
import { Toaster } from "./components/Toast";
import { Icon } from "./components/Icon";
import { TopBar } from "./components/TopBar";
import { useT } from "./i18n";
import { EffectsPage } from "./pages/EffectsPage";
import { ScreenPage } from "./pages/ScreenPage";
import { FadersPage } from "./pages/FadersPage";
import { LivePage } from "./pages/LivePage";
import { MusicPage } from "./pages/MusicPage";
import { TimelinePage } from "./pages/TimelinePage";
import { TestPage } from "./pages/TestPage";
import { IntegrationsPage } from "./pages/IntegrationsPage";
import { PatchPage } from "./pages/PatchPage";
import { SettingsPage } from "./pages/SettingsPage";
import { SchedulePage } from "./pages/SchedulePage";
import { PinDialog } from "./pages/OperatorView";
import { EasyView } from "./pages/EasyView";
import { PairScreen } from "./pages/PairScreen";
import { sessionPin } from "./api";
import { onLinux, useShow } from "./store";

import { isShown, pages, type PageId } from "./pages/pageList";

function savedPage(): PageId {
  try {
    const p = localStorage.getItem("page");
    if (p === "effects") { localStorage.setItem("liveTab", "effects"); return "live"; }
    if (p === "timeline") { localStorage.setItem("liveTimeline", "1"); return "live"; }
    if (pages.some((x) => x.id === p)) return p as PageId;
  } catch { /* storage unavailable */ }
  return "live";
}

export function App() {
  const t = useT();
  const { full, online, boot, needsPairing } = useShow();
  const [page, setPage] = useState<PageId>(savedPage);
  const [selected, setSelected] = useState<string[]>([]);
  const [unlocked, setUnlocked] = useState(() => !!sessionPin());
  const [askPin, setAskPin] = useState(false);
  // easy mode: per device. ?easy / ?full in the address sets it; a phone starts in easy mode.
  const [easy, setEasyState] = useState<boolean>(() => {
    // ?easy / ?full (QR code, bookmark) choose once; after that the device remembers what you switched to
    const q = new URLSearchParams(location.search);
    const fromUrl = q.has("easy") ? true : q.has("full") ? false : null;
    if (fromUrl != null) {
      try { localStorage.setItem("easy", fromUrl ? "1" : "0"); } catch { /* ignore */ }
      q.delete("easy"); q.delete("full");
      history.replaceState(null, "", location.pathname + (q.toString() ? "?" + q : "") + location.hash);
      return fromUrl;
    }
    try { const v = localStorage.getItem("easy"); if (v !== null) return v === "1"; } catch { /* ignore */ }
    return window.matchMedia("(max-width: 700px)").matches;
  });
  const setEasy = (v: boolean) => { setEasyState(v); try { localStorage.setItem("easy", v ? "1" : "0"); } catch { /* ignore */ } };
  useEffect(() => {
    const on = () => setEasy(true);
    window.addEventListener("dmx-easy", on);
    return () => window.removeEventListener("dmx-easy", on);
  }, []);
  const hiddenKey = (full?.settings.hiddenPages ?? []).join(",");
  // shown in the menu: not hidden by the user, and available here (no screen sync on Linux)
  const visible = (id: string) => isShown(id, full?.settings.hiddenPages) && !(id === "screen" && onLinux());
  useEffect(() => {
    if (!visible(page)) setPage(pages.find((p) => visible(p.id))?.id ?? "settings");
  }, [hiddenKey]); // eslint-disable-line react-hooks/exhaustive-deps -- not on page: a hidden page opened from elsewhere stays open
  // other parts of the app can open a page (e.g. the effects panel opens the Screen page)
  useEffect(() => {
    const go = (e: Event) => {
      const p = (e as CustomEvent<string>).detail;
      // effects and presets live on Bediening, as tabs under the stage
      if (p === "effects" || p === "presets") {
        try { localStorage.setItem("liveTab", p); } catch { /* ignore */ }
        setPage("live");
        window.dispatchEvent(new CustomEvent("dmx-livetab", { detail: p }));
        return;
      }
      if (p === "timeline") {
        try { localStorage.setItem("liveTimeline", "1"); } catch { /* ignore */ }
        setPage("live");
        window.dispatchEvent(new Event("dmx-livetimeline"));
        return;
      }
      if (pages.some((x) => x.id === p)) setPage(p as PageId);
    };
    window.addEventListener("dmx-goto", go);
    return () => window.removeEventListener("dmx-goto", go);
  }, []);
  // search everything: Ctrl+K, or the search button in the top bar
  const [search, setSearch] = useState(false);
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); setSearch((v) => !v); } };
    const open = () => setSearch(true);
    window.addEventListener("keydown", key);
    window.addEventListener("dmx-search", open);
    return () => { window.removeEventListener("keydown", key); window.removeEventListener("dmx-search", open); };
  }, []);
  // help: only on request (F1 or the ? button), never by itself
  const [help, setHelp] = useState<null | "page" | "start">(null);
  const closeHelp = () => setHelp(null);
  const toggleHelp = () => setHelp((h) => (h ? null : "page"));
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === "F1") { e.preventDefault(); toggleHelp(); } };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, []);
  // right column: folded per device; on a phone it is always the small strip of meters
  const [folded, setFolded] = useState(() => { try { return localStorage.getItem("railFolded") === "1"; } catch { return false; } });
  const fold = () => setFolded((f) => { try { localStorage.setItem("railFolded", f ? "0" : "1"); } catch { /* ignore */ } return !f; });
  const [phone, setPhone] = useState(() => window.matchMedia("(max-width: 720px)").matches);
  useEffect(() => {
    const mq = window.matchMedia("(max-width: 720px)");
    const on = () => setPhone(mq.matches);
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, []);
  // "Opbouwen" in the menu: open while one of its pages is open, or when opened by hand
  const onBuild = pages.some((p) => p.id === page && p.group === "build" && p.menu);
  const [buildOpenByHand, setBuildOpen] = useState(() => { try { return localStorage.getItem("buildOpen") === "1"; } catch { return false; } });
  useEffect(() => { try { localStorage.setItem("buildOpen", buildOpenByHand ? "1" : "0"); } catch { /* ignore */ } }, [buildOpenByHand]);
  const buildOpen = buildOpenByHand || onBuild;
  const locked = !!full?.settings.locked && !unlocked;
  const lock = () => { try { sessionStorage.removeItem("pin"); } catch { /* ignore */ } setUnlocked(false); };

  // a setup page opened from the right column goes back to the menu page it came from
  const lastMenuPage = useRef<PageId>("live");
  useEffect(() => {
    try { localStorage.setItem("page", page); } catch { /* ignore */ }
    if (pages.some((p) => p.id === page && p.menu)) lastMenuPage.current = page;
  }, [page]);
  useEffect(() => {
    const back = () => setPage(lastMenuPage.current);
    window.addEventListener("dmx-back", back);
    return () => window.removeEventListener("dmx-back", back);
  }, []);

  useEffect(() => {
    document.documentElement.lang = full?.settings.language ?? "nl";
  }, [full?.settings.language]);

  // drop selections of fixtures that no longer exist
  useEffect(() => {
    if (!full) return;
    const ids = new Set(full.patch.map((p) => p.id));
    setSelected((s) => (s.every((id) => ids.has(id)) ? s : s.filter((id) => ids.has(id))));
  }, [full]);

  if (needsPairing) return <PairScreen />;

  if ((locked || easy) && full && boot) {
    return (
      <>
        <EasyView locked={locked} onFull={() => (locked ? setAskPin(true) : setEasy(false))} onHelp={() => setHelp("page")} />
        {help && <HelpPanel page="operator" initialTab={help} onClose={closeHelp} />}
        {askPin && <PinDialog onCancel={() => setAskPin(false)} onDone={(pin) => {
          try { sessionStorage.setItem("pin", pin); } catch { /* ignore */ }
          setAskPin(false); setUnlocked(true); setEasy(false);
        }} />}
      </>
    );
  }

  return (
    <div className="shell">
      <nav className="sidebar">
        <div className="brand" title="DMX Light Control"><div className="brand-dot" /></div>
        {pages.filter((p) => p.menu && p.group === "play" && visible(p.id)).map((p) => (
          <button key={p.id} className={"nav-btn" + (page === p.id ? " active" : "")} onClick={() => setPage(p.id)}>
            <Icon name={p.icon} />
            {t("nav." + p.id)}
          </button>
        ))}
        {/* setting up: folded away behind one button, open while you are on one of its pages */}
        <button className={"nav-btn nav-build" + (buildOpen ? " open" : "")} onClick={() => setBuildOpen(!buildOpen)} aria-expanded={buildOpen}>
          <Icon name="gear" />
          {t("nav.build")}
        </button>
        {buildOpen && pages.filter((p) => p.menu && p.group === "build" && visible(p.id)).map((p) => (
          <button key={p.id} className={"nav-btn sub" + (page === p.id ? " active" : "")} onClick={() => setPage(p.id)}>
            <Icon name={p.icon} />
            {t("nav." + p.id)}
          </button>
        ))}
        <div className="spacer" />
        {isShown("easy", full?.settings.hiddenPages) && (
          <button className="nav-btn" onClick={() => setEasy(true)} title={t("easy.switchHint")}>
            <Icon name="grid" />{t("easy.button")}
          </button>
        )}
        {/* help stays out of the way: a small ? (or F1) */}
        <button className={"nav-help" + (help ? " active" : "")} onClick={toggleHelp} title={t("help.button") + " (F1)"} aria-label={t("help.button")}>?</button>
        {full?.settings.locked && (
          <button className="nav-btn" onClick={lock} title={t("op.lockNow")}><Icon name="lock" />{t("op.lock")}</button>
        )}
      </nav>
      <TopBar rail={!!full && isShown("rail", full.settings.hiddenPages)} />
      <main className="main">
        {full && boot && (
          <>
            {page === "live" && <LivePage selected={selected} setSelected={setSelected} goPatch={() => setPage("patch")} />}
            {page === "effects" && <EffectsPage />}
            {page === "screen" && <ScreenPage />}
            {page === "music" && <MusicPage />}
            {page === "timeline" && <TimelinePage />}
            {page === "schedule" && <SchedulePage />}
            {page === "faders" && <FadersPage selected={selected} />}
            {page === "patch" && <PatchPage />}
            {page === "test" && <TestPage />}
            {page === "integrations" && <IntegrationsPage />}
            {page === "settings" && <SettingsPage />}
          </>
        )}
      </main>
      {full && boot && isShown("rail", full.settings.hiddenPages) && <LiveRail folded={folded || phone} onFold={fold} page={page} selected={selected} />}
      {help && <HelpPanel page={page} initialTab={help} onClose={closeHelp} />}
      <CommandPalette open={search} onClose={() => setSearch(false)} />
      <Toaster />
      {!online && (
        <div className="offline">
          <div className="box"><div className="spinner" />{t("status.offline")}</div>
        </div>
      )}
    </div>
  );
}
