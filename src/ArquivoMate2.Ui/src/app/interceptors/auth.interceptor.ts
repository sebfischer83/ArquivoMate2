import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const oauthService = inject(OAuthService);

  // Nur API-Requests authentifizieren (optional)
  const isApiRequest = req.url.includes('/api/') || req.url.includes('localhost:5000');

  console.log('🔒 Auth Interceptor:', {
    url: req.url,
    isApiRequest,
    hasValidToken: oauthService.hasValidAccessToken()
  });

  // Prüfe, ob ein gültiges Access Token vorhanden ist
  if (isApiRequest && oauthService.hasValidAccessToken()) {
    const token = oauthService.getAccessToken();
    console.log('🔑 Adding token to request:', token?.substring(0, 20) + '...');
    
    // Füge Authorization Header hinzu
    const authReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
    
    return next(authReq);
  }

  console.log('⚠️ No token added to request');
  // Kein Token vorhanden oder kein API-Request - Request unverändert weiterleiten
  return next(req);
};
