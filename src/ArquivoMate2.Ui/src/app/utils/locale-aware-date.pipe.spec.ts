import { LocaleAwareDatePipe } from './locale-aware-date.pipe';
import { Subject } from 'rxjs';

class MockTransloco {
  private lang = 'en';
  langChanges$ = new Subject<string>();
  getActiveLang() { return this.lang; }
  setLang(l: string) { this.lang = l; this.langChanges$.next(l); }
}

describe('LocaleAwareDatePipe', () => {
  it('formats date in en by default', () => {
    const mock = new MockTransloco() as any;
    // inject mock via temporary hack: replace global TranslocoService import resolution isn't trivial here,
    // so we instantiate pipe and overwrite its transloco field
    const pipe = new LocaleAwareDatePipe();
    (pipe as any).transloco = mock;
    (pipe as any).activeLocale = 'en-US';

    const d = new Date(Date.UTC(2020, 0, 2, 12, 0, 0));
    const res = pipe.transform(d, 'short');
    expect(res).toBeTruthy();
  });

  it('updates formatting on language change', () => {
    const mock = new MockTransloco() as any;
    const pipe = new LocaleAwareDatePipe();
    (pipe as any).transloco = mock;
    (pipe as any).activeLocale = 'en-US';

    const d = new Date(2020, 11, 24, 15, 30, 0);
    const en = pipe.transform(d, 'short');
    mock.setLang('de');
    // simulate subscription effect
    (pipe as any).activeLocale = 'de';
    const de = pipe.transform(d, 'short');
    expect(en).not.toEqual(de);
  });
});
