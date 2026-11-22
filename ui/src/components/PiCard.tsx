import { useEffect, useState } from "react";
import { api } from "../api";
import { useT } from "../i18n";
import { Icon } from "./Icon";
import type { UpdateInfo } from "./UpdateCard";

/**
 * Settings > Raspberry Pi: download the Pi version (from the newest release on GitHub) and how to install it.
 * The Pi runs DMX Light Control as a background service; you use it from a phone, tablet or browser.
 */
export function PiCard() {
  const t = useT();
  const [info, setInfo] = useState<UpdateInfo | null>(null);
  useEffect(() => { api.update().then(setInfo).catch(() => {}); }, []);
  const file = info?.piUrl ? decodeURIComponent(info.piUrl.split("/").pop() ?? "") : null;
  const releases = `https://github.com/${info?.repo ?? "inkvolcano/dmx-light-control"}/releases/latest`;
  const steps = [1, 2, 3, 4].map((i) => t("pi.step" + i, { file: file ?? "DMX-Light-Control-<versie>-linux-arm64.tar.gz" }));
  return (
    <div className="card stack">
      <h2><Icon name="grid" /> {t("pi.title")}</h2>
      <p className="hint">{t("pi.hint")}</p>
      <div className="row" style={{ flexWrap: "wrap" }}>
        {info?.piUrl
          ? <a className="btn primary" href={info.piUrl} target="_blank" rel="noreferrer"><Icon name="upload" /> {t("pi.download", { v: info.latest ?? "" })}</a>
          : <a className="btn" href={releases} target="_blank" rel="noreferrer"><Icon name="link" /> {t("pi.releases")}</a>}
        {!info?.piUrl && info && <span className="hint">{t("pi.notYet")}</span>}
      </div>
      <ol className="steps">{steps.map((s, i) => <li key={i}><code className="pi-cmd">{s}</code></li>)}</ol>
      <p className="hint">{t("pi.after")}</p>
    </div>
  );
}
