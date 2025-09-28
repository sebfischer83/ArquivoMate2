import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TuiButton, TuiSurface, TuiExpand } from '@taiga-ui/core';
import { TuiLoader } from '@taiga-ui/core';
import { NoteDatePipe } from '../../../../../utils/note-date.pipe';
import { TranslocoModule } from '@jsverse/transloco';
import { DocumentNoteDto } from '../../../../../client/models/document-note-dto';

@Component({
  selector: 'app-notes-list',
  standalone: true,
  imports: [CommonModule, FormsModule, TuiButton, TuiSurface, TuiExpand, TuiLoader, NoteDatePipe, TranslocoModule],
  templateUrl: './notes-list.component.html',
  styleUrls: ['./notes-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotesListComponent implements OnChanges {
  @Input() notes: DocumentNoteDto[] | null = null;
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() adding = false;
  @Output() add = new EventEmitter<string>();
  @Output() retry = new EventEmitter<void>();

  draft = '';
  private lastSubmitted = false;
  composerOpen = false;
  get empty(): boolean { return !this.notes || this.notes.length === 0; }

  onSubmit(ev: Event) {
    ev.preventDefault();
    const v = this.draft.trim();
    if (!v) return;
    this.add.emit(v);
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
        this.add.emit(this.draft.trim());
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
  }
}