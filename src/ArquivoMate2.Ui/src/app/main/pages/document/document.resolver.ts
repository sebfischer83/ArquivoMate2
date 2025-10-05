import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, ResolveFn } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { DocumentDtoApiResponse } from '../../../client/models/document-dto-api-response';

// Lightweight resolver to fetch a single document before the component is instantiated.
// On error it returns null so the component can show an error state.
export const documentResolver: ResolveFn<DocumentDto | null> = (route: ActivatedRouteSnapshot) => {
  const api = inject(DocumentsService);
  const id = route.paramMap.get('id');
  if (!id) return of(null);
  return api.apiDocumentsIdGet$Json({ id }).pipe(
    map((resp: DocumentDtoApiResponse) => {
      if (resp && resp.success === false) return null;
      return resp?.data ?? null;
    }),
    catchError(() => of(null))
  );
};