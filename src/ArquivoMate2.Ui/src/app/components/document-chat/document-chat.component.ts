import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, Input, OnDestroy, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TuiButton, TuiHint, TuiLoader } from '@taiga-ui/core';
import { TuiTextarea } from '@taiga-ui/kit/components/textarea';
import { TuiSwitch } from '@taiga-ui/kit/components/switch';
import { finalize, Subscription } from 'rxjs';
import { DocumentDto, DocumentEventDto } from '../../client/models';
import { DocumentHistoryComponent } from '../document-history/document-history.component';
import {
  DocumentChatAnswerCitation,
  DocumentChatAnswerReference,
  DocumentChatError,
  DocumentChatRequestOptions,
  DocumentChatService,
} from '../../services/document-chat.service';

interface DocumentChatMessage {
  id: string;
  kind: 'question' | 'answer' | 'system';
  text: string;
  timestamp: Date;
  citations?: ReadonlyArray<DocumentChatAnswerCitation>;
  documents?: ReadonlyArray<DocumentChatAnswerReference>;
  model?: string;
  documentCount?: number | null;
}

@Component({
  selector: 'am-document-chat',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TuiTextarea, TuiSwitch, TuiButton, TuiHint, TuiLoader, DocumentHistoryComponent],
  templateUrl: './document-chat.component.html',
  styleUrls: ['./document-chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentChatComponent implements OnDestroy {
  private readonly chatService = inject(DocumentChatService);

  readonly questionMaxLength = 2000;
  private readonly defaultUiError = 'The chatbot request could not be completed.';

  private readonly documentSignal = signal<DocumentDto | null>(null);
  private readonly historySignal = signal<ReadonlyArray<DocumentEventDto>>([]);
  private readonly scopeSignal = signal<'document' | 'catalog'>('document');
  private historyDefaultValue = false;

  private readonly messagesSignal = signal<DocumentChatMessage[]>([]);
  protected readonly messages = computed(() => this.messagesSignal());

  readonly questionControl = new FormControl<string>('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(this.questionMaxLength)],
  });
  readonly includeHistoryControl = new FormControl<boolean>(false, { nonNullable: true });
  readonly form = new FormGroup({
    question: this.questionControl,
    includeHistory: this.includeHistoryControl,
  });

  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  private readonly includeHistoryValue = signal<boolean>(this.includeHistoryControl.value);

  private includeHistoryTouched = false;
  private readonly includeHistorySubscription: Subscription;
  private activeSubscription: Subscription | null = null;
  private currentAbortController: AbortController | null = null;

  @ViewChild('messagesContainer') private messagesContainer?: ElementRef<HTMLDivElement>;

  private readonly autoScroll = effect(() => {
    this.messagesSignal();
    if (!this.messagesContainer) {
      return;
    }
    queueMicrotask(() => {
      const element = this.messagesContainer?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  });

  constructor() {
    this.includeHistorySubscription = this.includeHistoryControl.valueChanges.subscribe(value => {
      this.includeHistoryTouched = true;
      this.includeHistoryValue.set(value);
    });
  }

  @Input()
  set document(value: DocumentDto | null | undefined) {
    this.documentSignal.set(value ?? null);
  }

  @Input()
  set history(value: ReadonlyArray<DocumentEventDto> | null | undefined) {
    const normalized = Array.isArray(value)
      ? value.filter((event): event is DocumentEventDto => !!event)
      : [];
    this.historySignal.set(normalized);
    if (!normalized.length) {
      this.setIncludeHistory(false);
      return;
    }
    this.syncHistoryToggle();
  }

  @Input()
  set historyDefault(value: boolean | null | undefined) {
    this.historyDefaultValue = !!value;
    this.syncHistoryToggle();
  }

  @Input()
  set scope(value: 'document' | 'catalog' | null | undefined) {
    this.scopeSignal.set(value === 'catalog' ? 'catalog' : 'document');
  }

  ngOnDestroy(): void {
    if (this.currentAbortController) {
      this.currentAbortController.abort();
    }
    this.cancelActiveRequestOnly();
    this.includeHistorySubscription.unsubscribe();
    this.autoScroll.destroy();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const question = this.questionControl.value.trim();
    if (!question) {
      this.questionControl.setValue('');
      return;
    }
    const includeHistory = this.includeHistoryValue();
    const scope = this.scopeSignal();
    const documentId = this.documentSignal()?.id ?? null;

    if (scope === 'document' && !documentId) {
      const message = 'Document information is missing for this chat.';
      this.errorMessage.set(message);
      this.appendSystemMessage(message);
      return;
    }

    this.errorMessage.set(null);
    this.appendQuestionMessage(question);
    this.questionControl.setValue('');

    this.cancelActiveRequestOnly();

    const options: DocumentChatRequestOptions = { includeHistory };
    const abortController = new AbortController();
    options.signal = abortController.signal;
    this.currentAbortController = abortController;

    const request$ = scope === 'document'
      ? this.chatService.askDocumentQuestion(documentId!, question, options)
      : this.chatService.askCatalogQuestion(question, options);

    this.isLoading.set(true);
    this.activeSubscription = request$
      .pipe(finalize(() => {
        this.isLoading.set(false);
        this.currentAbortController = null;
        this.activeSubscription = null;
      }))
      .subscribe({
        next: answer => this.appendAnswerMessage(answer.answer, answer.model, answer.citations ?? [], answer.documents ?? [], answer.documentCount ?? null),
        error: err => this.handleError(err),
      });
  }

  protected cancel(): void {
    if (this.currentAbortController) {
      this.currentAbortController.abort();
    }
    this.cancelActiveRequestOnly();
    if (this.isLoading()) {
      this.isLoading.set(false);
      this.appendSystemMessage('Chat request cancelled.');
    }
  }

  protected hasHistory(): boolean {
    return this.historySignal().length > 0;
  }

  protected historyPreviewVisible(): boolean {
    return this.hasHistory() && this.includeHistoryValue();
  }

  protected messagesEmpty(): boolean {
    return this.messages().length === 0;
  }

  // Renamed from `scope` to `currentScope` to avoid conflict with the @Input() setter named `scope`
  protected currentScope(): 'document' | 'catalog' {
    return this.scopeSignal();
  }

  protected trackByMessage = (_: number, message: DocumentChatMessage) => message.id;
  protected trackByCitation = (_: number, citation: DocumentChatAnswerCitation) => `${citation.source ?? 'source'}-${citation.snippet}`;
  protected trackByReference = (index: number, reference: DocumentChatAnswerReference) => reference.documentId || `reference-${index}`;

  protected roleLabel(kind: DocumentChatMessage['kind']): string {
    switch (kind) {
      case 'question':
        return 'You';
      case 'answer':
        return 'Assistant';
      default:
        return 'System';
    }
  }

  protected placeholder(): string {
    return this.currentScope() === 'catalog'
      ? 'Ask a question about your documents…'
      : 'Ask a question about this document…';
  }

  protected introTitle(): string {
    return this.currentScope() === 'catalog' ? 'Document catalogue chat' : 'Document chat';
  }

  protected introDescription(): string {
    if (this.currentScope() === 'catalog') {
      return 'Start a conversation about your entire document catalogue.';
    }
    const doc = this.documentSignal();
    const name = doc?.title?.trim() || doc?.id || 'this document';
    return `Ask the assistant about ${name}.`;
  }

  protected historyEvents(): DocumentEventDto[] {
    return [...this.historySignal()];
  }

  private handleError(error: unknown): void {
    const message = error instanceof DocumentChatError ? error.message : this.defaultUiError;
    this.errorMessage.set(message);
    this.appendSystemMessage(message);
  }

  private appendQuestionMessage(question: string): void {
    this.appendMessage({
      id: this.createMessageId(),
      kind: 'question',
      text: question,
      timestamp: new Date(),
    });
  }

  private appendAnswerMessage(
    answer: string | null | undefined,
    model: string | null | undefined,
    citations: ReadonlyArray<DocumentChatAnswerCitation>,
    references: ReadonlyArray<DocumentChatAnswerReference>,
    documentCount: number | null
  ): void {
    // Normalize nullable answer to a non-null string for the message text
    const text = answer ?? '';

    this.appendMessage({
      id: this.createMessageId(),
      kind: 'answer',
      text,
      timestamp: new Date(),
      model: model ?? undefined,
      citations,
      documents: references,
      documentCount,
    });
  }

  private appendSystemMessage(text: string): void {
    this.appendMessage({
      id: this.createMessageId(),
      kind: 'system',
      text,
      timestamp: new Date(),
    });
  }

  private appendMessage(message: DocumentChatMessage): void {
    this.messagesSignal.update(list => [...list, message]);
  }

  private cancelActiveRequestOnly(): void {
    if (this.activeSubscription) {
      this.activeSubscription.unsubscribe();
      this.activeSubscription = null;
    }
  }

  private syncHistoryToggle(): void {
    if (!this.hasHistory()) {
      this.setIncludeHistory(false);
      return;
    }
    if (!this.includeHistoryTouched) {
      this.setIncludeHistory(this.historyDefaultValue);
    }
  }

  private setIncludeHistory(value: boolean): void {
    this.includeHistoryControl.setValue(value, { emitEvent: false });
    this.includeHistoryValue.set(value);
  }

  private createMessageId(): string {
    return `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
  }
}
