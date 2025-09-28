// ...existing code...
import { ChangeDetectionStrategy, Component, OnInit, signal, computed, inject } from '@angular/core';
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
import { ActivatedRoute } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDownloadService } from '../../../services/document-download.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { ToastService } from '../../../services/toast.service';
import { Location } from '@angular/common';
import { DocumentNotesService } from '../../../client/services/document-notes.service';
import { DocumentNoteDto } from '../../../client/models/document-note-dto';
import { NotesListComponent } from './components/notes-list/notes-list.component';
import { TranslocoModule } from '@jsverse/transloco';

@Component({
  selector: 'app-document',
  imports: [CommonModule, TuiButton, TuiSurface, TuiTitle, TuiTabs, TuiDropdown, TuiDropdownOpen, TuiChip, DocumentHistoryComponent, PdfJsViewerComponent, DocumentTabsComponent, PdfJsViewerToolbarComponent, ContentToolbarComponent, NotesListComponent, TranslocoModule],
  templateUrl: './document.component.html',
  styleUrls: ['./document.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentComponent implements OnInit {
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
  ) {
    this.documentId.set(this.route.snapshot.paramMap.get('id'));
  }

  back(): void {
    this.location.back();
  }

  ngOnInit(): void {
    // Resolver supplies data under 'document'
    const resolved: DocumentDto | null | undefined = this.route.snapshot.data['document'];
    if (resolved) {
      console.log(resolved);
  this.document.set(resolved);
  this.updateSafePreview();
      this.history.set(resolved.history ?? []);
      // initialize local keywords copy
      this.keywords.set([...(resolved.keywords ?? [])]);
    } else {
      const id = this.documentId();
      if (!id) {
        this.error.set('Keine Dokument-ID.');
        return;
      }
      this.fetch(id);
    }
  }

  fetch(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.apiDocumentsIdGet$Json({ id }).subscribe({
      next: dto => {
  this.document.set(dto);
  this.updateSafePreview();
        this.history.set(dto.history ?? []);
        this.keywords.set([...(dto.keywords ?? [])]);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        const msg = 'Dokument konnte nicht geladen werden';
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
          this.toast.error('Download konnte nicht gestartet werden');
        }
      },
      error: () => {
        this.downloadLoading.set(false);
        this.toast.error('Download fehlgeschlagen');
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
      this.toast.error('Keyword zu lang (max 30)');
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
      next: list => { this.notes.set(list); this.notesLoading.set(false); this.notesLoadedOnce = true; },
      error: () => { this.notesLoading.set(false); this.notesError.set('Notizen konnten nicht geladen werden'); }
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
      next: saved => {
        const list = this.notes() || [];
        this.notes.set(list.map(n => n.id === draft.id ? saved : n));
        this.addingNote.set(false);
        this.toast.success('Notiz gespeichert');
      },
      error: () => {
        const list = this.notes() || [];
        this.notes.set(list.filter(n => n.id !== draft.id));
        if (doc) { (doc as any).notesCount = Math.max(((doc as any).notesCount || 1) - 1, 0); this.document.set({ ...doc }); }
        this.toast.error('Notiz konnte nicht gespeichert werden');
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
