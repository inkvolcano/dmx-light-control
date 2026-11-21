import { useEffect, useState } from "react";
import { HelpPanel } from "./components/Help";
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
import { useShow } from "./store";

import { isShown, pages, type PageId } from "./pages/pageList";

function savedPage(): PageId {
  try {
    const p = localStorage.getItem("page");
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
  useEffect(() => {
    if (!isShown(page, full?.settings.hiddenPages)) setPage(pages.find((p) => isShown(p.id, full?.settings.hiddenPages))?.id ?? "settings");
  }, [hiddenKey]); // eslint-disable-line react-hooks/exhaustive-deps -- not on page: a hidden page opened from elsewhere stays open
  // other parts of the app can open a page (e.g. the effects panel opens the Screen page)
  useEffect(() => {
    const go = (e: Event) => { const p = (e as CustomEvent<string>).detail; if (pages.some((x) => x.id === p)) setPage(p as PageId); };
    window.addEventListener("dmx-goto", go);
    return () => window.removeEventListener("dmx-goto", go);
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
  const locked = !!full?.settings.locked && !unlocked;
  const lock = () => { try { sessionStorage.removeItem("pin"); } catch { /* ignore */ } setUnlocked(false); };

  useEffect(() => {
    try { localStorage.setItem("page", page); } catch { /* ignore */ }
  }, [page]);

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
        {pages.filter((p) => isShown(p.id, full?.settings.hiddenPages)).map((p) => (
          <button key={p.id} className={"nav-btn" + (page === p.id ? " active" : "")} onClick={() => setPage(p.id)}>
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
      <TopBar />
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
      {help && <HelpPanel page={page} initialTab={help} onClose={closeHelp} />}
      {!online && (
        <div className="offline">
          <div className="box"><div className="spinner" />{t("status.offline")}</div>
        </div>
      )}
    </div>
  );
}
