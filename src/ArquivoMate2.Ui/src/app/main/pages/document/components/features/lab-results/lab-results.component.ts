import { ChangeDetectionStrategy, Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { DocumentDto } from '../../../../../../client/models/document-dto';
import { LabResultFeatureService } from '../../../../../../client/services/lab-result-feature.service';
import { LabResultDtoListApiResponse } from '../../../../../../client/models/lab-result-dto-list-api-response';
import { FeatureProcessingState } from '../../../../../../client/models/feature-processing-state';
import { DocumentFeatureProcessingDtoApiResponse } from '../../../../../../client/models/document-feature-processing-dto-api-response';
import { BaseFeatureComponent } from '../base-feature.component';
import { TuiHint } from '@taiga-ui/core';

@Component({
  selector: 'app-lab-results',
  standalone: true,
  imports: [CommonModule, TranslocoModule, TuiHint],
  templateUrl: './lab-results.component.html',
  styleUrls: ['./lab-results.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabResultsComponent extends BaseFeatureComponent<LabResultDtoListApiResponse> {
  labResults: LabResultDtoListApiResponse | null = null; // kept for template compatibility

  constructor(private labService: LabResultFeatureService, protected override cd: ChangeDetectorRef, private transloco: TranslocoService) {
    super(cd);
  }

    // Helper used by the template to determine if a point row should be marked abnormal.
    isPointAbnormal(point: any): boolean {
      if (!point) {
        return false;
      }

      if (point.resultComparator != null) {
        return true;
      }

      const normalized = point.normalizedResult;
      if (normalized == null) {
        return false;
      }

      const from = point.normalizedReferenceFrom;
      const to = point.normalizedReferenceTo;

      if (from != null && normalized < from) {
        return true;
      }
      if (to != null && normalized > to) {
        return true;
      }

      return false;
    }

    // Formats the reference string for display in the table. Returns '-' when no reference info available.
    formatReference(point: any): string {
      if (!point) {
        return '-';
      }

      // Prefer normalized references when present
      if (point.normalizedReferenceFrom != null || point.normalizedReferenceTo != null) {
        if (point.referenceComparator != null) {
          return `${point.referenceComparator} ${point.normalizedReferenceTo ?? ''} ${point.normalizedUnit ?? ''}`.trim();
        }
        return `${point.normalizedReferenceFrom ?? ''} - ${point.normalizedReferenceTo ?? ''} ${point.normalizedUnit ?? ''}`.trim();
      }

      // Fallback to raw/reference fields
      if (point.referenceComparator != null) {
        return `${point.referenceComparator} ${point.referenceTo ?? ''} ${point.unit ?? ''}`.trim();
      }

      if (point.reference) {
        return point.reference;
      }

      if (point.referenceFrom || point.referenceTo) {
        return `${point.referenceFrom ?? ''} - ${point.referenceTo ?? ''}`.trim();
      }

      return '-';
    }

  protected fetchStatus(documentId: string) {
    return this.labService.apiFeatureLabresultsStatusDocumentIdGet$Json({ documentId });
  }

  protected loadData(documentId: string): void {
    this.dataLoading = true;
    this.dataError = null;
    this.labService.apiFeatureLabresultsDocumentIdGet$Json({ documentId }).subscribe({
      next: (res) => {
        this.labResults = res;
        this.data = res;
        this._loaded = true;
        this.dataLoading = false;
        this.cd.markForCheck();
      },
      error: (err) => {
        this.dataError = err?.message || 'load-failed';
        this.dataLoading = false;
        this.cd.markForCheck();
      }
    });
  }

  // map numeric enum to readable label
  getStateLabel(state?: FeatureProcessingState | null): string {
    // Prefer translated labels from transloco; fall back to english keys
    if (state == null) return this.transloco.translate('Document.LabResults.State.Unknown') || 'unknown';
    switch (state) {
      case FeatureProcessingState.$0:
        return this.transloco.translate('Document.LabResults.State.Queued') || 'queued';
      case FeatureProcessingState.$1:
        return this.transloco.translate('Document.LabResults.State.Processing') || 'processing';
      case FeatureProcessingState.$2:
        return this.transloco.translate('Document.LabResults.State.Completed') || 'completed';
      case FeatureProcessingState.$3:
        return this.transloco.translate('Document.LabResults.State.Failed') || 'failed';
      default:
        return String(state);
    }
  }
}
