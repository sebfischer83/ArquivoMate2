import { NG_EVENT_PLUGINS, provideEventPlugins } from "@taiga-ui/event-plugins";
import { provideAnimations } from "@angular/platform-browser/animations";
import { ApplicationConfig, provideAppInitializer, inject, LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AuthConfig } from 'angular-oauth2-oidc';
import { routes } from './app.routes';
import { firstValueFrom, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { provideZonelessChangeDetection, isDevMode } from '@angular/core';
import { AuthGuard } from "./guards/auth.guard";
import { ApiConfiguration } from './client/api-configuration';
import { authInterceptor } from './interceptors/auth.interceptor';
import { localeInterceptor } from './interceptors/locale.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';
import { HttpClient } from '@angular/common/http';
import { TranslocoHttpLoader } from './transloco-loader';
import { provideTransloco } from '@jsverse/transloco';
import { AVAILABLE_LANGS, readPersistedLanguage } from './config/i18n.config';
import { TranslocoService } from '@jsverse/transloco';
import { registerLocaleData } from '@angular/common';
import localeDe from '@angular/common/locales/de';
import localeEn from '@angular/common/locales/en';
import localeRu from '@angular/common/locales/ru';
import { DocumentTypesService } from './client/services/document-types.service';

const defaultAuthConfig: AuthConfig = {
  issuer: 'https://default-issuer.com',
  redirectUri: window.location.origin + '/app',
  clientId: 'default-client-id',
  responseType: 'code',
  scope: 'openid profile email',
  showDebugInformation: true,
  strictDiscoveryDocumentValidation: false,
};

let authCodeFlowConfig: AuthConfig = { ...defaultAuthConfig };

interface RuntimeConfigFile {
  apiBaseUrl?: string;
  auth?: Partial<AuthConfig>;
  version?: string;
}

const intializeAppFn = () => {
  const apiConfig = inject(ApiConfiguration);
  const http = inject(HttpClient);
  const transloco = inject(TranslocoService);
  const docTypesApi = inject(DocumentTypesService);

  // Lade allein runtime-config.json (enthält jetzt apiBaseUrl + auth)
  const runtimeCfg$ = http.get<RuntimeConfigFile>('runtime-config.json').pipe(catchError(() => of({} as RuntimeConfigFile)));

  // Attempt to activate previously stored language early
  const persistedLang = readPersistedLanguage();
  if (persistedLang) {
    transloco.setActiveLang(persistedLang);
  }

  return firstValueFrom(runtimeCfg$)
    .then(runtimeFile => {
      const apiBase = runtimeFile.apiBaseUrl || 'http://localhost:5000';
      apiConfig.rootUrl = apiBase;
      const authPart = runtimeFile.auth || {};
      authCodeFlowConfig = { ...defaultAuthConfig, ...authPart, redirectUri: window.location.origin + '/app' };
      const version = runtimeFile.version || 'unknown';
      // eslint-disable-next-line no-console
      console.info(`[ArquivoMate2] Runtime config loaded (apiBaseUrl=${apiBase}, version=${version})`);
    })
    .then(async () => {
      // After runtime config is set, preload document types once and store in a cheap global cache
      try {
        const resp = await firstValueFrom(docTypesApi.apiDocumentTypesGet$Json().pipe(catchError(() => of(null))));
        // store the plain data array on globalThis for simple reuse
        (globalThis as any).__am_documentTypes = resp?.data ?? null;
        // eslint-disable-next-line no-console
        console.info('[ArquivoMate2] Preloaded document types', Array.isArray((globalThis as any).__am_documentTypes) ? (globalThis as any).__am_documentTypes!.length : 0);
      } catch (e) {
        // eslint-disable-next-line no-console
        console.warn('[ArquivoMate2] Failed to preload document types', e);
        (globalThis as any).__am_documentTypes = null;
      }
    })
    .catch(() => {
      apiConfig.rootUrl = 'http://localhost:5000';
      authCodeFlowConfig = { ...defaultAuthConfig };
    });
};


export function getAuthConfig() {
  return authCodeFlowConfig;
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimations(),
    provideZonelessChangeDetection(),
  provideHttpClient(withInterceptors([authInterceptor, localeInterceptor, errorInterceptor])),
    provideOAuthClient(),
    provideRouter(routes),
    provideAppInitializer(intializeAppFn),
    provideEventPlugins(),
    AuthGuard,
    provideTransloco({
        config: { 
      availableLangs: [...AVAILABLE_LANGS],
          defaultLang: 'en',
          reRenderOnLangChange: true,
          prodMode: !isDevMode(),
        },
        loader: TranslocoHttpLoader
      })
    ,
    // Provide LOCALE_ID based on Transloco active language so Angular DatePipe
    // uses the same locale as the translations. Also register locale data
    // for supported locales (en, de, ru).
    {
      provide: LOCALE_ID,
      useFactory: (transloco: TranslocoService) => {
        const lang = (transloco.getActiveLang() as string) || 'en';
        const map: Record<string, string> = {
          en: 'en-US',
          de: 'de',
          ru: 'ru'
        };
        const locale = map[lang] ?? 'en-US';

        // register locale data lazily (idempotent)
        try {
          // Avoid registering same locale multiple times
          const registeredKey = `__am_registered_${locale}`;
          // Use (globalThis as any) as cheap registry
          if (!(globalThis as any)[registeredKey]) {
            if (locale.startsWith('de')) {
              registerLocaleData(localeDe, 'de');
            } else if (locale.startsWith('ru')) {
              registerLocaleData(localeRu, 'ru');
            } else {
              registerLocaleData(localeEn, 'en-US');
            }
            (globalThis as any)[registeredKey] = true;
          }
        } catch (e) {
          // ignore - registration is best-effort
          // eslint-disable-next-line no-console
          console.warn('[ArquivoMate2] Failed to register locale data', e);
        }

        return locale;
      },
      deps: [TranslocoService]
    }
  ]
};
