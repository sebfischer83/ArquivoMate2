// Central i18n configuration: source of truth for languages and flag mapping
// Keep this minimal and framework-agnostic so both Transloco and UI can import it.

export const AVAILABLE_LANGS = ['en', 'de', 'ru'] as const;

// localStorage key for persisting the chosen UI language between sessions
export const LANGUAGE_STORAGE_KEY = 'app.language';

// Map language codes to flag asset codes when they differ
// Example: 'en' should show United Kingdom flag 'gb.svg'
export const LANGUAGE_FLAGS: Record<string, string> = {
  en: 'gb',
  // other languages default to their own code if not specified
};

export function flagFor(lang: string): string {
  return LANGUAGE_FLAGS[lang] ?? lang;
}

/**
 * Safe read of the persisted language from localStorage.
 * Validates against AVAILABLE_LANGS to avoid activating unsupported codes.
 */
export function readPersistedLanguage(): string | null {
  try {
    if (typeof window === 'undefined' || !('localStorage' in window)) return null;
    const stored = window.localStorage.getItem(LANGUAGE_STORAGE_KEY);
    if (stored && (AVAILABLE_LANGS as readonly string[]).includes(stored)) {
      return stored;
    }
    return null;
  } catch {
    return null; // Ignore storage errors (private mode, disabled storage, etc.)
  }
}

/**
 * Persist selected language; failures are silently ignored.
 */
export function persistLanguage(lang: string): void {
  try {
    if (typeof window === 'undefined' || !('localStorage' in window)) return;
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, lang);
  } catch {
    // no-op
  }
}
