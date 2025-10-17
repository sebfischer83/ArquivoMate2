import { Component, ChangeDetectionStrategy, Input, signal, inject, OnDestroy, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { CommonModule } from '@angular/common';
import { TuiLoader, TuiSurface } from '@taiga-ui/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subject } from 'rxjs';
import { OAuthService } from 'angular-oauth2-oidc';

// pdf.js imports
import * as pdfjsLib from 'pdfjs-dist';
import type { PDFDocumentProxy, PDFPageProxy } from 'pdfjs-dist';

// Worker wird aus den kopierten Assets geladen
const pdfWorkerUrl = '/assets/pdfjs/pdf.worker.min.mjs';

declare const window: any;

@Component({
  selector: 'app-pdfjs-viewer',
  standalone: true,
  imports: [CommonModule, TuiLoader, TuiSurface],
  template: `
  <div class="pdfjs-container" [class.loading]="loading()">
    <div class="viewer-wrapper" #wrapper>
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
export class PdfJsViewerComponent implements OnDestroy, AfterViewInit {
  private destroy$ = new Subject<void>();

  private sanitizer = inject(DomSanitizer);
  private transloco = inject(TranslocoService);
  private auth = inject(OAuthService);

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
  /** Effective last render scale (after fit/zoom computation). */
  lastScale = signal(1);
  /** Fit modes: page -> contain (whole page visible), width -> fill available width, custom -> manual zoom (100% baseline). */
  fitMode = signal<'page' | 'width' | 'custom'>('page');
  /** User rotation offset (multiples of 90). Currently not exposed via UI after toolbar simplification. */
  rotation = signal(0);
  /** If true (default) a pure 180° intrinsic page rotation is normalized to 0 (some PDFs embed upside-down pages). */
  @Input() normalizeUpsideDown = true;
  /** Allow initial override of fit mode from parent */
  @Input() set initialFitMode(m: 'page'|'width'|'custom') {
    const changed = this.fitMode() !== m;
    this.fitMode.set(m);
    if (changed && this.pdfDoc) {
      // re-render immediately if document already loaded
      this.renderPage();
    }
  }
  /** Optional initial custom zoom in percent (e.g. 100, 125). Only applied if initialFitMode='custom'. */
  @Input() set initialZoomPercent(p: number | null) {
    if (p && p > 0) {
      this.zoom.set(p / 100);
      if (this.fitMode() === 'custom' && this.pdfDoc) {
        this.renderPage();
      }
    }
  }

  private pdfDoc: PDFDocumentProxy | null = null;
  private currentPage: PDFPageProxy | null = null;
  private currentRenderTask: any = null; // RenderTask von pdfjs
  @ViewChild('canvasEl', { static: true }) private canvasRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('wrapper', { static: true }) private wrapperRef?: ElementRef<HTMLElement>;
  private rendering = false;
  private pendingPage: number | null = null;
  srcSafe: SafeResourceUrl | null = null;
  private resizeObserver?: ResizeObserver;

  ngAfterViewInit(): void {
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => {
        if (this.pdfDoc) {
          this.renderPage();
        }
      });
      const el = this.wrapperRef?.nativeElement;
      if (el) this.resizeObserver.observe(el);
    }
  }

  ngOnDestroy(): void {
    if (this.resizeObserver) {
      try { this.resizeObserver.disconnect(); } catch {}
    }
    this.destroy$.next();
    this.destroy$.complete();
    this.clearDoc();
  }

  private clearDoc() {
    // Laufenden Render-Task abbrechen
    if (this.currentRenderTask) {
      try {
        this.currentRenderTask.cancel();
      } catch (e) {
        // Ignore cancellation errors
      }
      this.currentRenderTask = null;
    }
    
    this.pdfDoc = null;
    this.currentPage = null;
    const c = this.canvasRef?.nativeElement;
    if (c) {
      const ctx = c.getContext('2d');
      if (ctx) ctx.clearRect(0, 0, c.width, c.height);
    }
  }

  private ensureWorker() {
    if (!pdfjsLib.GlobalWorkerOptions.workerSrc) {
      // Assign resolved URL only once. (Type expects string)
      pdfjsLib.GlobalWorkerOptions.workerSrc = pdfWorkerUrl;
    }
  }

  private load(url: string) {
    this.loading.set(true);
    this.error.set(null);
    this.ensureWorker();
    
    // Bearer Token für PDF.js Request hinzufügen
    const token = this.auth.getAccessToken();
    const httpHeaders: Record<string, string> = {};
    
    if (token) {
      httpHeaders['Authorization'] = `Bearer ${token}`;
    }
    
    const task = pdfjsLib.getDocument({ 
      url,
      httpHeaders,
      withCredentials: false // Bearer Token wird verwendet, nicht Cookies
    });
    
    task.promise.then((doc: PDFDocumentProxy) => {
      this.pdfDoc = doc;
      this.totalPages.set(doc.numPages);
      this.pageIndex.set(1);
      this.renderPage();
      this.loading.set(false);
    }).catch((err: unknown) => {
      console.error(err);
      this.error.set(this.transloco.translate('Document.PdfLoadError'));
      this.loading.set(false);
    });
  }

  private async renderPage() {
    if (!this.pdfDoc) return;
    
    // Abbrechen eines laufenden Render-Tasks und warten bis Cancellation abgeschlossen ist
    if (this.currentRenderTask) {
      try {
        this.currentRenderTask.cancel();
        // Warten bis das Promise rejected wird (Cancellation abgeschlossen)
        await this.currentRenderTask.promise.catch(() => {
          // Cancellation errors ignorieren
        });
      } catch (e) {
        // Ignore cancellation errors
      }
      this.currentRenderTask = null;
    }
    
    const pageNum = this.pageIndex();
    this.rendering = true;
    
    try {
      const page: PDFPageProxy = await this.pdfDoc.getPage(pageNum);
      this.currentPage = page;
      
      // Combine intrinsic page rotation with user rotation (if any)
      let effectiveRotation = (page.rotate + this.rotation()) % 360;
      if (this.normalizeUpsideDown && effectiveRotation === 180) {
        effectiveRotation = 0;
      }
      
      const wrapperEl = this.wrapperRef?.nativeElement;
      const baseViewport = page.getViewport({ scale: 1, rotation: effectiveRotation });
      const scale = this.computeScale(baseViewport.width, baseViewport.height, wrapperEl);
      const viewport = page.getViewport({ scale, rotation: effectiveRotation });
      this.lastScale.set(scale);
      
      const canvas = this.canvasRef!.nativeElement;
      const ctx = canvas.getContext('2d');
      canvas.height = viewport.height;
      canvas.width = viewport.width;
      // Also set CSS size so that intrinsic pixel size maps 1:1 unless browser scales
      canvas.style.width = `${viewport.width}px`;
      canvas.style.height = `${viewport.height}px`;
      
      if (!ctx) {
        this.rendering = false;
        return;
      }
      
      this.currentRenderTask = page.render({ canvasContext: ctx, canvas, viewport });
      
      try {
        await this.currentRenderTask.promise;
        this.currentRenderTask = null;
        this.rendering = false;
        
        // Pending page nach erfolgreichem Render verarbeiten
        if (this.pendingPage !== null) {
          const p = this.pendingPage;
          this.pendingPage = null;
          this.pageIndex.set(p);
          this.renderPage();
        }
      } catch (err: any) {
        // Ignore cancellation errors
        if (err?.name !== 'RenderingCancelledException') {
          console.error('Render error:', err);
        }
        this.currentRenderTask = null;
        this.rendering = false;
      }
    } catch (err) {
      console.error('Error getting page:', err);
      this.rendering = false;
    }
  }

  /**
   * Compute an effective scale that
   * 1. Respects explicit zoom()
   * 2. Optionally fits page height if fitPage() active
   * 3. Clamps so the resulting canvas does not exceed wrapper width/height (prevents layout overflow)
   */
  private computeScale(pdfWidth: number, pdfHeight: number, wrapperEl?: HTMLElement): number {
    if (!wrapperEl) return 1;
    const paddingComp = 16;
    const availW = Math.max(0, wrapperEl.clientWidth - paddingComp);
    const availH = Math.max(0, wrapperEl.clientHeight - paddingComp);
    const contain = Math.min(
      availW > 0 ? availW / pdfWidth : 1,
      availH > 0 ? availH / pdfHeight : 1
    );
    switch (this.fitMode()) {
      case 'page':
        return clamp(contain, 0.1, 5);
      case 'width':
        if (availW <= 0) return 1;
        return clamp(availW / pdfWidth, 0.1, 5);
      case 'custom':
      default:
        // custom uses manual zoom relative to natural 100% size (scale=1) but still avoid huge overflow > 5
        return clamp(this.zoom(), 0.1, 5);
    }
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

  setFitMode(mode: 'page'|'width'|'custom') {
    this.fitMode.set(mode);
    if (mode === 'custom') {
      if (this.zoom() === 1) {
        // keep 100% baseline
      }
    } else {
      // reset zoom to baseline when leaving custom
      this.zoom.set(1);
    }
    this.renderPage();
  }
  zoomIn() { if (this.fitMode() !== 'custom') this.fitMode.set('custom'); this.zoom.update(z => +(z + 0.25).toFixed(2)); this.renderPage(); }
  zoomOut() { if (this.fitMode() !== 'custom') this.fitMode.set('custom'); this.zoom.update(z => Math.max(0.1, +(z - 0.25).toFixed(2))); this.renderPage(); }
  resetZoom() { this.zoom.set(1); if (this.fitMode() !== 'custom') this.fitMode.set('custom'); this.renderPage(); }

  private queueRender(page: number) {
    if (this.rendering) { this.pendingPage = page; return; }
    this.pageIndex.set(page);
    this.renderPage();
  }

  // Exposed for external toolbar template consumption
  // (signals are already public; methods above are the API)
  // Overlay functionality removed
}

function clamp(v: number, min: number, max: number) { return v < min ? min : v > max ? max : v; }
