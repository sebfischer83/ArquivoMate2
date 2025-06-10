import { Injectable, inject } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { Observable, of } from 'rxjs';
import { filter, switchMap, take } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  private oauthService = inject(OAuthService);

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> | boolean {
    if (this.oauthService.hasValidAccessToken()) {
      return true;
    }

    // Wenn Code in URL, warte auf Token
    if (window.location.search.includes('code=')) {
      return this.waitForToken();
    }

    // Kein Token und kein Code: Login starten
    this.oauthService.initLoginFlow(state.url);
    return false;
  }

  private waitForToken(): Observable<boolean> {
    return this.oauthService.events.pipe(
      filter(e => e.type === 'token_received'),
      take(1),
      switchMap(() => of(this.oauthService.hasValidAccessToken()))
    );
  }
}