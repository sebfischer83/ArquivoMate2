/* tslint:disable */
/* eslint-disable */
/* Code generated by ng-openapi-gen DO NOT EDIT. */

import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { DocumentDto } from '../../models/document-dto';

export interface ApiDocumentsIdGet$Json$Params {
  id: string;
}

export function apiDocumentsIdGet$Json(http: HttpClient, rootUrl: string, params: ApiDocumentsIdGet$Json$Params, context?: HttpContext): Observable<StrictHttpResponse<DocumentDto>> {
  const rb = new RequestBuilder(rootUrl, apiDocumentsIdGet$Json.PATH, 'get');
  if (params) {
    rb.path('id', params.id, {});
  }

  return http.request(
    rb.build({ responseType: 'json', accept: 'text/json', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<DocumentDto>;
    })
  );
}

apiDocumentsIdGet$Json.PATH = '/api/documents/{id}';
