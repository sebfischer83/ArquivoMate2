import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { ApiConfiguration } from '../client/api-configuration';

export interface DocumentChatAnswerCitation {
  source?: string | null;
  snippet: string;
}

export interface DocumentChatAnswerReference {
  documentId: string;
  title?: string | null;
  summary?: string | null;
  date?: string | null;
  score?: number | null;
  fileSizeBytes?: number | null;
}

export interface DocumentChatAnswer {
  answer?: string | null;
  model?: string | null;
  citations?: ReadonlyArray<DocumentChatAnswerCitation> | null;
  documents?: ReadonlyArray<DocumentChatAnswerReference> | null;
  documentCount?: number | null;
}

interface DocumentChatQuestionRequest {
  question: string;
  includeHistory: boolean;
}

interface DocumentChatAnswerApiResponse {
  data?: DocumentChatAnswer | null;
  errorCode?: string | null;
  errors?: ({
    [key: string]: Array<string>;
  }) | null;
  message?: string | null;
  success?: boolean;
  timestamp?: string | null;
}

export interface DocumentChatRequestOptions {
  includeHistory?: boolean;
  signal?: AbortSignal;
}

export class DocumentChatError extends Error {
  constructor(message: string, public readonly status?: number, public readonly originalError?: unknown) {
    super(message);
    this.name = 'DocumentChatError';
  }
}

@Injectable({ providedIn: 'root' })
export class DocumentChatService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ApiConfiguration);
  private readonly defaultError = 'Chat request failed.';

  askDocumentQuestion(documentId: string, question: string, options?: DocumentChatRequestOptions): Observable<DocumentChatAnswer> {
    if (!documentId) {
      return throwError(() => new DocumentChatError('A document id is required to send a chat question.'));
    }
    const url = `${this.normalizeUrl(this.config.rootUrl)}/api/documents/${documentId}/chat`;
    return this.sendQuestion(url, question, options);
  }

  askCatalogQuestion(question: string, options?: DocumentChatRequestOptions): Observable<DocumentChatAnswer> {
    const url = `${this.normalizeUrl(this.config.rootUrl)}/api/documents/chat`;
    return this.sendQuestion(url, question, options);
  }

  private sendQuestion(endpoint: string, question: string, options?: DocumentChatRequestOptions): Observable<DocumentChatAnswer> {
    const trimmed = question?.trim() ?? '';
    if (!trimmed) {
      return throwError(() => new DocumentChatError('Please enter a question before starting the chat.'));
    }
    const payload: DocumentChatQuestionRequest = {
      question: trimmed,
      includeHistory: options?.includeHistory ?? false,
    };
    const httpOptions = this.createHttpOptions(options);
    return this.http
      .post<DocumentChatAnswerApiResponse>(endpoint, payload, httpOptions)
      .pipe(
        map(response => this.unwrap(response)),
        catchError(error => this.handleHttpError(error))
      );
  }

  private unwrap(response: DocumentChatAnswerApiResponse): DocumentChatAnswer {
    const success = response.success !== false;
    if (!success) {
      throw new DocumentChatError(response.message || this.defaultError);
    }
    if (!response.data) {
      throw new DocumentChatError('The chatbot returned an empty response.');
    }
    const answer = response.data;
    const citations = (answer.citations ?? []).map(citation => ({
      source: citation?.source ?? null,
      snippet: citation?.snippet ?? '',
    })) as ReadonlyArray<DocumentChatAnswerCitation>;
    const documents = (answer.documents ?? []).map(reference => ({
      documentId: reference?.documentId ?? '',
      title: reference?.title ?? null,
      summary: reference?.summary ?? null,
      date: reference?.date ?? null,
      score: reference?.score ?? null,
      fileSizeBytes: reference?.fileSizeBytes ?? null,
    })) as ReadonlyArray<DocumentChatAnswerReference>;
    return {
      ...answer,
      citations,
      documents,
    };
  }

  private handleHttpError(error: unknown) {
    if (error instanceof DocumentChatError) {
      return throwError(() => error);
    }
    if (error instanceof HttpErrorResponse) {
      const message = this.extractErrorMessage(error) || this.defaultError;
      return throwError(() => new DocumentChatError(message, error.status, error));
    }
    const message = error instanceof Error ? error.message : this.defaultError;
    return throwError(() => new DocumentChatError(message, undefined, error));
  }

  private extractErrorMessage(error: HttpErrorResponse): string | null {
    if (typeof error.error === 'string' && error.error.trim().length) {
      return error.error;
    }
    if (error.error && typeof error.error === 'object') {
      const maybeMessage = (error.error as { message?: unknown }).message;
      if (typeof maybeMessage === 'string' && maybeMessage.trim().length) {
        return maybeMessage;
      }
    }
    return error.message ?? null;
  }

  private createHttpOptions(options?: DocumentChatRequestOptions) {
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    const httpOptions: Record<string, unknown> = { headers };
    if (options?.signal) {
      httpOptions['signal'] = options.signal;
    }
    return httpOptions;
  }

  private normalizeUrl(root: string | undefined | null): string {
    if (!root) {
      return '';
    }
    return root.endsWith('/') ? root.slice(0, -1) : root;
  }
}
