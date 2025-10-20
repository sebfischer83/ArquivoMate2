import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface, TuiTitle, TuiDropdown, TuiDropdownOpen } from '@taiga-ui/core';
import { TuiTabs, TuiChip } from '@taiga-ui/kit';
import { DocumentTabsComponent } from './components/document-tabs/document-tabs.component';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PdfJsViewerComponent } from '../../../components/pdfjs-viewer/pdfjs-viewer.component';
import { PdfJsViewerToolbarComponent } from '../../../components/pdfjs-viewer/pdfjs-viewer-toolbar.component';
import { ContentToolbarComponent } from './components/content-toolbar/content-toolbar.component';
import { DocumentHistoryComponent } from '../../../components/document-history/document-history.component';
import { DocumentEventDto } from '../../../client/models/document-event-dto';
import { ActivatedRoute, Router } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDownloadService } from '../../../services/document-download.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { DocumentNoteDtoIEnumerableApiResponse } from '../../../client/models/document-note-dto-i-enumerable-api-response';
import { DocumentNoteDtoApiResponse } from '../../../client/models/document-note-dto-api-response';
import { ToastService } from '../../../services/toast.service';
import { Location } from '@angular/common';
import { DocumentNotesService } from '../../../client/services/document-notes.service';
import { DocumentNoteDto } from '../../../client/models/document-note-dto';
import { NotesListComponent } from './components/notes-list/notes-list.component';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { DocumentNavigationService, DocumentNavigationPage } from '../../../services/document-navigation.service';
import { DocumentListItemDto } from '../../../client/models/document-list-item-dto';
import { combineLatest, firstValueFrom, Subscription } from 'rxjs';

@Component({
  selector: 'app-document',
  imports: [CommonModule, TuiButton, TuiSurface, TuiTitle, TuiTabs, TuiDropdown, TuiDropdownOpen, TuiChip, DocumentHistoryComponent, PdfJsViewerComponent, DocumentTabsComponent, PdfJsViewerToolbarComponent, ContentToolbarComponent, NotesListComponent, TranslocoModule],
  templateUrl: './document.component.html',
  styleUrls: ['./document.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentComponent implements OnInit, OnDestroy {
  readonly tabIndex = signal(0);
  readonly documentId = signal<string | null>(null);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly document = signal<DocumentDto | null>(null);
  readonly hasData = computed(() => !!this.document());
  readonly history = signal<DocumentEventDto[]>([]);
  readonly copiedContent = signal(false);
  readonly wrapContent = signal(true);
  readonly downloadLoading = signal(false);
  readonly downloadMenuOpen = signal(false);
  // Local mutable keywords (UI only, no server persistence yet)
  readonly keywords = signal<string[]>([]);
  readonly newKeyword = signal<string>('');
  // Animation state
  readonly lastAddedKeyword = signal<string | null>(null);
  readonly removalSet = signal<Set<string>>(new Set());
  // Notes state/signals
  readonly notes = signal<DocumentNoteDto[] | null>(null);
  readonly notesLoading = signal(false);
  readonly notesError = signal<string | null>(null);
  readonly addingNote = signal(false);
  private notesLoadedOnce = false;
  readonly navPage = signal<number>(1);
  readonly navIndex = signal<number>(-1);
  readonly navLoading = signal(false);
  readonly hasNavContext = computed(() => this.navigation.hasContext());
  readonly canGoPrevious = computed(() => {
    this.navigation.cacheVersion();
    if (!this.navigation.hasContext()) return false;
    const page = this.navPage();
    const index = this.navIndex();
    if (index > 0) return true;
    const pageData = this.navigation.getCachedPage(page);
    if (pageData?.hasPrev) return true;
    if (page > 1) return true;
    return false;
  });
  readonly canGoNext = computed(() => {
    this.navigation.cacheVersion();
    if (!this.navigation.hasContext()) return false;
    const page = this.navPage();
    const index = this.navIndex();
    if (index < 0) return false;
    const pageData = this.navigation.getCachedPage(page);
    if (pageData) {
      if (index < pageData.items.length - 1) return true;
      if (pageData.hasNext) return true;
    }
    const pageCount = this.navigation.pageCount();
    if (pageCount > 0) return page < pageCount;
    const total = this.navigation.totalCount();
    const size = this.navigation.pageSize();
    if (total > 0 && size > 0) {
      return page < Math.ceil(total / size);
    }
    return false;
  });
  private navOperationId = 0;
  private readonly subscriptions = new Subscription();
  /**
   * Pure original filename (never ID). Derived from potential backend fields.
   * Priority: explicit originalFileName field -> filePath basename -> empty string
   */
  readonly originalFileName = computed(() => {
    const doc = this.document();
    if (!doc) return '';
    const candidate = (doc as any).originalFileName as string | undefined; // allow backend extension
    const raw = candidate || doc.filePath || '';
    if (!raw) return '';
    // Extract last path segment
    let base = raw.split(/[/\\]/).pop() || '';
    // Strip query (?...) and fragment (#...)
    base = base.split('?')[0].split('#')[0];
    return base;
  });

  private safePreview: SafeResourceUrl | null = null;

  constructor(
    private route: ActivatedRoute,
    private api: DocumentsService,
    private notesApi: DocumentNotesService,
    private downloadSrv: DocumentDownloadService,
    private toast: ToastService,
    private location: Location,
    private sanitizer: DomSanitizer,
    private transloco: TranslocoService,
    private router: Router,
    private navigation: DocumentNavigationService,
  ) {}

  back(): void {
    this.location.back();
  }

  ngOnInit(): void {
    const paramAndQuery$ = combineLatest([this.route.paramMap, this.route.queryParamMap]);
    this.subscriptions.add(
      paramAndQuery$.subscribe(([params, query]) => {
        const id = params.get('id');
        const page = this.parsePositiveInt(query.get('page'), 1);
        const fallbackSize = this.navigation.pageSize();
        const pageSize = this.parsePositiveInt(query.get('pageSize'), fallbackSize > 0 ? fallbackSize : 20);
        const search = this.normalizeSearch(query.get('search'));
        this.onRouteStateChange(id, page, pageSize, search);
      })
    );

    this.subscriptions.add(
      this.route.data.subscribe(data => {
        const resolved: DocumentDto | null | undefined = data['document'];
        if (resolved) {
          this.applyDocument(resolved);
        } else {
          const id = this.documentId() ?? this.route.snapshot.paramMap.get('id');
          if (id) {
            this.fetch(id);
          } else {
            this.loading.set(false);
          }
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private onRouteStateChange(id: string | null, page: number, pageSize: number, search: string | null): void {
    const normalizedPage = page > 0 ? page : 1;
    const normalizedPageSize = pageSize > 0 ? pageSize : 20;
    const normalizedSearch = this.normalizeSearch(search);

    this.navigation.ensureContext({ pageSize: normalizedPageSize, search: normalizedSearch });
    this.navPage.set(normalizedPage);

    if (!id) {
      this.documentId.set(null);
      this.resetDocumentState();
      const message = this.transloco.translate('Document.DocumentLoadError');
      this.error.set(message);
      this.loading.set(false);
      return;
    }

    const currentId = this.documentId();
    if (currentId !== id) {
      this.resetDocumentState();
      this.documentId.set(id);
      this.loading.set(true);
      this.error.set(null);
    }

    void this.locateDocument(id, normalizedPage);
  }

  private resetDocumentState(): void {
    this.navOperationId++;
    this.navLoading.set(false);
    this.navIndex.set(-1);
    this.document.set(null);
    this.history.set([]);
    this.keywords.set([]);
    this.newKeyword.set('');
    this.lastAddedKeyword.set(null);
    this.removalSet.set(new Set());
    this.notes.set(null);
    this.notesError.set(null);
    this.notesLoading.set(false);
    this.addingNote.set(false);
    this.notesLoadedOnce = false;
    this.copiedContent.set(false);
    this.wrapContent.set(true);
    this.tabIndex.set(0);
    this.downloadLoading.set(false);
    this.downloadMenuOpen.set(false);
    this.safePreview = null;
  }

  private async locateDocument(id: string, page: number): Promise<void> {
    if (!id) {
      this.navIndex.set(-1);
      return;
    }

    const cached = this.findDocumentInCachedPages(id);
    if (cached) {
      this.navPage.set(cached.page);
      this.navIndex.set(cached.index);
      return;
    }

    const targetPage = page > 0 ? page : 1;
    const pageData = await this.getNavigationPage(targetPage);
    if (!pageData) {
      this.navPage.set(targetPage);
      this.navIndex.set(-1);
      return;
    }
    let index = pageData.items.findIndex(item => item?.id === id);
    if (index < 0) {
      const fallback = this.findDocumentInCachedPages(id);
      if (fallback) {
        this.navPage.set(fallback.page);
        this.navIndex.set(fallback.index);
        return;
      }
    }
    this.navPage.set(targetPage);
    this.navIndex.set(index);
  }

  private findDocumentInCachedPages(id: string): { page: number; index: number } | null {
    const pages = this.navigation.getCachedPages();
    for (const page of pages) {
      const index = page.items.findIndex(item => item?.id === id);
      if (index >= 0) {
        return { page: page.page, index };
      }
    }
    return null;
  }

  private async getNavigationPage(page: number): Promise<DocumentNavigationPage | null> {
    const normalizedPage = page > 0 ? page : 1;
    const cached = this.navigation.getCachedPage(normalizedPage);
    if (cached) {
      return cached;
    }
    const opId = ++this.navOperationId;
    this.navLoading.set(true);
    try {
      const result = await firstValueFrom(this.navigation.ensurePage(normalizedPage));
      if (this.navOperationId !== opId) {
        return null;
      }
      return result;
    } catch (error) {
      if (this.navOperationId === opId) {
        console.error('Failed to load navigation page', error);
      }
      return null;
    } finally {
      if (this.navOperationId === opId) {
        this.navLoading.set(false);
      }
    }
  }

  private hasAnotherPageAfter(page: number): boolean {
    const pageCount = this.navigation.pageCount();
    if (pageCount > 0) {
      return page < pageCount;
    }
    const total = this.navigation.totalCount();
    const size = this.navigation.pageSize();
    if (total > 0 && size > 0) {
      return page < Math.ceil(total / size);
    }
    const pageData = this.navigation.getCachedPage(page);
    return !!pageData?.hasNext;
  }

  private hasPageBefore(page: number): boolean {
    if (page > 1) {
      return true;
    }
    const pageData = this.navigation.getCachedPage(page);
    return !!pageData?.hasPrev;
  }

  private navigateToDocument(item: DocumentListItemDto | null | undefined, page: number, index: number): void {
    const id = item?.id;
    if (!id) {
      return;
    }
    this.navPage.set(page);
    this.navIndex.set(index);
    const queryParams: Record<string, any> = { page };
    const size = this.navigation.pageSize();
    if (size > 0) {
      queryParams['pageSize'] = size;
    }
    const search = this.navigation.search();
    if (search) {
      queryParams['search'] = search;
    }
    void this.router.navigate(['/app/document', id], { queryParams });
  }

  async goToPrevious(): Promise<void> {
    if (!this.canGoPrevious() || this.navLoading()) {
      return;
    }
    const page = this.navPage();
    const index = this.navIndex();
    const currentPage = await this.getNavigationPage(page);
    if (!currentPage) {
      return;
    }
    if (index > 0) {
      const target = currentPage.items[index - 1];
      this.navigateToDocument(target, page, index - 1);
      return;
    }
    if (!this.hasPageBefore(page)) {
      return;
    }
    const previousPageNumber = Math.max(1, page - 1);
    const previousPage = await this.getNavigationPage(previousPageNumber);
    if (!previousPage || previousPage.items.length === 0) {
      return;
    }
    const targetIndex = previousPage.items.length - 1;
    const target = previousPage.items[targetIndex];
    this.navigateToDocument(target, previousPageNumber, targetIndex);
  }

  async goToNext(): Promise<void> {
    if (!this.canGoNext() || this.navLoading()) {
      return;
    }
    const page = this.navPage();
    const index = this.navIndex();
    const currentPage = await this.getNavigationPage(page);
    if (!currentPage) {
      return;
    }
    if (index >= 0 && index < currentPage.items.length - 1) {
      const target = currentPage.items[index + 1];
      this.navigateToDocument(target, page, index + 1);
      return;
    }
    if (!this.hasAnotherPageAfter(page)) {
      return;
    }
    const nextPageNumber = page + 1;
    const nextPage = await this.getNavigationPage(nextPageNumber);
    if (!nextPage || nextPage.items.length === 0) {
      return;
    }
    const target = nextPage.items[0];
    this.navigateToDocument(target, nextPageNumber, 0);
  }

  private parsePositiveInt(value: string | null, fallback: number): number {
    const parsed = Number.parseInt(value ?? '', 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
  }

  private normalizeSearch(value: string | null): string | null {
    const trimmed = (value ?? '').trim();
    return trimmed.length ? trimmed : null;
  }

  private applyDocument(dto: DocumentDto): void {
    this.document.set(dto);
    this.updateSafePreview();
    this.history.set(dto.history ?? []);
    this.keywords.set([...(dto.keywords ?? [])]);
    this.loading.set(false);
    this.error.set(null);
  }

  fetch(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.apiDocumentsIdGet$Json({ id }).subscribe({
      next: (resp: any) => {
        // Expecting ApiResponse<DocumentDto>
        const ok = resp?.success !== false;
        const dto: DocumentDto | null | undefined = resp?.data;
        if (!ok || !dto) {
          this.loading.set(false);
          const key = 'Document.DocumentLoadError';
          const msg = this.transloco.translate(key);
          this.error.set(msg);
          this.toast.error(msg);
          return;
        }
        this.applyDocument(dto);
      },
      error: () => {
        this.loading.set(false);
        const key = 'Document.DocumentLoadError';
        const msg = this.transloco.translate(key);
        this.error.set(msg);
        this.toast.error(msg);
      }
    });
  }

  retry(): void {
    const id = this.documentId();
    if (id) this.fetch(id);
  }

  get filePath(): string | null {
    return this.document()?.filePath || null;
  }

  get previewPath(): string | null {
    const path = (this.document()?.previewPath as string | undefined) || this.filePath;
    if (!path) return null;
    // ensure it's treated as safe resource URL when used in iframe via sanitization upstream if needed
    return path;
  }

  get safePreviewPath(): SafeResourceUrl | null {
    return this.safePreview;
  }

  private updateSafePreview(): void {
    const p = this.previewPath;
    this.safePreview = p ? this.sanitizer.bypassSecurityTrustResourceUrl(p) : null;
  }


  downloadOriginal(): void {
    const path = this.filePath;
    if (!path) return;
    const filename = this.originalFileName() || 'document';
    this.performDownload(path, filename);
  }

  /** Download archived file if available */
  downloadArchive(): void {
    const path = this.document()?.archivePath as string | undefined;
    if (!path) return;
    const base = this.originalFileName() || 'document';
    const filename = base.replace(/(\.[^.]+)?$/, '_archive$1');
    this.performDownload(path, filename);
  }

  /** Shared download implementation to avoid duplication */
  private performDownload(path: string, filename: string): void {
    if (!path || this.downloadLoading()) return;
    this.downloadLoading.set(true);
    this.downloadSrv.download(path).subscribe({
      next: blob => {
        this.downloadLoading.set(false);
        try {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = filename;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          setTimeout(() => URL.revokeObjectURL(url), 4000);
        } catch {
  this.toast.error(this.transloco.translate('Document.DownloadStartError'));
        }
      },
      error: () => {
        this.downloadLoading.set(false);
  this.toast.error(this.transloco.translate('Document.DownloadStartError'));
      }
    });
  }

  onDownloadMenu(open: boolean) {
    this.downloadMenuOpen.set(open);
  }

  closeDownloadMenu() { this.downloadMenuOpen.set(false); }

  openInNewTab(): void {
    const path = this.filePath;
    if (!path) return;
    window.open(path, '_blank');
  }

  copyContent(): void {
    const text = this.document()?.content || '';
    if (!text) return;
    if (navigator?.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(() => this.markContentCopied()).catch(() => this.fallbackCopy(text));
    } else {
      this.fallbackCopy(text);
      this.markContentCopied();
    }
  }

  private markContentCopied() {
    this.copiedContent.set(true);
    setTimeout(() => this.copiedContent.set(false), 2000);
  }

  toggleWrap(): void { this.wrapContent.update(v => !v); }

  // --- Keyword UI actions (no server interaction) ---
  addKeyword(): void {
    const raw = (this.newKeyword() || '').trim();
    if (!raw) return;
    if (raw.length > 30) {
      this.toast.error(this.transloco.translate('Document.KeywordTooLong'));
      return;
    }
    const current = this.keywords();
    if (current.some(k => k.toLowerCase() === raw.toLowerCase())) {
      this.newKeyword.set('');
      return; // ignore duplicates
    }
    this.keywords.set([...current, raw]);
    this.newKeyword.set('');
    this.lastAddedKeyword.set(raw);
    // clear highlight after animation
    setTimeout(() => {
      if (this.lastAddedKeyword() === raw) this.lastAddedKeyword.set(null);
    }, 400);
  }

  removeKeyword(k: string): void {
    // stage removal to allow CSS animation
    if (this.removalSet().has(k)) return;
    this.removalSet.update(set => new Set([...Array.from(set), k]));
    setTimeout(() => {
      this.keywords.set(this.keywords().filter(x => x !== k));
      this.removalSet.update(set => { set.delete(k); return new Set(set); });
      if (this.lastAddedKeyword() === k) this.lastAddedKeyword.set(null);
    }, 180); // matches CSS transition duration
  }

  onKeywordInput(value: string): void { this.newKeyword.set(value); }
  onKeywordKey(event: KeyboardEvent): void {
    if (event.key === 'Enter') { event.preventDefault(); this.addKeyword(); }
  }

  // Adapter for Taiga UI two-way binding syntax if needed
  get activeItemIndex(): number { return this.tabIndex(); }
  set activeItemIndex(i: number) {
    this.tabIndex.set(i);
    if (i === 3 && !this.notesLoadedOnce) this.loadNotes();
  }

  // --- Notes ---
  private loadNotes(): void {
    const id = this.documentId();
    if (!id || this.notesLoading()) return;
    this.notesLoading.set(true);
    this.notesError.set(null);
    this.notesApi.apiDocumentsDocumentIdNotesGet$Json({ documentId: id }).subscribe({
      next: (resp: DocumentNoteDtoIEnumerableApiResponse) => {
        const ok = resp?.success !== false;
        if (!ok) {
          this.notesLoading.set(false);
          this.notesError.set(this.transloco.translate('Document.NotesLoadError'));
          return;
        }
        this.notes.set(resp.data ?? []);
        this.notesLoading.set(false);
        this.notesLoadedOnce = true;
      },
      error: () => { this.notesLoading.set(false); this.notesError.set(this.transloco.translate('Document.NotesLoadError')); }
    });
  }

  retryNotes(): void { this.notesLoadedOnce = false; this.loadNotes(); }

  addNote(text: string): void {
    const id = this.documentId();
    if (!id || !text.trim()) return;
    if (this.addingNote()) return;
    // optimistic add
    const draft: DocumentNoteDto = { id: 'tmp-' + Date.now(), text: text.trim(), createdAt: new Date().toISOString(), documentId: id };
    const current = this.notes() || [];
    this.notes.set([draft, ...current]);
    // increment notesCount on document signal
    const doc = this.document();
    if (doc) { (doc as any).notesCount = (doc as any).notesCount ? (doc as any).notesCount + 1 : 1; this.document.set({ ...doc }); }
    this.addingNote.set(true);
    this.notesApi.apiDocumentsDocumentIdNotesPost$Json({ documentId: id, body: { text: text.trim() } as any }).subscribe({
      next: (resp: DocumentNoteDtoApiResponse) => {
        const ok = resp?.success !== false;
        const saved = ok ? resp.data : null;
        if (!ok || !saved) {
          // revert optimistic insert
          const list = this.notes() || [];
          this.notes.set(list.filter(n => n.id !== draft.id));
          if (doc) { (doc as any).notesCount = Math.max(((doc as any).notesCount || 1) - 1, 0); this.document.set({ ...doc }); }
          this.toast.error(this.transloco.translate('Document.NoteSaveError'));
          this.addingNote.set(false);
          return;
        }
        const list = this.notes() || [];
        this.notes.set(list.map(n => n.id === draft.id ? saved : n));
        this.addingNote.set(false);
        this.toast.success(this.transloco.translate('Document.NoteSaved'));
      },
      error: () => {
        const list = this.notes() || [];
        this.notes.set(list.filter(n => n.id !== draft.id));
        if (doc) { (doc as any).notesCount = Math.max(((doc as any).notesCount || 1) - 1, 0); this.document.set({ ...doc }); }
        this.toast.error(this.transloco.translate('Document.NoteSaveError'));
        this.addingNote.set(false);
      }
    });
  }

  private fallbackCopy(text: string) {
    try {
      const ta = document.createElement('textarea');
      ta.value = text;
      ta.style.position = 'fixed';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      document.execCommand('copy');
      document.body.removeChild(ta);
      this.markContentCopied();
    } catch { /* ignore */ }
  }
}
