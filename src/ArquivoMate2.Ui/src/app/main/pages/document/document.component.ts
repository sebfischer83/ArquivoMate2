// ...existing code...
import { ChangeDetectionStrategy, Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface, TuiTitle } from '@taiga-ui/core';
import { TuiTabs } from '@taiga-ui/kit';
import { DocumentTabsComponent } from './components/document-tabs/document-tabs.component';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PdfJsViewerComponent } from '../../../components/pdfjs-viewer/pdfjs-viewer.component';
import { DocumentHistoryComponent } from '../../../components/document-history/document-history.component';
import { DocumentEventDto } from '../../../client/models/document-event-dto';
import { ActivatedRoute } from '@angular/router';
import { DocumentsService } from '../../../client/services/documents.service';
import { DocumentDownloadService } from '../../../services/document-download.service';
import { DocumentDto } from '../../../client/models/document-dto';
import { ToastService } from '../../../services/toast.service';
import { Location } from '@angular/common';

@Component({
  selector: 'app-document',
  imports: [CommonModule, TuiButton, TuiSurface, TuiTitle, TuiTabs, DocumentHistoryComponent, PdfJsViewerComponent, DocumentTabsComponent],
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
    if (!path || this.downloadLoading()) return;
    this.downloadLoading.set(true);
    this.downloadSrv.download(path).subscribe({
      next: blob => {
        this.downloadLoading.set(false);
        try {
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = this.originalFileName() || 'document';
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

  // Adapter for Taiga UI two-way binding syntax if needed
  get activeItemIndex(): number { return this.tabIndex(); }
  set activeItemIndex(i: number) { this.tabIndex.set(i); }

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
