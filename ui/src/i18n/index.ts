import { useShow } from "../store";
import { en } from "./en";
import { nl } from "./nl";

// To add a language: create xx.ts with the same keys as en.ts and add it here.
export const languages: { code: string; label: string; dict: Record<string, string> }[] = [
  { code: "nl", label: "Nederlands", dict: nl },
  { code: "en", label: "English", dict: en },
];

export type T = (key: string, vars?: Record<string, string | number>, fallback?: string) => string;

export function translate(lang: string, key: string, vars?: Record<string, string | number>, fallback?: string) {
  const dict = languages.find((l) => l.code === lang)?.dict ?? nl;
  let s = dict[key] ?? fallback ?? nl[key] ?? key;
  if (vars) for (const [k, v] of Object.entries(vars)) s = s.replaceAll("{" + k + "}", String(v));
  return s;
}

export function useT(): T {
  const lang = useShow().full?.settings.language ?? "nl";
  return (key, vars, fallback) => translate(lang, key, vars, fallback);
}

export function useLang() {
  return useShow().full?.settings.language ?? "nl";
}
