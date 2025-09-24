import { inject, Injectable, signal, computed } from '@angular/core';
import { DocumentsService } from '../client/services/documents.service';
import { DocumentListDto } from '../client/models/document-list-dto';
import { ToastService } from './toast.service';
import { ApiDocumentsGet$Json$Params } from '../client/fn/documents/api-documents-get-json';

// Facade: encapsulates retrieval, transformation & lightweight caching for documents list.
@Injectable({ providedIn: 'root' })
export class DocumentsFacadeService {
  private api = inject(DocumentsService);
  private toast = inject(ToastService);

  private readonly documentsSignal = signal<DocumentListDto | null>(null);
  private readonly loadingSignal = signal<boolean>(false);
  private readonly errorSignal = signal<string | null>(null);
  private lastLoadedAt: number | null = null;
  private readonly ttlMs = 30_000; // simple TTL cache
  private page = signal<number>(1);
  private pageSizeInternal = signal<number>(20);

  readonly documents = computed(() => this.documentsSignal());
  readonly totalCount = computed(() => this.documentsSignal()?.totalCount ?? 0);
  readonly isLoading = computed(() => this.loadingSignal());
  readonly error = computed(() => this.errorSignal());
  readonly currentPage = computed(() => this.page());
  readonly pageSize = computed(() => this.pageSizeInternal());
  readonly totalPages = computed(() => this.documentsSignal()?.pageCount ?? 0);

  load(force = false): void {
    const now = Date.now();
    if (!force && this.lastLoadedAt && now - this.lastLoadedAt < this.ttlMs && this.documentsSignal()) {
      return; // serve from cache
    }
  const params: ApiDocumentsGet$Json$Params = { Page: this.page(), PageSize: this.pageSizeInternal() };
    this.loadingSignal.set(true);
    this.errorSignal.set(null);
    this.api.apiDocumentsGet$Json(params).subscribe({
      next: dto => {
        this.documentsSignal.set(dto);
        this.lastLoadedAt = now;
        this.loadingSignal.set(false);
      },
      error: () => {
        this.loadingSignal.set(false);
        this.errorSignal.set('Dokumente konnten nicht geladen werden.');
        this.toast.error('Dokumente konnten nicht geladen werden.');
      }
    });
  }

  invalidate(): void { this.lastLoadedAt = null; }

  setPage(page: number): void {
    if (page < 1) return;
    this.page.set(page);
    this.invalidate();
    this.load(true);
  }

  setPageSize(size: number): void {
    if (size < 1) return;
    this.pageSizeInternal.set(size);
    this.page.set(1); // reset to first page
    this.invalidate();
    this.load(true);
  }
}
