import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { DocumentsFacadeService } from '../../../services/documents-facade.service';
import { CommonModule } from '@angular/common';
import { UploadWidgetComponent } from './upload-widget.component';
import { DocumentCardGridComponent } from '../../../components/document-card-grid/document-card-grid.component';
import { FormsModule } from '@angular/forms';


@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, UploadWidgetComponent, DocumentCardGridComponent, FormsModule],
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
  currentSearch = this.facade.currentSearch;
  auth = inject(OAuthService);

  searchValue = '';

  ngOnInit(): void { this.facade.load(); }

  onPageChange(p: number) { this.facade.setPage(p); }
  onPageSizeChange(s: number) { this.facade.setPageSize(s); }
  onReload() { this.facade.load(true); }
  onItemClick(doc: any) {
    // placeholder for navigation or detail view integration
    console.debug('Document clicked', doc?.id);
  }

  onSearchInput(val: string) { this.searchValue = val; this.facade.setSearchTerm(val.trim()); }
}
