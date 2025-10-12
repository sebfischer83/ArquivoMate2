import { NG_EVENT_PLUGINS, provideEventPlugins } from "@taiga-ui/event-plugins";
import { provideAnimations } from "@angular/platform-browser/animations";
import { ApplicationConfig, provideAppInitializer, inject } from '@angular/core';
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
  ]
};
