import { Injectable, computed, signal } from '@angular/core';
import { DocumentsService } from '../client/services/documents.service';
import { DocumentListDto } from '../client/models/document-list-dto';
import { DocumentListItemDto } from '../client/models/document-list-item-dto';
import { DocumentListDtoApiResponse } from '../client/models/document-list-dto-api-response';
import { ApiDocumentsGet$Json$Params } from '../client/fn/documents/api-documents-get-json';
import { Observable, of, throwError } from 'rxjs';
import { map, tap } from 'rxjs/operators';

export interface DocumentNavigationPage {
  page: number;
  items: readonly DocumentListItemDto[];
  hasNext: boolean;
  hasPrev: boolean;
  totalCount: number | null;
  pageCount: number | null;
}

interface NavigationContext {
  pageSize: number;
  search: string | null;
}

@Injectable({ providedIn: 'root' })
export class DocumentNavigationService {
  private readonly contextSignal = signal<NavigationContext | null>(null);
  private readonly totalCountSignal = signal<number>(0);
  private readonly pageCountSignal = signal<number>(0);
  private readonly cacheVersionSignal = signal<number>(0);

  private readonly cachedPages = new Map<number, DocumentNavigationPage>();

  readonly hasContext = computed(() => !!this.contextSignal());
  readonly pageSize = computed(() => this.contextSignal()?.pageSize ?? 0);
  readonly search = computed(() => this.contextSignal()?.search ?? null);
  readonly totalCount = computed(() => this.totalCountSignal());
  readonly pageCount = computed(() => this.pageCountSignal());
  readonly cacheVersion = computed(() => this.cacheVersionSignal());

  constructor(private readonly api: DocumentsService) {}

  prepare(options: { page: number; pageSize: number; search?: string | null; list?: DocumentListDto | null }): void {
    const normalizedSearch = this.normalizeSearch(options.search);
    this.updateContext({ pageSize: options.pageSize, search: normalizedSearch }, true);

    const list = options.list;
    if (list) {
      const pageData = this.createPageFromList(list, options.page);
      this.cachePage(pageData.page, pageData);
    }
  }

  ensureContext(options: { pageSize: number; search?: string | null }): void {
    const normalizedSearch = this.normalizeSearch(options.search);
    const ctx = this.contextSignal();
    if (!ctx || ctx.pageSize !== options.pageSize || ctx.search !== normalizedSearch) {
      this.updateContext({ pageSize: options.pageSize, search: normalizedSearch }, true);
    }
  }

  clear(): void {
    this.contextSignal.set(null);
    this.cachedPages.clear();
    this.bumpCacheVersion();
    this.totalCountSignal.set(0);
    this.pageCountSignal.set(0);
  }

  getCachedPage(page: number): DocumentNavigationPage | null {
    return this.cachedPages.get(page) ?? null;
  }

  getCachedPages(): DocumentNavigationPage[] {
    return Array.from(this.cachedPages.values()).sort((a, b) => a.page - b.page);
  }

  ensurePage(page: number): Observable<DocumentNavigationPage> {
    if (page < 1) {
      return throwError(() => new Error('Page index must be positive.'));
    }
    const cached = this.cachedPages.get(page);
    if (cached) {
      return of({ ...cached, items: [...cached.items] });
    }
    const ctx = this.contextSignal();
    if (!ctx) {
      return throwError(() => new Error('Navigation context is not available.'));
    }
    const params: ApiDocumentsGet$Json$Params = {
      Page: page,
      PageSize: ctx.pageSize,
      Search: ctx.search ?? undefined,
    };
    return this.api.apiDocumentsGet$Json(params).pipe(
      map((resp: DocumentListDtoApiResponse) => {
        if (!resp || resp.success === false) {
          const message = resp?.message ?? 'Failed to load documents page.';
          throw new Error(message);
        }
        const pageData = this.createPageFromList(resp.data, page);
        return pageData;
      }),
      tap(pageData => {
        this.cachePage(pageData.page, pageData);
      })
    );
  }

  private updateContext(ctx: NavigationContext, resetCache: boolean): void {
    this.contextSignal.set(ctx);
    if (resetCache) {
      this.cachedPages.clear();
      this.bumpCacheVersion();
      this.totalCountSignal.set(0);
      this.pageCountSignal.set(0);
    }
  }

  private createPageFromList(list: DocumentListDto | null | undefined, page: number): DocumentNavigationPage {
    const items = [...(list?.documents ?? [])];
    const pageCount = list?.pageCount ?? null;
    const hasNext = list?.hasNextPage ?? (pageCount != null ? page < pageCount : false);
    const hasPrev = list?.hasPreviousPage ?? (page > 1);
    const totalCount = list?.totalCount ?? null;

    const pageData: DocumentNavigationPage = {
      page,
      items,
      hasNext,
      hasPrev,
      totalCount,
      pageCount,
    };

    this.applyTotals(pageData);

    return pageData;
  }

  private cachePage(page: number, data: DocumentNavigationPage): void {
    const copy: DocumentNavigationPage = {
      page,
      items: [...data.items],
      hasNext: data.hasNext,
      hasPrev: data.hasPrev,
      totalCount: data.totalCount,
      pageCount: data.pageCount,
    };
    this.cachedPages.set(page, copy);
    this.bumpCacheVersion();
    this.applyTotals(copy);
  }

  private applyTotals(page: DocumentNavigationPage): void {
    if (typeof page.totalCount === 'number') {
      this.totalCountSignal.set(page.totalCount);
    }
    if (typeof page.pageCount === 'number' && page.pageCount > 0) {
      this.pageCountSignal.set(page.pageCount);
    } else {
      this.recomputePageCount();
    }
  }

  private recomputePageCount(): void {
    const total = this.totalCountSignal();
    const ctx = this.contextSignal();
    if (!ctx || ctx.pageSize <= 0 || total <= 0) {
      if (total === 0) {
        this.pageCountSignal.set(0);
      }
      return;
    }
    const computed = Math.max(1, Math.ceil(total / ctx.pageSize));
    this.pageCountSignal.set(computed);
  }

  private bumpCacheVersion(): void {
    this.cacheVersionSignal.update(v => v + 1);
  }

  private normalizeSearch(value: string | null | undefined): string | null {
    const trimmed = (value ?? '').trim();
    return trimmed.length ? trimmed : null;
  }
}
