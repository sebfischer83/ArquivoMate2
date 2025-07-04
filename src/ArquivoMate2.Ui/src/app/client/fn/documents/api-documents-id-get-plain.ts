/* tslint:disable */
/* eslint-disable */
/* Code generated by ng-openapi-gen DO NOT EDIT. */

import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { DocumentDto } from '../../models/document-dto';

export interface ApiDocumentsIdGet$Plain$Params {
  id: string;
}

export function apiDocumentsIdGet$Plain(http: HttpClient, rootUrl: string, params: ApiDocumentsIdGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<DocumentDto>> {
  const rb = new RequestBuilder(rootUrl, apiDocumentsIdGet$Plain.PATH, 'get');
  if (params) {
    rb.path('id', params.id, {});
  }

  return http.request(
    rb.build({ responseType: 'text', accept: 'text/plain', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<DocumentDto>;
    })
  );
}

apiDocumentsIdGet$Plain.PATH = '/api/documents/{id}';
