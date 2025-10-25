import { ChangeDetectionStrategy, Component, Input, OnChanges, SimpleChanges, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoModule } from '@jsverse/transloco';
import { DocumentDto } from '../../../../../../client/models/document-dto';
import { LabResultFeatureService } from '../../../../../../client/services/lab-result-feature.service';
import { LabResultDtoListApiResponse } from '../../../../../../client/models/lab-result-dto-list-api-response';
import { TuiHint } from '@taiga-ui/core';

@Component({
  selector: 'app-lab-results',
  standalone: true,
  imports: [CommonModule, TranslocoModule, TuiHint],
  templateUrl: './lab-results.component.html',
  styleUrls: ['./lab-results.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabResultsComponent {
  @Input() document: DocumentDto | null = null;
  /** whether the lab-results tab is currently active/visible */
  @Input() active = false;

  loading = false;
  error: string | null = null;
  labResults: LabResultDtoListApiResponse | null = null;
  private _loaded = false;

  constructor(private labService: LabResultFeatureService, private cd: ChangeDetectorRef) {}

  ngOnChanges(changes: SimpleChanges) {
    if (this.active && !this._loaded) {
      this.load();
    }
  }

  private load() {
    const id = this.document?.id;
    if (!id) {
      this.error = 'no-document-id';
      return;
    }
    this.loading = true;
    this.error = null;
    this.labService.apiFeatureLabresultsDocumentIdGet$Json({ documentId: id }).subscribe({
      next: (res) => {
        this.labResults = res;
        this._loaded = true;
        this.loading = false;
        this.cd.markForCheck();
      },
      error: (err) => {
        this.error = err?.message || 'load-failed';
        this.loading = false;
        this.cd.markForCheck();
      }
    });
  }
}
