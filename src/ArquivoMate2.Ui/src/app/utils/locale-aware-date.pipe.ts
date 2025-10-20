import { Pipe, PipeTransform, DestroyRef, inject, ChangeDetectorRef } from '@angular/core';
import { formatDate, registerLocaleData } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import localeDe from '@angular/common/locales/de';
import localeEn from '@angular/common/locales/en';
import localeRu from '@angular/common/locales/ru';

/**
 * Locale-aware date pipe that reacts to Transloco language changes.
 * Use like: {{ someDate | localeDate:'short' }}
 * Standalone so it can be imported into components or declared in a shared module.
 */
@Pipe({ name: 'localeDate', standalone: true, pure: false })
export class LocaleAwareDatePipe implements PipeTransform {
  private transloco = inject(TranslocoService);
  private destroyRef = inject(DestroyRef);
  private lastValue: string | null = null;
  private lastArgs: Array<any> | null = null;
  private onDestroy$ = new Subject<void>();
  private activeLocale: string;
  private cdr = inject(ChangeDetectorRef);

  constructor() {
    // map Transloco lang -> angular locale id
    const map: Record<string, string> = { en: 'en-US', de: 'de', ru: 'ru' };
    this.activeLocale = map[(this.transloco.getActiveLang() as string) ?? 'en'] ?? 'en-US';

  // ensure locale data registered for initial language
  ensureLocaleRegistered(this.activeLocale);

    // react to language changes
    this.transloco.langChanges$.pipe(takeUntil(this.onDestroy$)).subscribe((lang) => {
      const newLocale = map[(lang as string) ?? 'en'] ?? 'en-US';
      ensureLocaleRegistered(newLocale);
      this.activeLocale = newLocale;
      // mark cached value as stale so transform recalculates
      this.lastValue = null;
      // ensure host component updates (works for OnPush)
      try { this.cdr.markForCheck(); } catch { /* ignore if not available */ }
    });

    // ensure we cleanup if DestroyRef supports onDestroy
    if (this.destroyRef) {
      // when the host is destroyed, complete our subject
      (this.destroyRef as any).onDestroy?.(() => this.onDestroy$.next());
    }
  }

  transform(value: Date | string | number | null | undefined, format = 'short', timezone?: string): string | null {
    if (value == null) return null;

    const args = [value, format, timezone ?? undefined, this.activeLocale];

    // fast-path: if identical inputs and we have cached result, return it
    if (this.lastArgs && this.lastValue && shallowArgsEqual(this.lastArgs, args)) {
      return this.lastValue;
    }

    try {
      const dateStr = formatDate(value as any, format, this.activeLocale, timezone);
      this.lastArgs = args;
      this.lastValue = dateStr;
      return dateStr;
    } catch (e) {
      // fallback to toString
      const fallback = (value as any).toString?.() ?? String(value);
      this.lastArgs = args;
      this.lastValue = fallback;
      return fallback;
    }
  }
}

function shallowArgsEqual(a: any[], b: any[]) {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    if (a[i] !== b[i]) return false;
  }
  return true;
}

function ensureLocaleRegistered(locale: string) {
  const key = `__am_loc_reg_${locale}`;
  if ((globalThis as any)[key]) return;
  try {
    if (locale.startsWith('de')) registerLocaleData(localeDe, 'de');
    else if (locale.startsWith('ru')) registerLocaleData(localeRu, 'ru');
    else registerLocaleData(localeEn, 'en-US');
    (globalThis as any)[key] = true;
  } catch (e) {
    // ignore registration failures
    // eslint-disable-next-line no-console
    console.warn('[LocaleAwareDatePipe] Failed to register locale data', e);
  }
}
