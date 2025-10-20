import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { DocumentsFacadeService } from '../../../services/documents-facade.service';
import { CommonModule } from '@angular/common';
import { UploadWidgetComponent } from './upload-widget.component';
import { DocumentCardGridComponent } from '../../../components/document-card-grid/document-card-grid.component';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DocumentNavigationService } from '../../../services/document-navigation.service';


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
  private router = inject(Router);
  private navigation = inject(DocumentNavigationService);
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
    if (doc?.id) {
      const list = this.documents();
      const page = this.currentPage();
      const pageSize = this.pageSize();
      const search = (this.currentSearch() || '').trim();

      this.navigation.prepare({
        page,
        pageSize,
        search,
        list: list || null,
      });

      const queryParams: Record<string, any> = {
        page,
        pageSize,
      };
      if (search) {
        queryParams['search'] = search;
      }

      this.router.navigate(['/app/document', doc.id], { queryParams });
    }
  }

  onSearchInput(val: string) { this.searchValue = val; this.facade.setSearchTerm(val.trim()); }
}
