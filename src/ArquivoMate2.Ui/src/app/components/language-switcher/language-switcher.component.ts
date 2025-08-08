import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoService } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { TuiDropdown, TuiDropdownOpen } from '@taiga-ui/core';
import { AVAILABLE_LANGS, flagFor } from '../../config/i18n.config';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule, TuiDropdown, TuiDropdownOpen],
  templateUrl: './language-switcher.component.html',
  styleUrl: './language-switcher.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitcherComponent implements OnInit, OnDestroy {
  private transloco = inject(TranslocoService);
  private cdr = inject(ChangeDetectorRef);
  private langSub?: Subscription;

  langs: string[] = [];
  activeLang = this.transloco.getActiveLang();
  open = false;

  constructor() {
  // Use centralized AVAILABLE_LANGS to keep UI and Transloco in sync
  this.langs = [...AVAILABLE_LANGS];
  }

  ngOnInit(): void {
    // Ensure the current selection reflects Transloco's active language
    this.activeLang = this.transloco.getActiveLang();
    this.langSub = this.transloco.langChanges$.subscribe((lang) => {
      this.activeLang = lang as string;
      this.cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.langSub?.unsubscribe();
  }

  onChange(lang: string) {
    if (lang && lang !== this.activeLang) {
  // debug: verify event fires and value is correct
  // eslint-disable-next-line no-console
  console.log('[LanguageSwitcher] change ->', lang);
      this.transloco.setActiveLang(lang);
      this.activeLang = lang;
      this.open = false;
      this.cdr.markForCheck();
    }
  }

  select(lang: string) {
    this.onChange(lang);
  }

  flagSrc(lang: string): string {
    return `assets/taiga-ui/icons/flags/${flagFor(lang)}.svg`;
  }
}
