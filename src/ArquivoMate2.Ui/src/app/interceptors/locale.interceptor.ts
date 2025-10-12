import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

// Adds Accept-Language header to outgoing API requests based on Transloco's active language.
// Respects existing Accept-Language header if already present.
export const localeInterceptor: HttpInterceptorFn = (req, next) => {
  const transloco = inject(TranslocoService);

  // Only add header for API requests (same heuristic as auth.interceptor)
  const isApiRequest = req.url.includes('/api/') || req.url.includes('localhost:5000');

  if (isApiRequest) {
    const lang = (transloco.getActiveLang() as string) || 'en';
    if (!req.headers.has('Accept-Language')) {
      const cloned = req.clone({ headers: req.headers.set('Accept-Language', lang) });
      return next(cloned);
    }
  }

  return next(req);
};
