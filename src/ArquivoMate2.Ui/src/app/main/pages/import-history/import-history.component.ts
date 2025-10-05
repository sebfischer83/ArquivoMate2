
import { ChangeDetectionStrategy, Component, OnInit, signal, computed, inject } from '@angular/core';
import { TuiButton, TuiTitle } from '@taiga-ui/core';
import { TuiBadge } from '@taiga-ui/kit';
import { TuiHeader } from '@taiga-ui/layout';
import { ImportHistoryService } from '../../../client/services/import-history.service';
import { Observable } from 'rxjs';
import { ImportHistoryListDto } from '../../../client/models/import-history-list-dto';
import { ImportHistoryListDtoApiResponse } from '../../../client/models/import-history-list-dto-api-response';
import { ImportHistoryListItemDto } from '../../../client/models/import-history-list-item-dto';

@Component({
  standalone: true,
  selector: 'app-import-history',
  imports: [
    TuiButton,
    TuiTitle,
    TuiBadge,
    TuiHeader,
  ],
  templateUrl: './import-history.component.html',
  styleUrl: './import-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    
  ]
})
export class ImportHistoryComponent implements OnInit {
  protected readonly historyItems = signal<any[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedFilter = signal<string>('all');
  protected readonly currentPage = signal<number>(1);
  protected readonly pageSize = signal<number>(10);
  protected readonly totalCount = signal<number>(0);
  protected readonly pageCount = signal<number>(1);
  private readonly importHistoryService = inject(ImportHistoryService);

  // Filtered Items: Backend liefert bereits paginiert, Filter wird auf die aktuelle Seite angewendet
  protected readonly filteredItems = computed(() => {
    const items = this.historyItems();
    const filter = this.selectedFilter();
    if (filter === 'all') {
      return items;
    }
    return items.filter(item => item.type === filter);
  });

  ngOnInit(): void {
    this.loadHistory();
  }

  private loadHistory(): void {
    this.loading.set(true);
    this.error.set(null);
    const params = {
      Page: this.currentPage(),
      PageSize: this.pageSize(),
    };
  let apiCall: (p: any) => Observable<ImportHistoryListDtoApiResponse>;
    switch (this.selectedFilter()) {
      case 'success':
        apiCall = this.importHistoryService.apiHistoryCompletedGet$Json.bind(this.importHistoryService);
        break;
      case 'error':
        apiCall = this.importHistoryService.apiHistoryFailedGet$Json.bind(this.importHistoryService);
        break;
      case 'processing':
        apiCall = this.importHistoryService.apiHistoryInprogressGet$Json.bind(this.importHistoryService);
        break;
      case 'queued':
        apiCall = this.importHistoryService.apiHistoryPendingGet$Json.bind(this.importHistoryService);
        break;
      case 'all':
      default:
        apiCall = this.importHistoryService.apiHistoryGet$Json.bind(this.importHistoryService);
        break;
    }
    apiCall(params)
      .subscribe({
        next: (resp: ImportHistoryListDtoApiResponse) => {
          const ok = resp?.success !== false;
          const result = resp?.data;
          if (ok && result && Array.isArray(result.items)) {
            this.historyItems.set(result.items.map((item: ImportHistoryListItemDto) => ({
              id: item.id,
              title: item.fileName ?? '',
              description: item.errorMessage ?? '',
              timestamp: item.completedAt
                ? new Date(item.completedAt)
                : item.startedAt
                  ? new Date(item.startedAt)
                  : item.occurredOn
                    ? new Date(item.occurredOn)
                    : new Date(),
              type: this.mapStatusToUiType(item.status)
            })));
            this.totalCount.set(result.totalCount ?? 0);
            this.pageCount.set(result.pageCount ?? 1);
            this.currentPage.set(result.currentPage ?? 1);
          } else {
            this.error.set(resp?.message || 'Fehler beim Laden der Import-Historie');
            this.historyItems.set([]);
            this.totalCount.set(0);
            this.pageCount.set(1);
          }
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Fehler beim Laden der Import-Historie');
          this.historyItems.set([]);
          this.totalCount.set(0);
          this.pageCount.set(1);
          this.loading.set(false);
        }
      });
  }

  /**
   * Mappt Backend-Status auf UI-Status für Filter und Anzeige
   */
  // Accepts status which may be string, null or undefined.
  private mapStatusToUiType(status: string | null | undefined): string {
    switch (status) {
      case 'Completed': return 'success';
      case 'Failed': return 'error';
      case 'InProgress': return 'processing';
      case 'Pending': return 'queued';
      default: return status ?? 'unknown';
    }
  }

  protected refresh(): void {
    this.loadHistory();
  }

  protected setFilter(filter: string): void {
    this.selectedFilter.set(filter);
    this.currentPage.set(1);
    this.loadHistory();
  }

  // Paging methods
  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.pageCount()) {
      this.currentPage.set(page);
      this.loadHistory();
    }
  }

  protected getPageNumbers(): number[] {
    const total = this.pageCount();
    const current = this.currentPage();
    const pages: number[] = [];
    // Zeige max. 5 Seiten um die aktuelle Seite herum
    const maxVisible = 5;
    let start = Math.max(1, current - Math.floor(maxVisible / 2));
    let end = Math.min(total, start + maxVisible - 1);
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  /**
   * Gibt den Endindex der aktuellen Seite für die Anzeige zurück
   */
  protected getEndIndex(): number {
    const endIndex = this.currentPage() * this.pageSize();
    return endIndex > this.totalCount() ? this.totalCount() : endIndex;
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
    this.loadHistory();
  }

  protected getStatusBadge(type: string): string {
    switch (type) {
      case 'success': return 'success';
      case 'processing': return 'warning';
      case 'error': return 'error';
      case 'queued': return 'neutral';
      default: return 'neutral';
    }
  }

  protected getStatusText(type: string): string {
    switch (type) {
      case 'success': return 'Successful';
      case 'processing': return 'In Progress';
      case 'error': return 'Failed';
      case 'queued': return 'Queued';
      default: return type;
    }
  }

  protected formatTimestamp(timestamp: Date): string {
    const now = new Date();
    const diffMs = now.getTime() - timestamp.getTime();
    const diffMinutes = Math.floor(diffMs / (1000 * 60));
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    if (diffMinutes < 60) {
      return `${diffMinutes} minutes ago`;
    } else if (diffHours < 24) {
      return `${diffHours} hours ago`;
    } else {
      return `${diffDays} days ago`;
    }
  }
}
