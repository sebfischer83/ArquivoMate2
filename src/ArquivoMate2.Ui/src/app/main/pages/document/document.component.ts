import { ChangeDetectionStrategy, Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface, TuiTitle } from '@taiga-ui/core';
import { DocumentHistoryComponent } from '../../../components/document-history/document-history.component';
import { DocumentEventDto } from '../../../client/models/document-event-dto';
import { ActivatedRoute } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { ToastService } from '../../../services/toast.service';
import { Location } from '@angular/common';

@Component({
  selector: 'app-document',
  imports: [CommonModule, TuiButton, TuiSurface, TuiTitle, DocumentHistoryComponent],
  templateUrl: './document.component.html',
  styleUrl: './document.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentComponent implements OnInit {
  readonly documentId = signal<string | null>(null);
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly document = signal<DocumentDto | null>(null);
  readonly hasData = computed(() => !!this.document());
  readonly history = signal<DocumentEventDto[]>([]);

  constructor(
    private route: ActivatedRoute,
    private api: DocumentsService,
    private toast: ToastService,
    private location: Location,
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
      this.history.set(resolved.history ?? []);
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
        this.history.set(dto.history ?? []);
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
}
