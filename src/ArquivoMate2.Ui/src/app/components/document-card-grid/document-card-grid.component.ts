import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DocumentListItemDto } from '../../client/models/document-list-item-dto';
import { TuiPagination } from '@taiga-ui/kit';
import { DocumentCardComponent } from '../document-card/document-card.component';

/**
 * Reusable grid component to display a paged list of documents as Taiga UI style cards.
 * (Currently uses lightweight internal markup; can be replaced with real Taiga UI card components once library is integrated.)
 */
@Component({
  standalone: true,
  selector: 'am-document-card-grid',
  imports: [CommonModule, TuiPagination, DocumentCardComponent],
  templateUrl: './document-card-grid.component.html',
  styleUrl: './document-card-grid.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentCardGridComponent implements OnChanges, OnDestroy {
  // Inputs
  @Input() items: readonly DocumentListItemDto[] | null | undefined;
  @Input() totalCount = 0;
  @Input() page = 1;
  @Input() pageCount = 0;
  @Input() pageSize = 20;
  @Input() pageSizeOptions: readonly number[] = [10, 20, 50];
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() skeletonCount = 6;
  @Input() alwaysShowControls = true;
  @Input() loadingVariant: 'shimmer' | 'minimal' = 'shimmer';
  @Input() showOverlayOnInitialLoad = true;
  @Input() preserveItemsWhileLoading = false;
  @Input() loadingDebounceMs = 120;
  @Input() loadingMinVisibleMs = 250;
  /** Minimum width (in px) for a single card cell before wrapping; increases overall card size. */
  @Input() cardMinWidth = 240; // reduced for more compact layout (was 300, originally ~220 implicit)

  private initialLoadDone = false;

  private visibleLoading = signal(false);
  private loadingStartAt: number | null = null;
  private debounceTimer: any;
  private minVisibleTimer: any;

  get showOverlay(): boolean {
    if (!this.visibleLoading()) return false;
    if (!this.showOverlayOnInitialLoad) return false;
    return !this.initialLoadDone;
  }

  // Statt computed -> einfache Methode; Inputs verändern Template direkt bei CD
  hasItems(): boolean { return !!(this.items && this.items.length); }
  displayLoading() { return this.visibleLoading(); }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['loading']) {
      this.syncLoadingState();
      if (this.loading === false) this.initialLoadDone = true;
    }
  }

  private syncLoadingState() {
    if (this.loading) {
      if (this.visibleLoading()) return; // bereits sichtbar
      this.clearTimers();
      this.debounceTimer = setTimeout(() => {
        this.visibleLoading.set(true);
        this.loadingStartAt = performance.now();
      }, this.loadingDebounceMs);
    } else {
      if (!this.visibleLoading()) { this.clearTimers(); return; }
      const elapsed = this.loadingStartAt ? performance.now() - this.loadingStartAt : 0;
      const remaining = this.loadingMinVisibleMs - elapsed;
      this.clearTimers();
      if (remaining > 0) {
        this.minVisibleTimer = setTimeout(() => { this.visibleLoading.set(false); this.loadingStartAt = null; }, remaining);
      } else {
        this.visibleLoading.set(false); this.loadingStartAt = null;
      }
    }
  }

  private clearTimers() {
    if (this.debounceTimer) { clearTimeout(this.debounceTimer); this.debounceTimer = null; }
    if (this.minVisibleTimer) { clearTimeout(this.minVisibleTimer); this.minVisibleTimer = null; }
  }

  ngOnDestroy() { this.clearTimers(); }

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();
  @Output() itemClick = new EventEmitter<DocumentListItemDto>();
  @Output() reload = new EventEmitter<void>();

  trackById(_: number, item: DocumentListItemDto) { return item.id; }
  onCardClick(item: DocumentListItemDto) { this.itemClick.emit(item); }
  changeSize(size: string) { const v = parseInt(size, 10); if (!isNaN(v)) this.pageSizeChange.emit(v); }
  onIndexChange(i: number) { if (this.displayLoading()) return; this.pageChange.emit(i + 1); }
}
