import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ApiConfiguration } from '../client/api-configuration';
import { Observable, map } from 'rxjs';

/**
 * Separater Service für Datei-Downloads um den generierten Client nicht zu verändern.
 * Unterstützt relative und absolute Pfade. Auth-Header werden durch Interceptor (auth.interceptor) automatisch gesetzt.
 */
@Injectable({ providedIn: 'root' })
export class DocumentDownloadService {
  private http = inject(HttpClient);
  private config = inject(ApiConfiguration);

  download(filePath: string): Observable<Blob> {
    const isAbsolute = /^https?:\/\//i.test(filePath);
    const url = isAbsolute ? filePath : `${this.config.rootUrl.replace(/\/$/, '')}/${filePath.replace(/^\//, '')}`;
    return this.http.get(url, { responseType: 'blob', observe: 'response' }).pipe(
      map(resp => {
        if (!resp.body) throw new Error('Leerer Download');
        return resp.body;
      })
    );
  }
}
