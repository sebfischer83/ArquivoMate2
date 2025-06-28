import { NG_EVENT_PLUGINS, provideEventPlugins } from "@taiga-ui/event-plugins";
import { provideAnimations } from "@angular/platform-browser/animations";
import { ApplicationConfig, provideAppInitializer, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AuthConfig } from 'angular-oauth2-oidc';
import { routes } from './app.routes';
import { firstValueFrom } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { AuthGuard } from "./guards/auth.guard";
import { ApiConfiguration } from './client/api-configuration';
import { authInterceptor } from './interceptors/auth.interceptor';
import { HttpClient } from '@angular/common/http';

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

const intializeAppFn = () => {
  const apiConfig = inject(ApiConfiguration);
  apiConfig.rootUrl = 'http://localhost:5000'; 

  const http = inject(HttpClient);
  return firstValueFrom(http.get<Partial<AuthConfig>>('auth-config.json'))
    .then(config => {
      if (config) {
        authCodeFlowConfig = { ...defaultAuthConfig, ...config };
      } else {
        authCodeFlowConfig = { ...defaultAuthConfig };
      }
    });
};


export function getAuthConfig() {
  return authCodeFlowConfig;
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideAnimations(),
    provideZonelessChangeDetection(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideOAuthClient(),
    provideRouter(routes),
    provideAppInitializer(intializeAppFn),
    provideEventPlugins(),
    AuthGuard
  ]
};
