// ...existing code...
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, signal, computed, inject, Injector } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface, TuiTitle, TuiDropdown, TuiDropdownOpen, TuiDialogService } from '@taiga-ui/core';
import { TuiTabs, TuiChip, TUI_CONFIRM } from '@taiga-ui/kit';
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
import { DocumentNavigationService } from '../../../services/document-navigation.service';
import { Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';

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
  // Pending keyword operations
  readonly keywordAddPending = signal(false);
  readonly keywordPendingSet = signal<Set<string>>(new Set());
  // Notes state/signals
  readonly notes = signal<DocumentNoteDto[] | null>(null);
  readonly notesLoading = signal(false);
  readonly notesError = signal<string | null>(null);
  readonly addingNote = signal(false);
  private notesLoadedOnce = false;
  private readonly destroy$ = new Subject<void>();
  private readonly documentNavigator = inject(DocumentNavigationService);
  private readonly router = inject(Router);
  readonly navigationPending = signal(false);
  readonly acceptPending = signal(false);
  readonly dialogs = inject(TuiDialogService);
  readonly navigationLoading = computed(() => this.documentNavigator.isLoading());
  readonly navigationBusy = computed(() => this.navigationPending() || this.navigationLoading());
  readonly canNavigatePrevious = computed(() => {
    const id = this.documentId();
    if (!id) return false;
    return this.documentNavigator.canNavigate(id, -1);
  });
  readonly canNavigateNext = computed(() => {
    const id = this.documentId();
    if (!id) return false;
    return this.documentNavigator.canNavigate(id, 1);
  });
  readonly hasNavigationContext = computed(() => {
    const id = this.documentId();
    if (!id) return false;
    return this.documentNavigator.hasContextFor(id);
  });
  /**
   * Pure original filename (never ID). Derived from potential backend fields.
   * Priority: explicit originalFileName field -> filePath basename -> empty string
   */
  readonly originalFileName = computed(() => {
    console.log(this.document()); 
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
    return '';
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
  ) {
    this.documentId.set(this.route.snapshot.paramMap.get('id'));
  }

  /** Marks document as accepted by calling backend API and updates UI/toast */
  acceptDocument(): void {
    const id = this.documentId();
    if (!id || this.acceptPending() || this.loading()) return;
    this.acceptPending.set(true);
    // API expects a boolean body to set accepted flag (true = accept)
    this.api.apiDocumentsIdAcceptPatch$Json({ id, body: true }).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        if (!ok) {
          this.toast.error(this.transloco.translate('Document.AcceptError') || 'Fehler beim Akzeptieren');
          this.acceptPending.set(false);
          return;
        }
        // If api returns data, update document; otherwise optimistically set accepted
        if (resp?.data) {
          const doc = this.document();
          if (doc) {
            // merge returned fields into current document
            this.document.set({ ...doc, ...(resp.data ? { accepted: resp.data } : {}) } as DocumentDto);
          }
        } else {
          const doc = this.document();
          if (doc) { doc.accepted = true; this.document.set({ ...doc }); }
        }
        this.toast.success(this.transloco.translate('Document.Accepted') || 'Dokument akzeptiert');
        this.acceptPending.set(false);
      },
      error: () => {
        this.acceptPending.set(false);
        this.toast.error(this.transloco.translate('Document.AcceptError') || 'Fehler beim Akzeptieren');
      }
    });
  }
  

  // trackBy function for keywords ngFor to improve rendering
  trackByKeyword(index: number, item: string): string {
    return item;
  }

  /** Shows a Taiga confirm before accepting the document */
  confirmAccept(): void {
    const msg = this.transloco.translate('Document.AcceptConfirm') || 'Do you really want to accept this document?';

    const yes = this.transloco.translate('Common.Yes') || this.transloco.translate('Document.Yes') || 'Yes';
    const no = this.transloco.translate('Common.No') || this.transloco.translate('Document.No') || 'No';
    this.dialogs
      .open<boolean>(TUI_CONFIRM, {
        label: 'Akzeptieren',
        data: {
          content: msg,
          yes: yes,
          no: no,
        },
      })
      .subscribe((response) => {
        if (response) this.acceptDocument();
      });
  }

  back(): void {
    this.location.back();
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      this.documentId.set(id);
    });

    this.route.data.pipe(takeUntil(this.destroy$)).subscribe(data => {
      const resolved: DocumentDto | null | undefined = data['document'];
      if (resolved) {
        this.applyDocument(resolved);
        return;
      }

      const id = this.documentId();
      if (!id) {
        this.error.set('Keine Dokument-ID.');
        return;
      }
      this.fetch(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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

  private applyDocument(dto: DocumentDto): void {
    this.document.set(dto);
    this.updateSafePreview();
    this.history.set(dto.history ?? []);
    this.keywords.set([...(dto.keywords ?? [])]);
    this.notes.set(null);
    this.notesError.set(null);
    this.notesLoading.set(false);
    this.addingNote.set(false);
    this.notesLoadedOnce = false;
    this.loading.set(false);
    this.error.set(null);
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
    // keep snapshot for potential revert
    const prev = [...current];
    const optimistic = [...prev, raw];
    this.keywords.set(optimistic);
    this.newKeyword.set('');
    this.lastAddedKeyword.set(raw);
    // clear highlight after animation
    setTimeout(() => {
      if (this.lastAddedKeyword() === raw) this.lastAddedKeyword.set(null);
    }, 400);

    // Persist change
    const id = this.documentId();
    if (!id) {
      // revert
      this.keywords.set(prev);
      this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
      return;
    }

  this.keywordAddPending.set(true);
  // API expects field name casing like `Keywords` (server-side expects that key), use that to avoid 400
  this.api.apiDocumentsIdUpdateFieldsPatch$Json({ id, body: { fields: { Keywords: optimistic } } as any }).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        if (!ok) {
          this.keywords.set(prev);
          this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
        } else if (resp?.data) {
          // sync full document if returned
          this.document.set(resp.data);
          // success feedback
          this.toast.success(this.transloco.translate('Document.KeywordSaved') || 'Schlagwort gespeichert');
        }
        this.keywordAddPending.set(false);
      },
      error: () => {
        this.keywords.set(prev);
        this.keywordAddPending.set(false);
        this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
      }
    });
  }

  removeKeyword(k: string): void {
    // stage removal to allow CSS animation
    if (this.removalSet().has(k)) return;
    this.removalSet.update(set => new Set([...Array.from(set), k]));
    // apply visual removal first, then persist
    setTimeout(() => {
      const prev = [...this.keywords()];
      const updated = prev.filter(x => x !== k);
      this.keywords.set(updated);
      // try to persist
      const id = this.documentId();
      if (!id) {
        // restore
        this.keywords.set(prev);
        this.removalSet.update(set => { set.delete(k); return new Set(set); });
        this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
        return;
      }

      // mark as pending (removalSet already indicates animation/pending)
      this.keywordPendingSet.update(s => new Set([...Array.from(s), k]));

  // API expects `Keywords` with upper-case K based on server contract
  this.api.apiDocumentsIdUpdateFieldsPatch$Json({ id, body: { fields: { Keywords: updated } } as any }).subscribe({
        next: (resp: any) => {
          const ok = resp?.success !== false;
          if (!ok) {
            this.keywords.set(prev);
            this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
          } else if (resp?.data) {
            this.document.set(resp.data);
            // success feedback
            this.toast.success(this.transloco.translate('Document.KeywordRemoved') || 'Schlagwort entfernt');
          }
          this.keywordPendingSet.update(s => { s.delete(k); return new Set(s); });
          this.removalSet.update(set => { set.delete(k); return new Set(set); });
        },
        error: () => {
          this.keywords.set(prev);
          this.keywordPendingSet.update(s => { s.delete(k); return new Set(s); });
          this.removalSet.update(set => { set.delete(k); return new Set(set); });
          this.toast.error(this.transloco.translate('Document.KeywordSaveError') || 'Fehler beim Speichern');
        }
      });

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

  goToPrevious(): void {
    this.navigateRelative(-1);
  }

  goToNext(): void {
    this.navigateRelative(1);
  }

  private navigateRelative(direction: 1 | -1): void {
    const currentId = this.documentId();
    if (!currentId) return;
    if (this.navigationBusy()) return;

    const stream = direction > 0 ? this.documentNavigator.getNextId(currentId) : this.documentNavigator.getPreviousId(currentId);
    this.navigationPending.set(true);
    stream.pipe(take(1)).subscribe({
      next: targetId => {
        if (!targetId) {
          this.navigationPending.set(false);
          this.toast.info(this.transloco.translate('Document.NoMoreDocuments'));
          return;
        }
        this.router
          .navigate(['/app/document', targetId], { replaceUrl: true })
          .then(() => this.navigationPending.set(false))
          .catch(() => {
            this.navigationPending.set(false);
            this.toast.error(this.transloco.translate('Document.NavigationError'));
          });
      },
      error: () => {
        this.navigationPending.set(false);
        this.toast.error(this.transloco.translate('Document.NavigationError'));
      },
    });
  }

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
