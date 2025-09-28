import { NG_EVENT_PLUGINS, provideEventPlugins } from "@taiga-ui/event-plugins";
import { provideAnimations } from "@angular/platform-browser/animations";
import { ApplicationConfig, provideAppInitializer, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AuthConfig } from 'angular-oauth2-oidc';
import { routes } from './app.routes';
import { firstValueFrom, of, forkJoin } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { provideZonelessChangeDetection, isDevMode } from '@angular/core';
import { AuthGuard } from "./guards/auth.guard";
import { ApiConfiguration } from './client/api-configuration';
import { authInterceptor } from './interceptors/auth.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';
import { HttpClient } from '@angular/common/http';
import { TranslocoHttpLoader } from './transloco-loader';
import { provideTransloco } from '@jsverse/transloco';
import { AVAILABLE_LANGS } from './config/i18n.config';

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
}

const intializeAppFn = () => {
  const apiConfig = inject(ApiConfiguration);
  const http = inject(HttpClient);

  // Parallel laden: auth + runtime config
  const authCfg$ = http.get<Partial<AuthConfig>>('auth-config.json').pipe(catchError(() => of({}))); 
  const runtimeCfg$ = http.get<RuntimeConfigFile>('runtime-config.json').pipe(catchError(() => of({} as RuntimeConfigFile)));

  return firstValueFrom(forkJoin([authCfg$, runtimeCfg$]))
    .then(([authFile, runtimeFile]) => {
      // API Base URL Priorität: runtime-config.json > auth-config.json(apiBaseUrl?) > default
      const apiBase = runtimeFile.apiBaseUrl || (authFile as any).apiBaseUrl || 'http://localhost:5000';
      apiConfig.rootUrl = apiBase;

      // Merge Auth config
      const authPart = runtimeFile.auth || authFile || {};
      authCodeFlowConfig = { ...defaultAuthConfig, ...authPart, redirectUri: window.location.origin + '/app' };
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
  provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
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
