import { Injectable, computed, inject, signal } from '@angular/core';
import { DocumentsService } from '../client/services/documents.service';
import { ApiDocumentsGet$Json$Params } from '../client/fn/documents/api-documents-get-json';
import { DocumentListDto } from '../client/models/document-list-dto';
import { DocumentListItemDto } from '../client/models/document-list-item-dto';
import { DocumentListDtoApiResponse } from '../client/models/document-list-dto-api-response';
import { Observable, of } from 'rxjs';
import { finalize, map, tap } from 'rxjs/operators';

interface NavigationContext {
  list: DocumentListDto;
  params: ApiDocumentsGet$Json$Params;
}

@Injectable({ providedIn: 'root' })
export class DocumentNavigationService {
  private readonly api = inject(DocumentsService);
  private readonly context = signal<NavigationContext | null>(null);
  private readonly loadingSignal = signal(false);

  readonly isLoading = computed(() => this.loadingSignal());

  updateContext(list: DocumentListDto | null, params: ApiDocumentsGet$Json$Params): void {
    if (!list || !list.documents?.length) {
      this.context.set(null);
      return;
    }

    const currentPage = list.currentPage ?? params.Page ?? 1;
    const nextParams: ApiDocumentsGet$Json$Params = {
      ...params,
      Page: currentPage,
    };

    this.context.set({ list, params: nextParams });
  }

  clear(): void {
    this.context.set(null);
  }

  hasContextFor(currentId: string | null): boolean {
    if (!currentId) return false;
    const ctx = this.context();
    if (!ctx?.list?.documents?.length) return false;
    return ctx.list.documents.some(item => item?.id === currentId);
  }

  canNavigate(currentId: string | null, direction: 1 | -1): boolean {
    if (!currentId) return false;
    const ctx = this.context();
    if (!ctx?.list?.documents?.length) return false;

    const docs = ctx.list.documents ?? [];
    const index = docs.findIndex(doc => doc?.id === currentId);
    if (index === -1) return false;

    if (direction === 1) {
      if (index < docs.length - 1) return true;
      return ctx.list.hasNextPage ?? this.hasAdditionalPage(ctx, direction);
    }

    if (index > 0) return true;
    return ctx.list.hasPreviousPage ?? this.hasAdditionalPage(ctx, direction);
  }

  getNextId(currentId: string): Observable<string | null> {
    return this.resolveNeighbor(currentId, 1).pipe(map(item => item?.id ?? null));
  }

  getPreviousId(currentId: string): Observable<string | null> {
    return this.resolveNeighbor(currentId, -1).pipe(map(item => item?.id ?? null));
  }

  private resolveNeighbor(currentId: string, direction: 1 | -1): Observable<DocumentListItemDto | null> {
    const ctx = this.context();
    if (!ctx?.list?.documents?.length) {
      return of(null);
    }

    const docs = ctx.list.documents ?? [];
    const index = docs.findIndex(doc => doc?.id === currentId);
    if (index === -1) {
      return of(null);
    }

    const targetIndex = index + direction;
    if (targetIndex >= 0 && targetIndex < docs.length) {
      return of(docs[targetIndex] ?? null);
    }

    const currentPage = ctx.list.currentPage ?? ctx.params.Page ?? 1;
    const targetPage = currentPage + (direction > 0 ? 1 : -1);
    if (!this.isPageWithinBounds(ctx, targetPage)) {
      return of(null);
    }

    return this.loadPage(targetPage, ctx).pipe(
      map(list => {
        if (!list?.documents?.length) return null;
        return direction > 0 ? list.documents.find(item => !!item?.id) ?? null : [...list.documents].reverse().find(item => !!item?.id) ?? null;
      })
    );
  }

  private loadPage(page: number, ctx: NavigationContext): Observable<DocumentListDto> {
    const params: ApiDocumentsGet$Json$Params = { ...ctx.params, Page: page };
    this.loadingSignal.set(true);

    return this.api.apiDocumentsGet$Json(params).pipe(
      map((resp: DocumentListDtoApiResponse) => {
        const ok = resp?.success !== false;
        if (!ok) {
          throw new Error(resp?.message || 'Navigation load failed');
        }
        if (!resp?.data || !resp.data.documents?.length) {
          throw new Error('Navigation page empty');
        }
        return resp.data;
      }),
      tap(list => {
        this.context.set({ list, params });
      }),
      finalize(() => this.loadingSignal.set(false))
    );
  }

  private hasAdditionalPage(ctx: NavigationContext, direction: 1 | -1): boolean {
    const pageSize = ctx.params.PageSize ?? ctx.list.documents?.length ?? 0;
    const totalCount = ctx.list.totalCount ?? null;
    if (!pageSize || totalCount === null) return false;
    const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
    const currentPage = ctx.list.currentPage ?? ctx.params.Page ?? 1;
    if (direction > 0) return currentPage < pageCount;
    return currentPage > 1;
  }

  private isPageWithinBounds(ctx: NavigationContext, page: number): boolean {
    if (page < 1) return false;
    const declaredCount = ctx.list.pageCount;
    if (declaredCount && page > declaredCount) return false;

    if (!declaredCount) {
      const pageSize = ctx.params.PageSize ?? ctx.list.documents?.length ?? 0;
      const totalCount = ctx.list.totalCount ?? null;
      if (pageSize && totalCount !== null) {
        const derivedCount = Math.max(1, Math.ceil(totalCount / pageSize));
        if (page > derivedCount) return false;
      }
    }

    return true;
  }
}
