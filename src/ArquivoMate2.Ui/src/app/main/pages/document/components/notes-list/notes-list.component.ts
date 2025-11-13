import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TuiButton, TuiSurface, TuiExpand } from '@taiga-ui/core';
import { TuiLoader } from '@taiga-ui/core';
import { NoteDatePipe } from '../../../../../utils/note-date.pipe';
import { TranslocoModule } from '@jsverse/transloco';
import { DocumentNoteDto } from '../../../../../client/models/document-note-dto';
import { DocumentNotesService } from '../../../../../client/services/document-notes.service';
import { take } from 'rxjs/operators';

@Component({
  selector: 'app-notes-list',
  standalone: true,
  imports: [CommonModule, FormsModule, TuiButton, TuiSurface, TuiExpand, TuiLoader, NoteDatePipe, TranslocoModule],
  templateUrl: './notes-list.component.html',
  styleUrls: ['./notes-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotesListComponent implements OnChanges {
  // Optional controlled inputs (if parent wants to fully control rendering)
  @Input() notes: DocumentNoteDto[] | null = null;
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() adding = false;
  // New inputs to let the component load its own data when active
  @Input() documentId: string | null = null;
  @Input() active = false;
  // Emit saved note so parent can update document-level counters if needed
  @Output() noteSaved = new EventEmitter<DocumentNoteDto>();

  // Internal flags when self-loading
  private notesLoadedOnce = false;

  draft = '';
  private lastSubmitted = false;
  composerOpen = false;
  get empty(): boolean { return !this.notes || this.notes.length === 0; }

  constructor(private notesApi: DocumentNotesService, private cd: ChangeDetectorRef) {}

  onSubmit(ev: Event) {
    ev.preventDefault();
    const v = this.draft.trim();
    if (!v) return;
    this.addNote(v);
    this.lastSubmitted = true;
    this.draft = '';
  }

  trackId(_i: number, n: DocumentNoteDto) { return n.id; }

  openComposer() { this.composerOpen = true; setTimeout(() => this.focusTextarea(), 0); }
  cancelComposer() { this.composerOpen = false; this.draft = ''; }
  onComposerKey(ev: KeyboardEvent) {
    if (ev.key === 'Enter' && !ev.shiftKey) {
      ev.preventDefault();
      if (!this.adding && this.draft.trim()) {
        this.addNote(this.draft.trim());
        this.lastSubmitted = true;
        this.draft = '';
      }
    }
  }
  isTemp(n: DocumentNoteDto) { return !!n.id && n.id.startsWith('tmp-'); }
  private focusTextarea() {
    const el = (document.querySelector('.note-textarea') as HTMLTextAreaElement | null);
    if (el) el.focus();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['adding'] && !this.adding && this.lastSubmitted) {
      // Save just finished
      this.lastSubmitted = false;
      this.composerOpen = false;
    }

    // When the documentId changes we must reset cached load state so a new document reloads
    if (changes['documentId']) {
      this.notesLoadedOnce = false;
      this.notes = null;
      this.loading = false;
      this.error = null;
      this.adding = false;
    }

    // If the component is active and the documentId changed (or not loaded yet), load notes
    if ((changes['active'] && this.active) || (changes['documentId'] && this.active)) {
      this.loadNotes();
    }
  }

  public loadNotes(): void {
    const id = this.documentId;
    if (!id) return;
    // Avoid reloading repeatedly
    if (this.notesLoadedOnce) return;
    this.loading = true;
    this.error = null;
    this.notesApi.apiDocumentsDocumentIdNotesGet$Json({ documentId: id }).pipe(take(1)).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        if (!ok) {
          this.loading = false;
          this.error = 'load-failed';
          try { this.cd.markForCheck(); } catch {}
          return;
        }
        this.notes = resp.data ?? [];
        this.loading = false;
        this.notesLoadedOnce = true;
        try { this.cd.markForCheck(); } catch {}
      },
      error: () => { this.loading = false; this.error = 'load-failed'; try { this.cd.markForCheck(); } catch {} }
    });
  }

  // Add a new note (internal handler). Performs optimistic insert and posts to API.
  addNote(text: string): void {
    const id = this.documentId;
    if (!id || !text.trim()) return;
    if (this.adding) return;
    const draft: DocumentNoteDto = { id: 'tmp-' + Date.now(), text: text.trim(), createdAt: new Date().toISOString(), documentId: id };
    const current = this.notes || [];
    this.notes = [draft, ...current];
    // increment local adding flag
    this.adding = true;
    this.notesApi.apiDocumentsDocumentIdNotesPost$Json({ documentId: id, body: { text: text.trim() } as any }).pipe(take(1)).subscribe({
      next: (resp: any) => {
        const ok = resp?.success !== false;
        const saved = ok ? resp.data as DocumentNoteDto : null;
        if (!ok || !saved) {
          this.notes = (this.notes || []).filter(n => n.id !== draft.id);
          this.adding = false;
          this.error = 'save-failed';
          return;
        }
        this.notes = (this.notes || []).map(n => n.id === draft.id ? saved : n);
        this.adding = false;
        try { this.cd.markForCheck(); } catch {}
        // notify parent so it can update document-level notesCount
        this.noteSaved.emit(saved);
      },
      error: () => {
        this.notes = (this.notes || []).filter(n => n.id !== draft.id);
        this.adding = false;
        this.error = 'save-failed';
        try { this.cd.markForCheck(); } catch {}
      }
    });
  }
}