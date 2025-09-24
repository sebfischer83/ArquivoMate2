import { HttpErrorResponse, HttpInterceptorFn, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { DevConsole } from '../utils/console';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../services/toast.service';

// Central HTTP error interceptor.
// Responsibility: Normalize error logging and provide a single extension point for future UI toast integration.
export const errorInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const toast = inject<ToastService>(ToastService);
  return next(req).pipe(
    catchError((unknownErr: unknown) => {
      if (unknownErr instanceof HttpErrorResponse) {
        const err = unknownErr; // narrowed
        const { status, url, message } = err;
        // Map status to user-friendly text (basic phase)
        let userMessage = 'Ein unerwarteter Fehler ist aufgetreten.';
        if (status === 0) userMessage = 'Netzwerkfehler oder Server nicht erreichbar.';
        else if (status >= 500) userMessage = 'Serverfehler. Bitte später erneut versuchen.';
        else if (status === 401) userMessage = 'Nicht angemeldet oder Session abgelaufen.';
        else if (status === 403) userMessage = 'Zugriff verweigert.';
        else if (status === 404) userMessage = 'Ressource nicht gefunden.';
        else if (status === 400) userMessage = 'Eingabefehler oder ungültige Anfrage.';
        toast.error(userMessage);
        if (status === 0) {
          DevConsole.error('🌐 Network error / CORS / Server unreachable', { url, message });
        } else if (status >= 500) {
          DevConsole.error('💥 Server error', { status, url, message });
        } else if (status === 401) {
          DevConsole.warn('🔑 Unauthorized (401)', { url });
        } else if (status === 403) {
          DevConsole.warn('⛔ Forbidden (403)', { url });
        } else if (status === 404) {
          DevConsole.warn('🔍 Not Found (404)', { url });
        } else if (status === 400) {
          DevConsole.warn('⚠️ Validation / Bad Request (400)', { url, error: err.error });
        } else {
          DevConsole.warn('⚠️ HTTP error', { status, url, message });
        }
      } else {
        DevConsole.error('Unknown error thrown in HTTP pipeline', unknownErr);
      }
      return throwError(() => unknownErr);
    })
  );
};
