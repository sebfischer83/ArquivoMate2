// ...existing code...
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, signal, computed, inject, Injector, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface, TuiTitle, TuiDropdown, TuiDropdownOpen, TuiDialogService, TuiTextfield, TuiTextfieldDropdownDirective, TuiLabel, TuiHint, TuiDataList } from '@taiga-ui/core';
import { TuiTabs, TuiChip, TuiDataListWrapper, TUI_CONFIRM, TuiComboBox } from '@taiga-ui/kit';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { DocumentTabsComponent } from './components/document-tabs/document-tabs.component';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PdfJsViewerComponent } from '../../../components/pdfjs-viewer/pdfjs-viewer.component';
import { PdfJsViewerToolbarComponent } from '../../../components/pdfjs-viewer/pdfjs-viewer-toolbar.component';
import { ContentToolbarComponent } from './components/content-toolbar/content-toolbar.component';
import { DocumentHistoryComponent } from '../../../components/document-history/document-history.component';
import { LabResultsComponent } from './components/features/lab-results/lab-results.component';
import { DocumentEventDto } from '../../../client/models/document-event-dto';
import { ActivatedRoute, Router } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDownloadService } from '../../../services/document-download.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { DocumentTypeDto } from '../../../client/models/document-type-dto';
import { ToastService } from '../../../services/toast.service';
import { Location } from '@angular/common';
import { DocumentTypesService } from '../../../client/services/document-types.service';
import { DocumentNoteDto } from '../../../client/models/document-note-dto';
import { NotesListComponent } from './components/notes-list/notes-list.component';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { DocumentNavigationService } from '../../../services/document-navigation.service';
import { Subject, of } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { CollectionsService } from '../../../client/services/collections.service';
import { CollectionDto } from '../../../client/models/collection-dto';

@Component({
  selector: 'app-document',
  imports: [CommonModule, ReactiveFormsModule, TuiButton, TuiSurface, TuiTitle, TuiTabs, TuiDropdown, TuiDropdownOpen, TuiChip, TuiTextfield, TuiTextfieldDropdownDirective, TuiDataList, TuiDataListWrapper, TuiComboBox, TuiLabel, TuiHint, DocumentHistoryComponent, PdfJsViewerComponent, DocumentTabsComponent, PdfJsViewerToolbarComponent, ContentToolbarComponent, NotesListComponent, LabResultsComponent, TranslocoModule],
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
  // Notes are handled inside NotesListComponent now
  // document types loaded from server
  readonly documentTypes = signal<DocumentTypeDto[] | null>(null);
  readonly documentTypesLoading = signal(false);
  readonly documentTypesError = signal<string | null>(null);
  // convenience list of type names for UI (used by combo-box)
  readonly documentTypeNames = computed(() => (this.documentTypes() ?? []).map(t => t?.name ?? ''));
  // Global edit mode state
  readonly editMode = signal(false);
  // Indicates a save operation in progress (document fields + lab results)
  readonly isSaving = signal(false);
  // Buffer for editing fields before saving
  readonly editBuffer = signal<{ title?: string | null; type?: string | null }>({});
  // Form control used to provide NgControl for Taiga ComboBox and to sync value with editBuffer
  readonly editTypeControl = new FormControl<string | null>(null);
  // Collections management
  readonly availableCollections = signal<CollectionDto[]>([]);
  readonly collectionsLoading = signal(false);
  readonly selectedCollectionIds = signal<string[]>([]);
  readonly collectionsSaving = signal(false);
  // (notes lifecycle moved to NotesListComponent)
  // Handler for child noteSaved event
  onNoteSaved(saved: DocumentNoteDto): void {
    const doc = this.document();
    if (!doc) return;
    (doc as any).notesCount = (doc as any).notesCount ? (doc as any).notesCount + 1 : 1;
    this.document.set({ ...doc });
  }
  private readonly destroy$ = new Subject<void>();
  private readonly documentNavigator = inject(DocumentNavigationService);
  private readonly router = inject(Router);
  readonly navigationPending = signal(false);
  readonly acceptPending = signal(false);
  readonly dialogs = inject(TuiDialogService);
  readonly navigationLoading = computed(() => this.documentNavigator.isLoading());
  readonly navigationBusy = computed(() => this.navigationPending() || this.navigationLoading());
  // lab results are handled inside the dedicated LabResultsComponent
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
    // console.log(this.document()); 
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
    
    private downloadSrv: DocumentDownloadService,
    private toast: ToastService,
    private location: Location,
    private sanitizer: DomSanitizer,
    private transloco: TranslocoService,
    private docTypesApi: DocumentTypesService,
    private collectionsApi: CollectionsService,
  ) {
    this.documentId.set(this.route.snapshot.paramMap.get('id'));
  }

  @ViewChild(LabResultsComponent) labResultsComponent?: LabResultsComponent;

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

    // Keep editBuffer.type in sync with the form control value
    this.editTypeControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(v => {
      this.editBuffer.update(b => ({ ...b, type: v ?? null }));
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
    this.loading.set(false);
    this.error.set(null);
    // Load available document types when displaying a document
    this.loadDocumentTypes();
    // Load available collections
    this.loadCollections();
    // Set selected collections from document
    const collectionIds = (dto.collectionRefs ?? []).map(c => c.id).filter(Boolean) as string[];
    this.selectedCollectionIds.set(collectionIds);
    // reset edit buffer and mode
    this.editMode.set(false);
    this.editBuffer.set({});
    // Ensure the active tab is available for the newly loaded document.
    // If the user was on a feature tab that the new document does not provide (e.g. lab-results),
    // switch to the first available tab to avoid an empty content area.
    try {
      const available = [0, 1, 2, 3]; // properties, content, history, notes
      if (dto?.documentTypeSystemFeatures?.includes && dto.documentTypeSystemFeatures.includes('lab-results')) {
        available.push(4);
      }
      const current = this.tabIndex();
      if (!available.includes(current)) {
        this.tabIndex.set(available[0] ?? 0);
      }
    } catch (e) {
      // non-blocking: if anything goes wrong, keep default behavior
    }
    // keep initial tab at index 0 (do not auto-activate lab-results)
    // Tabs are now responsible for loading their own data when the document changes.
    // No manual child refresh calls here to keep parent logic simple.
  }

  startEdit(): void {
    const doc = this.document();
    if (!doc) return;
    this.editBuffer.set({ title: doc.title ?? null, type: doc.type ?? null });
    this.editMode.set(true);
    // initialize form control without emitting change to valueChanges subscription
    this.editTypeControl.setValue(doc.type ?? null, { emitEvent: false });
  }

  cancelEdit(): void {
    this.editMode.set(false);
    this.editBuffer.set({});
  }

  saveEdit(): void {
    const id = this.documentId();
    if (!id) return;
    const buffer = this.editBuffer();
    const fields: any = {};
    const current = this.document();
    // Only include fields that actually changed compared to the current document
    if (buffer.title !== undefined) {
      const origTitle = current?.title ?? null;
      if (buffer.title !== origTitle) fields.Title = buffer.title;
    }
    if (buffer.type !== undefined) {
      const origType = current?.type ?? null;
      if (buffer.type !== origType) fields.Type = buffer.type;
    }

    const finalize = () => {
      this.isSaving.set(false);
      this.editMode.set(false);
      this.editBuffer.set({});
    };
    // If nothing changed on document fields, still try to commit lab-result edits
    if (Object.keys(fields).length === 0) {
      this.isSaving.set(true);
      const commit$ = this.labResultsComponent ? this.labResultsComponent.commitEdits() : of(null);
      (commit$ as any).subscribe({
        next: () => finalize(),
        error: (err: any) => {
          this.toast.error(this.transloco.translate('Document.LabResultsSaveError') || 'Fehler beim Speichern der Laborwerte');
          finalize();
        }
      });
      return;
    }

    // call update-fields endpoint
    this.isSaving.set(true);
    this.api.apiDocumentsIdUpdateFieldsPatch$Json({ id, body: { fields } as any }).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        if (!ok) {
          this.isSaving.set(false);
          this.toast.error(this.transloco.translate('Document.SaveError') || 'Fehler beim Speichern');
          return;
        }
        if (resp?.data) {
          this.document.set(resp.data as DocumentDto);
        } else {
          // optimistic update locally
          const doc = this.document();
          if (doc) {
            if (fields.Title !== undefined) doc.title = fields.Title;
            if (fields.Type !== undefined) doc.type = fields.Type;
            this.document.set({ ...doc });
          }
        }
        this.toast.success(this.transloco.translate('Document.Saved') || 'Gespeichert');

        // commit lab result edits and finalize when done
        const commit$ = this.labResultsComponent ? this.labResultsComponent.commitEdits() : of(null);
        (commit$ as any).subscribe({
          next: () => finalize(),
          error: (err: any) => {
            this.toast.error(this.transloco.translate('Document.LabResultsSaveError') || 'Fehler beim Speichern der Laborwerte');
            finalize();
          }
        });
      },
      error: () => {
        this.isSaving.set(false);
        this.toast.error(this.transloco.translate('Document.SaveError') || 'Fehler beim Speichern');
      }
    });
  }

  // Helpers used from template to update the edit buffer (avoid complex expressions in template)
  setEditTitle(value: string | null): void {
    this.editBuffer.update(b => ({ ...b, title: value }));
  }

  setEditType(value: unknown): void {
    // Accept either a raw string (from a data-list item) or an input event/value.
    let v: string | null = null;
    if (value == null) v = null;
    else if (typeof value === 'string') v = value;
    else if ((value as any)?.target) {
      // event from an <input> element
      v = (value as any).target?.value ?? null;
    } else {
      // fallback: try to stringify
      try { v = String(value); } catch { v = null; }
    }

    this.editBuffer.update(b => ({ ...b, type: v }));
  }

  

  // (computed signal `documentTypeNames` is defined earlier) — template calls documentTypeNames()

  private loadDocumentTypes(): void {
    // If already loading, skip
    if (this.documentTypesLoading()) return;
    // Check global cache populated at app initialization
    const cached = (globalThis as any).__am_documentTypes as DocumentTypeDto[] | null | undefined;
    if (Array.isArray(cached) && cached.length) {
      this.documentTypes.set(cached as DocumentTypeDto[]);
      this.documentTypesLoading.set(false);
      this.documentTypesError.set(null);
      return;
    }

    this.documentTypesLoading.set(true);
    this.documentTypesError.set(null);
    this.docTypesApi.apiDocumentTypesGet$Json().pipe(take(1)).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        const list = resp?.data ?? [];
        if (!ok) {
          this.documentTypesError.set(this.transloco.translate('Document.TypesLoadError'));
          this.documentTypesLoading.set(false);
          return;
        }
        // set component signal and update global cache for other consumers
        this.documentTypes.set(list as DocumentTypeDto[]);
        (globalThis as any).__am_documentTypes = list;
        this.documentTypesLoading.set(false);
      },
      error: () => {
        this.documentTypesLoading.set(false);
        this.documentTypesError.set(this.transloco.translate('Document.TypesLoadError'));
      }
    });
  }

  private loadCollections(): void {
    if (this.collectionsLoading()) return;
    this.collectionsLoading.set(true);
    this.collectionsApi.apiCollectionsGet$Json().pipe(take(1)).subscribe({
      next: (resp: { success?: boolean; data?: CollectionDto[] }) => {
        const ok = resp?.success !== false;
        const list = resp?.data ?? [];
        if (ok) {
          this.availableCollections.set(list as CollectionDto[]);
        }
        this.collectionsLoading.set(false);
      },
      error: () => {
        this.collectionsLoading.set(false);
        this.toast.error(this.transloco.translate('Document.CollectionsLoadError') || 'Fehler beim Laden der Sammlungen');
      }
    });
  }

  public saveCollections(): void {
    const doc = this.document();
    const docId = this.documentId();
    if (!doc || !docId) return;
    
    const currentIds = (doc.collectionRefs ?? []).map(c => c.id).filter(Boolean) as string[];
    const selectedIds = this.selectedCollectionIds();
    
    const toAdd = selectedIds.filter(id => !currentIds.includes(id));
    const toRemove = currentIds.filter(id => !selectedIds.includes(id));
    
    if (toAdd.length === 0 && toRemove.length === 0) {
      return; // No changes
    }
    
    this.collectionsSaving.set(true);
    
    // Execute all operations
    let totalOps = toAdd.length + toRemove.length;
    let completed = 0;
    let hasError = false;
    
    const checkCompletion = () => {
      completed++;
      if (completed === totalOps) {
        this.collectionsSaving.set(false);
        if (!hasError) {
          this.toast.success(this.transloco.translate('Document.CollectionsSaved') || 'Sammlungen gespeichert');
          // Reload document to get updated collectionRefs
          const id = this.documentId();
          if (id) this.fetch(id);
        } else {
          this.toast.error(this.transloco.translate('Document.CollectionsSaveError') || 'Fehler beim Speichern der Sammlungen');
        }
      }
    };
    
    // Process removals
    toRemove.forEach(collectionId => {
      this.collectionsApi.apiCollectionsIdDocumentsDocumentIdDelete({ 
        id: collectionId, 
        documentId: docId 
      }).subscribe({
        next: () => checkCompletion(),
        error: () => {
          hasError = true;
          checkCompletion();
        }
      });
    });
    
    // Process additions
    toAdd.forEach(collectionId => {
      this.collectionsApi.apiCollectionsIdAssignPost$Json({
        id: collectionId,
        body: { documentIds: [docId], collectionId }
      }).subscribe({
        next: () => checkCompletion(),
        error: () => {
          hasError = true;
          checkCompletion();
        }
      });
    });
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

  /** Copy the current document ID to the clipboard when user double-clicks the ID */
  copyId(): void {
    const id = this.document()?.id;
    if (!id) return;
    const text = String(id);
    if (navigator?.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(() => {
        this.toast.success(this.transloco.translate('Document.IdCopied') || 'ID kopiert');
      }).catch(() => {
        this.fallbackCopy(text);
        this.toast.success(this.transloco.translate('Document.IdCopied') || 'ID kopiert');
      });
    } else {
      this.fallbackCopy(text);
      this.toast.success(this.transloco.translate('Document.IdCopied') || 'ID kopiert');
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
  }

  // Stringify function for collections multi-select
  readonly stringify = (item: CollectionDto): string => item?.name ?? '';

  toggleCollection(id: string): void {
    const current = this.selectedCollectionIds();
    if (current.includes(id)) {
      this.selectedCollectionIds.set(current.filter(cid => cid !== id));
    } else {
      this.selectedCollectionIds.set([...current, id]);
    }
  }

  // Notes are handled by NotesListComponent now.

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

  // parent does not manage notes anymore

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
