import { Component, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton, TuiSurface } from '@taiga-ui/core';
import { PdfJsViewerComponent } from './pdfjs-viewer.component';

/**
 * External toolbar for PdfJsViewerComponent.
 * Receives the viewer instance and calls its public API methods directly.
 * This avoids duplicating state signals outside the viewer while allowing a sticky layout like document tabs.
 */
@Component({
  selector: 'app-pdfjs-viewer-toolbar',
  standalone: true,
  imports: [CommonModule, TuiButton, TuiSurface],
  template: `
  <div class="pdf-toolbar" tuiSurface appearance="floating" *ngIf="viewer">
    <div class="group nav">
      <button tuiButton size="xs" appearance="flat" iconStart="@tui.arrow-left" (click)="viewer.prevPage()" [disabled]="viewer.pageIndex() <= 1"></button>
      <span class="page-indicator">{{ viewer.pageIndex() }} / {{ viewer.totalPages() || '?' }}</span>
      <button tuiButton size="xs" appearance="flat" iconStart="@tui.arrow-right" (click)="viewer.nextPage()" [disabled]="viewer.pageIndex() >= viewer.totalPages()"></button>
    </div>
    <div class="group zoom">
      <button tuiButton size="xs" appearance="flat" iconStart="@tui.zoom-in" (click)="viewer.zoomIn()">Zoom +</button>
      <button tuiButton size="xs" appearance="flat" iconStart="@tui.zoom-out" (click)="viewer.zoomOut()">Zoom -</button>
      <button tuiButton size="xs" appearance="flat" iconStart="@tui.refresh-ccw" (click)="viewer.resetZoom()" [disabled]="viewer.zoom() === 1 && viewer.fitMode() === 'custom'">Reset</button>
      <span class="zoom-indicator">{{ calcPercent() }}%</span>
    </div>
    <div class="group fit">
      <button tuiButton size="xs" appearance="flat" (click)="viewer.setFitMode('page')" [disabled]="viewer.fitMode() === 'page'">Fit Page</button>
      <button tuiButton size="xs" appearance="flat" (click)="viewer.setFitMode('width')" [disabled]="viewer.fitMode() === 'width'">Fit Width</button>
    </div>
  </div>
  `,
  styles: [`
    :host { display:block; width:100%; }
    .pdf-toolbar { display:flex; flex-wrap:wrap; align-items:center; gap:0.6rem; padding:0.35rem 0.6rem; border-radius:0.6rem; }
    .group { display:flex; align-items:center; gap:0.35rem; }
    .page-indicator { font-size:0.7rem; font-family:var(--tui-font-mono); opacity:0.8; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfJsViewerToolbarComponent {
  @Input() viewer!: PdfJsViewerComponent | null;
  calcPercent(): string {
    if (!this.viewer) return '100';
    // Use lastScale (effective) to reflect fit modes, fallback to zoom()
    const scale = this.viewer.lastScale ? this.viewer.lastScale() : this.viewer.zoom();
    return (scale * 100).toFixed(0);
  }
}
