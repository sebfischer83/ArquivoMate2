import { ChangeDetectionStrategy, Component, ChangeDetectorRef, signal, Input, OnChanges, SimpleChanges } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { DocumentDto } from '../../../../../../client/models/document-dto';
import { LabResultFeatureService } from '../../../../../../client/services/lab-result-feature.service';
import { LabResultDtoListApiResponse } from '../../../../../../client/models/lab-result-dto-list-api-response';
import { DocumentFeatureProcessingDtoApiResponse } from '../../../../../../client/models/document-feature-processing-dto-api-response';
import { LabResultDto } from '../../../../../../client/models/lab-result-dto';
import { BaseFeatureComponent } from '../base-feature.component';
import { ToastService } from '../../../../../../services/toast.service';
import { TuiHint, TuiButton } from '@taiga-ui/core';
import { TuiInputInline } from '@taiga-ui/kit';
import { LocaleAwareDatePipe } from '../../../../../../utils/locale-aware-date.pipe';

@Component({
  selector: 'app-lab-results',
  standalone: true,
  imports: [CommonModule, TranslocoModule, TuiHint, TuiButton, ReactiveFormsModule, TuiInputInline, LocaleAwareDatePipe],
  templateUrl: './lab-results.component.html',
  styleUrls: ['./lab-results.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabResultsComponent extends BaseFeatureComponent<LabResultDtoListApiResponse> {
  // receives edit mode flag from parent document component
  private _editMode = false;
  @Input()
  set editMode(v: boolean) {
    this._editMode = !!v;
    // enable/disable cached controls based solely on parent edit mode
    this.setControlsEnabled(this._editMode);
  }
  get editMode() { return this._editMode; }
  labResults: LabResultDtoListApiResponse | null = null; // kept for template compatibility
  // New signals for data loading/error
  dataLoadingSignal = signal(false);
  dataErrorSignal = signal<string | null>(null);

  constructor(private labService: LabResultFeatureService, protected override cd: ChangeDetectorRef, private transloco: TranslocoService, protected override toast: ToastService) {
    super(cd, toast);
  }

  // New inputs to allow tab-driven loading like NotesList
  @Input() documentId: string | null = null;
  @Input() override active = false;

  // Mirror inputs into base-class `document`/`active` lifecycle by creating a minimal DocumentDto
  override ngOnChanges(changes: SimpleChanges) {
    // Support either `document` (parent passes whole DTO) or `documentId` (parent passes id)
    const mapped: SimpleChanges = {} as any;

    // If parent passed a whole document, forward it directly
    if (changes['document']) {
      this.document = changes['document'].currentValue ?? null;
      mapped['document'] = changes['document'];
    }

    // If parent passed only an id, map it into a minimal DocumentDto and forward
    if (changes['documentId']) {
      const prevId = (changes['documentId'].previousValue ?? null) as string | null;
      const currId = (changes['documentId'].currentValue ?? null) as string | null;
      this.document = currId ? ({ id: currId } as DocumentDto) : null;
      mapped['document'] = {
        previousValue: prevId ? ({ id: prevId } as DocumentDto) : null,
        currentValue: this.document,
        firstChange: changes['documentId'].firstChange,
        isFirstChange: changes['documentId'].isFirstChange,
      } as SimpleChanges[keyof SimpleChanges];
    }

    if (changes['active']) {
      // make sure our active flag mirrors the input value
      this.active = !!changes['active'].currentValue;
      mapped['active'] = changes['active'];
    }

    // If nothing was mapped (odd), fall back to forwarding the original changes
    const toForward = Object.keys(mapped).length ? mapped : changes;
    super.ngOnChanges(toForward as any);
  }

  // Workaround: reference TuiInputInline to satisfy Angular's "used in template" analyzer that
  // may be thrown off by the repository's template preprocessor. This is a no-op.
  private readonly _tuiInputInline = TuiInputInline;

  // Controls cache
  private controls = new Map<string, FormControl>();

  private controlKey(labResultId: string | undefined, pointIndex: number) {
    return `${labResultId ?? 'null'}:${pointIndex}`;
  }

  private controlKeyWithField(labResultId: string | undefined, pointIndex: number, field: string) {
    return `${this.controlKey(labResultId, pointIndex)}|${field}`;
  }

  // Return a cached FormControl for inline editing of a point's normalized value.
  // The control will write back changes to the model via onPointNormalizedChange.
  controlForPoint(labResultId: string | undefined, pointIndex: number, initial: any) {
    const key = this.controlKey(labResultId, pointIndex);
    const existing = this.controls.get(key);
    if (existing) return existing;

    const c = new FormControl(initial == null ? '' : initial);
  // initialize disabled state based on parent editMode
  if (!this.editMode) c.disable({ emitEvent: false });
    c.valueChanges.subscribe(v => this.onPointNormalizedChange(labResultId, pointIndex, v));
    this.controls.set(key, c);
    return c;
  }

  controlForField(labResultId: string | undefined, pointIndex: number, field: string, initial: any) {
    const key = this.controlKeyWithField(labResultId, pointIndex, field);
    const existing = this.controls.get(key);
    if (existing) return existing;

    const c = new FormControl(initial == null ? '' : initial);
    if (!this.editMode) c.disable({ emitEvent: false });
    c.valueChanges.subscribe(v => {
      if (field === 'normalizedResult') this.onPointNormalizedChange(labResultId, pointIndex, v);
      if (field === 'parameter') this.onPointParameterChange(labResultId, pointIndex, v);
      if (field === 'unit') this.onPointUnitChange(labResultId, pointIndex, v);
    });
    this.controls.set(key, c);
    return c;
  }

  private setControlsEnabled(enabled: boolean) {
    for (const c of Array.from(this.controls.values())) {
      try {
        if (enabled) c.enable({ emitEvent: false }); else c.disable({ emitEvent: false });
      } catch {}
    }
  }

  // track modified lab results by id
  private editedResults = new Map<string, LabResultDto>();

  // point value change handlers
  onPointNumericChange(labResultId: string | undefined, pointIndex: number, value: string) {
    if (!labResultId) return;
    const numeric = value === '' ? null : Number(value);
    const list = this.labResults?.data;
    if (!list) return;
    const res = list.find(r => r.id === labResultId);
    if (!res) return;
    const points = (res.points ?? []).map((p, idx) => idx === pointIndex ? ({ ...p, resultNumeric: numeric }) : { ...p });
    // update in-memory
    res.points = points as any;
    // register edited copy
    this.editedResults.set(labResultId, { ...res, points } as LabResultDto);
    // Ensure template updates under OnPush
    try { this.cd.markForCheck(); } catch {}
  }

  onPointRawChange(labResultId: string | undefined, pointIndex: number, value: string) {
    if (!labResultId) return;
    const list = this.labResults?.data;
    if (!list) return;
    const res = list.find(r => r.id === labResultId);
    if (!res) return;
    const points = (res.points ?? []).map((p, idx) => idx === pointIndex ? ({ ...p, resultRaw: value }) : { ...p });
    res.points = points as any;
    this.editedResults.set(labResultId, { ...res, points } as LabResultDto);
    // Ensure template updates under OnPush
    try { this.cd.markForCheck(); } catch {}
  }

  onPointNormalizedChange(labResultId: string | undefined, pointIndex: number, value: string) {
    if (!labResultId) return;
    const numeric = value === '' ? null : Number(value);
    const list = this.labResults?.data;
    if (!list) return;
    const res = list.find(r => r.id === labResultId);
    if (!res) return;
    const points = (res.points ?? []).map((p, idx) => idx === pointIndex ? ({ ...p, normalizedResult: numeric }) : { ...p });
    res.points = points as any;
    this.editedResults.set(labResultId, { ...res, points } as LabResultDto);
    try { this.cd.markForCheck(); } catch {}
  }

  // Handlers for parameter and unit edits
  onPointParameterChange(labResultId: string | undefined, pointIndex: number, value: string) {
    if (!labResultId) return;
    const list = this.labResults?.data;
    if (!list) return;
    const res = list.find(r => r.id === labResultId);
    if (!res) return;
    const points = (res.points ?? []).map((p, idx) => idx === pointIndex ? ({ ...p, parameter: value }) : { ...p });
    res.points = points as any;
    this.editedResults.set(labResultId, { ...res, points } as LabResultDto);
    try { this.cd.markForCheck(); } catch {}
  }

  onPointUnitChange(labResultId: string | undefined, pointIndex: number, value: string) {
    if (!labResultId) return;
    const list = this.labResults?.data;
    if (!list) return;
    const res = list.find(r => r.id === labResultId);
    if (!res) return;
    const points = (res.points ?? []).map((p, idx) => idx === pointIndex ? ({ ...p, unit: value, normalizedUnit: value }) : { ...p });
    res.points = points as any;
    this.editedResults.set(labResultId, { ...res, points } as LabResultDto);
    try { this.cd.markForCheck(); } catch {}
  }

  /**
   * Commit pending lab result edits to server. Returns an observable that completes when all requests finished.
   */
  public commitEdits() {
    const changed = Array.from(this.editedResults.values());
    if (!changed.length) return of(undefined);
    const calls = changed.map(r => this.labService.apiFeatureLabresultsPut({ body: r }));
    // Return an observable that completes when all updates finished and perform local cleanup
    return forkJoin(calls).pipe();
  }

  // Implement abstract restart hook from base class by delegating to the generated service.
  protected override restartFeature(documentId: string) {
    return this.labService.apiFeatureLabresultsRestartDocumentIdPut({ documentId });
  }

    // Helper used by the template to determine if a point row should be marked abnormal.
    isPointAbnormal(point: any): boolean {
      if (!point) {
        return false;
      }

      if (point.resultComparator != null) {
        // If result is expressed with a comparator like '<0.6', that's usually not abnormal
        // (it means the value is below detection limit). Only treat '>' comparators as abnormal.
        const comp = String(point.resultComparator).trim();
        if (comp === '>' || comp === '>=' ) return true;
        return false;
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

  // Remove a lab result row (only available in edit mode)
  removeResult(labResultId?: string | null) {
    if (!labResultId) return;
    this.dataLoadingSignal.set(true);
    this.dataErrorSignal.set(null);

    this.labService.apiFeatureLabresultsLabResultIdDelete({ labResultId }).subscribe({
      next: () => {
        // refresh data for current document
        const docId = this.document?.id;
        if (docId) this.loadData(docId);
        else {
          this.dataLoadingSignal.set(false);
          this.cd.markForCheck();
        }
      },
      error: (err) => {
        this.dataErrorSignal.set(err?.message || 'delete-failed');
        this.dataLoadingSignal.set(false);
        this.cd.markForCheck();
      }
    });
  }

  protected loadData(documentId: string): void {
    this.dataLoadingSignal.set(true);
    this.dataErrorSignal.set(null);
    this.labService.apiFeatureLabresultsDocumentIdGet$Json({ documentId }).subscribe({
      next: (res) => {
        this.labResults = res;
        this.data = res;
        this._loaded = true;
        this.dataLoadingSignal.set(false);
        this.cd.markForCheck();
      },
      error: (err) => {
        this.dataErrorSignal.set(err?.message || 'load-failed');
        this.dataLoadingSignal.set(false);
        this.cd.markForCheck();
      }
    });
  }
}