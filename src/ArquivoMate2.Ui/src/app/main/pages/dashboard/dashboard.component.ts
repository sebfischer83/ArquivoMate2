import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { DocumentsFacadeService } from '../../../services/documents-facade.service';
import { CommonModule, NgForOf, DatePipe } from '@angular/common';
import { UploadWidgetComponent } from './upload-widget.component';


@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, NgForOf, DatePipe, UploadWidgetComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  private facade = inject(DocumentsFacadeService);
  documents = this.facade.documents;
  total = this.facade.totalCount;
  isLoading = this.facade.isLoading;
  error = this.facade.error;
  currentPage = this.facade.currentPage;
  totalPages = this.facade.totalPages;
  pageSize = this.facade.pageSize;
  auth = inject(OAuthService);

  // Upload widget always visible now; on completed uploads the widget itself should trigger facade refresh (can be wired later)

  ngOnInit(): void { this.facade.load(); }

  nextPage(): void { this.facade.setPage(this.currentPage() + 1); }
  prevPage(): void { this.facade.setPage(this.currentPage() - 1); }
  changeSize(val: string): void { const size = parseInt(val, 10); this.facade.setPageSize(size); }
}
