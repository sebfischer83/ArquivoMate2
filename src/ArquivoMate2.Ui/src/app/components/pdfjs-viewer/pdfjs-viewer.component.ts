import { Component, ChangeDetectionStrategy, Input, signal, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiLoader, TuiSurface } from '@taiga-ui/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subject } from 'rxjs';

// pdf.js imports
import { getDocument, GlobalWorkerOptions, type PDFDocumentProxy, type PDFPageProxy } from 'pdfjs-dist';
// Import worker asset URL (bundler should copy it); adjust path if necessary
// @ts-ignore
import workerSrc from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
// Worker asset path note: If needed you can copy from node_modules/pdfjs-dist/build/pdf.worker.min.js
// For now we set workerSrc dynamically when first used (vite/angular builder will serve it from node_modules).

declare const window: any;

@Component({
  selector: 'app-pdfjs-viewer',
  standalone: true,
  imports: [CommonModule, TuiButton, TuiLoader, TuiSurface],
  template: `
  <div class="pdfjs-container" [class.loading]="loading()">
    <div class="toolbar" tuiSurface appearance="floating">
      <div class="left">
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.arrow-left" (click)="prevPage()" [disabled]="pageIndex() <= 1">Seite -</button>
  <span class="page-indicator">{{ pageIndex() }} / {{ totalPages() || '?' }}</span>
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.arrow-right" (click)="nextPage()" [disabled]="pageIndex() >= totalPages()">Seite +</button>
      </div>
      <div class="mid">
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.zoom-in" (click)="zoomIn()" [disabled]="zoom() >= 3 || fitPage()">Zoom +</button>
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.zoom-out" (click)="zoomOut()" [disabled]="zoom() <= 0.5 || fitPage()">Zoom -</button>
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.refresh-ccw" (click)="resetZoom()" [disabled]="(zoom() === 1 && !fitPage())">Reset</button>
        <button tuiButton size="xs" appearance="flat" iconStart="@tui.maximize" (click)="toggleFit()" [appearance]="fitPage() ? 'primary' : 'flat'">Fit Höhe</button>
      </div>
      <div class="right"></div>
    </div>

  <div class="viewer-wrapper">
      <canvas #canvasEl class="pdf-canvas"></canvas>
      <div class="loading-overlay" *ngIf="loading()">
        <tui-loader size="l"></tui-loader>
      </div>
      <div class="error-box" *ngIf="error()" tuiSurface appearance="negative">{{ error() }}</div>
    </div>
  </div>
  `,
  styleUrls: ['./pdfjs-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfJsViewerComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  private sanitizer = inject(DomSanitizer);

  @Input() set src(url: string | null) {
    if (!url) {
      this.srcSafe = null;
      this.clearDoc();
      return;
    }
    // Trust the URL (already internal), still sanitize for iframe-like semantics
    this.srcSafe = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    this.load(url);
  }


  loading = signal(false);
  error = signal<string | null>(null);
  pageIndex = signal(1);
  totalPages = signal(0);
  zoom = signal(1);
  fitPage = signal(false);

  private pdfDoc: PDFDocumentProxy | null = null;
  private currentPage: PDFPageProxy | null = null;
  private canvas?: HTMLCanvasElement;
  private rendering = false;
  private pendingPage: number | null = null;
  srcSafe: SafeResourceUrl | null = null;

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.clearDoc();
  }

  private clearDoc() {
    this.pdfDoc = null;
    this.currentPage = null;
    if (this.canvas) {
      const ctx = this.canvas.getContext('2d');
      ctx?.clearRect(0, 0, this.canvas.width, this.canvas.height);
    }
  }

  private ensureWorker() {
    if (!GlobalWorkerOptions.workerSrc) {
      GlobalWorkerOptions.workerSrc = workerSrc;
    }
  }

  private load(url: string) {
    this.loading.set(true);
    this.error.set(null);
    this.ensureWorker();
    const task = getDocument({ url });
    task.promise.then(doc => {
      this.pdfDoc = doc;
      this.totalPages.set(doc.numPages);
      this.pageIndex.set(1);
      this.renderPage();
      this.loading.set(false);
    }).catch(err => {
      console.error(err);
      this.error.set('PDF konnte nicht geladen werden');
      this.loading.set(false);
    });
  }

  private renderPage() {
    if (!this.pdfDoc) return;
    const pageNum = this.pageIndex();
    this.rendering = true;
    this.pdfDoc.getPage(pageNum).then(page => {
      this.currentPage = page;
      // Determine scale: either explicit zoom or fit-to-height
      let scale = this.zoom();
      if (this.fitPage()) {
        const wrapper = document.querySelector('.viewer-wrapper') as HTMLElement | null;
        const baseViewport = page.getViewport({ scale: 1 });
        if (wrapper && baseViewport.height) {
          const available = wrapper.clientHeight - 16; // subtract padding
          scale = available / baseViewport.height;
          if (scale <= 0) scale = this.zoom();
        }
      }
      const viewport = page.getViewport({ scale });
      if (!this.canvas) {
        this.canvas = document.querySelector('canvas.pdf-canvas') as HTMLCanvasElement;
      }
      const canvas = this.canvas!;
      const ctx = canvas.getContext('2d');
      canvas.height = viewport.height;
      canvas.width = viewport.width;
      if (!ctx) {
        this.rendering = false;
        return;
      }
      const renderTask = page.render({ canvasContext: ctx, canvas, viewport });
      renderTask.promise.then(() => {
        this.rendering = false;
        if (this.pendingPage !== null) {
          const p = this.pendingPage; this.pendingPage = null; this.pageIndex.set(p); this.renderPage();
        }
      });
    });
  }

  nextPage() {
    if (this.pageIndex() >= this.totalPages()) return;
    const target = this.pageIndex() + 1;
    this.queueRender(target);
  }

  prevPage() {
    if (this.pageIndex() <= 1) return;
    const target = this.pageIndex() - 1;
    this.queueRender(target);
  }

  zoomIn() { this.zoom.update(z => Math.min(3, +(z + 0.25).toFixed(2))); this.renderPage(); }
  zoomOut() { this.zoom.update(z => Math.max(0.5, +(z - 0.25).toFixed(2))); this.renderPage(); }
  resetZoom() { this.zoom.set(1); this.renderPage(); }
  toggleFit() {
    this.fitPage.update(v => !v);
    if (!this.fitPage()) {
      this.zoom.set(1); // reset when leaving fit mode
    }
    this.renderPage();
  }

  private queueRender(page: number) {
    if (this.rendering) { this.pendingPage = page; return; }
    this.pageIndex.set(page);
    this.renderPage();
  }

  // Overlay functionality removed
}
