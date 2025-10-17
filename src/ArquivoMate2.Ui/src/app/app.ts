import { TUI_DARK_MODE, TuiDropdownService, TuiRoot } from "@taiga-ui/core";
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OAuthEvent, OAuthSuccessEvent, OAuthService } from "angular-oauth2-oidc";
import { getAuthConfig } from "./app.config";
import { tuiAsPortal } from "@taiga-ui/cdk";
import { HttpClient } from '@angular/common/http';
import { ApiConfiguration } from './client/api-configuration';
import { filter, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TuiRoot],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  providers: [TuiDropdownService, tuiAsPortal(TuiDropdownService)],
})
export class App {
  protected title = 'ArquivoMate2.Ui';
  protected readonly darkMode = inject(TUI_DARK_MODE);

  private cookieExchangeInFlight = false;
  private cookieEstablished = false;

  constructor(
    private oauthService: OAuthService,
    private http: HttpClient,
    private apiConfiguration: ApiConfiguration,
  ) {
    this.oauthService.configure(getAuthConfig());
    this.oauthService.events
      .pipe(filter((event: OAuthEvent) =>
        event instanceof OAuthSuccessEvent
      ))
      .subscribe(() => {
        this.cookieEstablished = false;
        this.exchangeTokenForCookie();
      });

    this.oauthService.loadDiscoveryDocument().then(() => {
      this.oauthService.tryLoginCodeFlow().then(() => {
        this.exchangeTokenForCookie();
      });
    });
  }

  private exchangeTokenForCookie(): void {
    if (!this.oauthService.hasValidAccessToken() || this.cookieExchangeInFlight || this.cookieEstablished) {
      return;
    }

    this.cookieExchangeInFlight = true;

    const endpoint = new URL('/api/users/login', this.apiConfiguration.rootUrl).toString();

  const claims = (this.oauthService.getIdentityClaims() as any) || {};
  const name = claims.preferred_username || claims.name || claims.sub || '';
  const body = { name };

  this.http.post(endpoint, body, { withCredentials: true, observe: 'response' })
      .pipe(finalize(() => {
        this.cookieExchangeInFlight = false;
      }))
      .subscribe({
        next: () => {
          this.cookieEstablished = true;
        },
        error: (error) => {
          console.warn('⚠️ Failed to exchange access token for cookie session', error);
        }
      });
  }
}