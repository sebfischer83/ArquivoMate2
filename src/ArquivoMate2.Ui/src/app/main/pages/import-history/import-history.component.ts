import { ChangeDetectionStrategy, Component, OnInit, signal, computed } from '@angular/core';
import { TuiButton, TuiTitle } from '@taiga-ui/core';
import { TuiBadge } from '@taiga-ui/kit';
import { TuiHeader } from '@taiga-ui/layout';

@Component({
  standalone: true,
  selector: 'app-import-history',
  imports: [
    TuiButton,
    TuiTitle,
    TuiBadge,
    TuiHeader
  ],
  templateUrl: './import-history.component.html',
  styleUrl: './import-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportHistoryComponent implements OnInit {
  protected readonly historyItems = signal<any[]>([]);
  protected readonly loading = signal(false);
  protected readonly selectedFilter = signal<string>('all');
  protected readonly currentPage = signal<number>(1);
  protected readonly pageSize = signal<number>(10);

  // Computed property für gefilterte Items
  protected readonly filteredItems = computed(() => {
    const items = this.historyItems();
    const filter = this.selectedFilter();
    
    if (filter === 'all') {
      return items;
    }
    
    return items.filter(item => item.type === filter);
  });

  // Computed property für total pages
  protected readonly totalPages = computed(() => {
    return Math.ceil(this.filteredItems().length / this.pageSize());
  });

  // Computed property für paginierte Items
  protected readonly paginatedItems = computed(() => {
    const items = this.filteredItems();
    const page = this.currentPage();
    const size = this.pageSize();
    const startIndex = (page - 1) * size;
    const endIndex = startIndex + size;
    
    return items.slice(startIndex, endIndex);
  });

  ngOnInit(): void {
    this.loadHistory();
  }

  private loadHistory(): void {
    this.loading.set(true);
    
    setTimeout(() => {
      // Generate mock data for better paging demonstration
      const mockData = [];
      
      // Generate more test data for paging
      for (let i = 1; i <= 25; i++) {
        const types = ['success', 'error', 'processing', 'queued'];
        const type = types[i % 4];
        const titles = [
          'Document processed',
          'Import started', 
          'Processing error',
          'Waiting in queue'
        ];
        const descriptions = [
          `Document_${String(i).padStart(3, '0')}.pdf was successfully processed`,
          `Contract_${String(i).padStart(3, '0')}.pdf is being processed`,
          `Invoice_${String(i).padStart(3, '0')}.pdf could not be processed`,
          `Report_${String(i).padStart(3, '0')}.pdf is waiting to be processed`
        ];
        
        mockData.push({
          id: i,
          title: titles[i % 4],
          description: descriptions[i % 4],
          timestamp: new Date(Date.now() - (1000 * 60 * i * 5)), // 5 min intervals
          type: type
        });
      }
      
      this.historyItems.set(mockData);
      this.loading.set(false);
    }, 1000);
  }

  protected refresh(): void {
    this.loadHistory();
  }

  protected setFilter(filter: string): void {
    this.selectedFilter.set(filter);
    // Reset to first page when filter changes
    this.currentPage.set(1);
  }

  // Paging methods
  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  protected getPageNumbers(): number[] {
    const total = this.totalPages();
    const current = this.currentPage();
    const pages: number[] = [];
    
    // Show max 5 page numbers around current page
    const maxVisible = 5;
    let start = Math.max(1, current - Math.floor(maxVisible / 2));
    let end = Math.min(total, start + maxVisible - 1);
    
    // Adjust start if we're near the end
    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    
    return pages;
  }

  protected getStartIndex(): number {
    return (this.currentPage() - 1) * this.pageSize();
  }

  protected getEndIndex(): number {
    const startIndex = this.getStartIndex();
    const remainingItems = this.filteredItems().length - startIndex;
    return startIndex + Math.min(this.pageSize(), remainingItems);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    // Reset to first page when page size changes
    this.currentPage.set(1);
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
