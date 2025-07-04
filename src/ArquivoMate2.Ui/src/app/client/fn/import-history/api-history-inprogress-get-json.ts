/* tslint:disable */
/* eslint-disable */
/* Code generated by ng-openapi-gen DO NOT EDIT. */

import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { ImportHistoryListDto } from '../../models/import-history-list-dto';

export interface ApiHistoryInprogressGet$Json$Params {
  Page?: number;
  PageSize?: number;
}

export function apiHistoryInprogressGet$Json(http: HttpClient, rootUrl: string, params?: ApiHistoryInprogressGet$Json$Params, context?: HttpContext): Observable<StrictHttpResponse<ImportHistoryListDto>> {
  const rb = new RequestBuilder(rootUrl, apiHistoryInprogressGet$Json.PATH, 'get');
  if (params) {
    rb.query('Page', params.Page, {});
    rb.query('PageSize', params.PageSize, {});
  }

  return http.request(
    rb.build({ responseType: 'json', accept: 'text/json', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<ImportHistoryListDto>;
    })
  );
}

apiHistoryInprogressGet$Json.PATH = '/api/history/inprogress';
