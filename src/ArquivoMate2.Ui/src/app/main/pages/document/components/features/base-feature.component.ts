import { Input, SimpleChanges, ChangeDetectorRef, Directive } from '@angular/core';
import { Observable, Subscription, interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { DocumentDto } from '../../../../../client/models/document-dto';
import { DocumentFeatureProcessingDtoApiResponse } from '../../../../../client/models/document-feature-processing-dto-api-response';

/**
 * Small, reusable base class for feature components that need to check a processing status
 * before fetching the actual feature data.
 *
 * Subclasses must implement fetchStatus() and loadData().
 */
@Directive()
export abstract class BaseFeatureComponent<T> {
  @Input() document: DocumentDto | null = null;
  /** whether tab is active / visible */
  @Input() active = false;

  status: DocumentFeatureProcessingDtoApiResponse | null = null;
  statusLoading = false;
  statusError: string | null = null;

  data: T | null = null;
  dataLoading = false;
  dataError: string | null = null;
  protected _loaded = false;

  private subs = new Subscription();
  private pollingSub: Subscription | null = null;
  /** polling interval in ms (used when feature not completed yet) */
  protected pollIntervalMs = 5000;
  /** max polling attempts before giving up */
  protected maxPollAttempts = 12;

  constructor(protected cd: ChangeDetectorRef) {}

  ngOnChanges(changes: SimpleChanges) {
    if (this.active && !this._loaded) {
      this.checkStatusAndMaybeLoad();
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  protected checkStatusAndMaybeLoad(): void {
    const id = this.document?.id;
    if (!id) {
      this.statusError = 'no-document-id';
      this.cd.markForCheck();
      return;
    }

    this.statusLoading = true;
    this.statusError = null;
      // initial one-off fetch
      const s = this.fetchStatus(id).subscribe({
          next: (res: DocumentFeatureProcessingDtoApiResponse) => {
            this.status = res;
          this.statusLoading = false;
          const completed = !!(res?.data && (res.data.completedAtUtc != null));
          if (completed) {
            this.loadData(id);
          } else {
            // start polling
            this.startPolling(id);
          }
        },
          error: (err: any) => {
            this.statusError = err?.message || 'status-failed';
          this.statusLoading = false;
          this.cd.markForCheck();
        }
      });

      this.subs.add(s);
  }

    /** public helper so templates can trigger a manual refresh */
    public refreshStatus(): void {
      // cancel any existing polling and re-run the status check
      if (this.pollingSub) {
        this.pollingSub.unsubscribe();
        this.pollingSub = null;
      }
      this.checkStatusAndMaybeLoad();
    }

    private startPolling(documentId: string) {
      let attempts = 0;
      if (this.pollingSub) {
        this.pollingSub.unsubscribe();
        this.pollingSub = null;
      }
      this.pollingSub = interval(this.pollIntervalMs)
      .pipe(switchMap(() => this.fetchStatus(documentId)))
      .subscribe({
        next: (res: DocumentFeatureProcessingDtoApiResponse) => {
            attempts++;
            this.status = res;
            const completed = !!(res?.data && (res.data.completedAtUtc != null));
            if (completed) {
              // stop polling and load data
              if (this.pollingSub) { this.pollingSub.unsubscribe(); this.pollingSub = null; }
              this.loadData(documentId);
            } else if (attempts >= this.maxPollAttempts) {
              // give up
              if (this.pollingSub) { this.pollingSub.unsubscribe(); this.pollingSub = null; }
              this.statusError = 'processing-timeout';
              this.cd.markForCheck();
            } else {
              this.cd.markForCheck();
            }
          },
          error: (err: any) => {
            if (this.pollingSub) { this.pollingSub.unsubscribe(); this.pollingSub = null; }
            this.statusError = err?.message || 'status-failed';
            this.cd.markForCheck();
          }
        });

      if (this.pollingSub) this.subs.add(this.pollingSub);
    }

  /**
   * Implementations must call the feature status endpoint and return the API response
   */
  protected abstract fetchStatus(documentId: string): Observable<DocumentFeatureProcessingDtoApiResponse>;

  /**
   * Called when status indicates the feature is ready — implementation should fetch data and
   * update `this.data` and _loaded flags accordingly.
   */
  protected abstract loadData(documentId: string): void;
}
