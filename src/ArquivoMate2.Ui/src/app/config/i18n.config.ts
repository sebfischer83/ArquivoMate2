// Central i18n configuration: source of truth for languages and flag mapping
// Keep this minimal and framework-agnostic so both Transloco and UI can import it.

export const AVAILABLE_LANGS = ['en', 'de', 'ru'] as const;

// Map language codes to flag asset codes when they differ
// Example: 'en' should show United Kingdom flag 'gb.svg'
export const LANGUAGE_FLAGS: Record<string, string> = {
  en: 'gb',
  // other languages default to their own code if not specified
};

export function flagFor(lang: string): string {
  return LANGUAGE_FLAGS[lang] ?? lang;
}
