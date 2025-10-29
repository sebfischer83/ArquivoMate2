import { Input, SimpleChanges, ChangeDetectorRef, Directive, signal, WritableSignal } from '@angular/core';
import { ToastService } from '../../../../../services/toast.service';
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
  // New Angular signals to represent status
  statusLoadingSignal: WritableSignal<boolean> = signal(false);
  statusCompletedSignal: WritableSignal<boolean> = signal(false);
  statusFailedSignal: WritableSignal<boolean> = signal(false);
  // status error as signal (string message or null)
  statusErrorSignal: WritableSignal<string | null> = signal(null);

  data: T | null = null;
  protected _loaded = false;

  private subs = new Subscription();
  private pollingSub: Subscription | null = null;
  /** polling interval in ms (used when feature not completed yet) */
  protected pollIntervalMs = 5000;
  /** max polling attempts before giving up */
  protected maxPollAttempts = 12;

  constructor(protected cd: ChangeDetectorRef, protected toast: ToastService) {}

  ngOnChanges(changes: SimpleChanges) {
    if (this.active && !this._loaded) {
      this.checkStatusAndMaybeLoad();
    }
  }

  /**
   * Hook for subclasses to implement the actual restart call for their feature.
   * Should perform the restart request and return an Observable that completes when done.
   */
  protected abstract restartFeature(documentId: string): Observable<void>;

  /**
   * Public helper so templates can trigger a manual restart of processing when it previously failed.
   * Handles signals and refreshing status/data after the restart request.
   */
  public restartProcessing(documentId?: string | null): void {
    if (!documentId) return;

    // Clear prior errors and indicate loading
    this.statusErrorSignal.set(null);
    this.statusLoadingSignal.set(true);

    // show a toast indicating restart was started
    try {
      this.toast?.info('Verarbeitung wird gestartet...');
    } catch (e) {
      // ignore toast errors
    }

    const obs = this.restartFeature(documentId);
    const sub = obs.subscribe({
      next: () => {
        // After restart succeeded, re-check status which will load data when completed
        this.refreshStatus();
      },
      error: (err: any) => {
        this.statusErrorSignal.set(err?.message || 'restart-failed');
        this.statusLoadingSignal.set(false);
        this.statusFailedSignal.set(true);
        this.cd.markForCheck();
      },
      complete: () => {
        // ensure loading flag cleared; refreshStatus will set proper flags when status arrives
        this.statusLoadingSignal.set(false);
        this.cd.markForCheck();
      }
    });

    this.subs.add(sub);
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  protected checkStatusAndMaybeLoad(): void {
    const id = this.document?.id;
    if (!id) {
      this.statusErrorSignal.set('no-document-id');
      this.statusLoadingSignal.set(false);
      this.statusCompletedSignal.set(false);
      this.statusFailedSignal.set(true);
      this.cd.markForCheck();
      return;
    }
    this.statusErrorSignal.set(null);
    this.statusLoadingSignal.set(true);
    this.statusCompletedSignal.set(false);
    this.statusFailedSignal.set(false);
      // initial one-off fetch
      const s = this.fetchStatus(id).subscribe({
          next: (res: DocumentFeatureProcessingDtoApiResponse) => {
            this.status = res;
          this.statusLoadingSignal.set(false);

          console.log('Feature status:', res);
          const completed = res.data?.state === 'Completed';
          const failed = res.data?.state === 'Failed';
          if (completed) {
            this.statusCompletedSignal.set(true);
            this.statusFailedSignal.set(false);
            this.loadData(id);
          } else if (failed) {
            this.statusFailedSignal.set(true);
            this.statusCompletedSignal.set(false);
            this.statusErrorSignal.set(res.data?.lastError || 'processing-failed');
            this.cd.markForCheck();
          } else {
            // start polling
            this.statusCompletedSignal.set(false);
            this.statusFailedSignal.set(false);
            this.startPolling(id);
          }
        },
          error: (err: any) => {
            this.statusErrorSignal.set(err?.message || 'status-failed');
          this.statusLoadingSignal.set(false);
          this.statusFailedSignal.set(true);
          this.statusCompletedSignal.set(false);
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
                this.statusCompletedSignal.set(true);
                this.statusFailedSignal.set(false);
                this.statusLoadingSignal.set(false);
                this.loadData(documentId);
              } else if (attempts >= this.maxPollAttempts) {
                // give up
                if (this.pollingSub) { this.pollingSub.unsubscribe(); this.pollingSub = null; }
                this.statusErrorSignal.set('processing-timeout');
                this.statusFailedSignal.set(true);
                this.statusCompletedSignal.set(false);
                this.statusLoadingSignal.set(false);
                this.cd.markForCheck();
              } else {
                // still polling
                this.statusLoadingSignal.set(true);
                this.statusCompletedSignal.set(false);
                this.statusFailedSignal.set(false);
                this.cd.markForCheck();
              }
          },
          error: (err: any) => {
            if (this.pollingSub) { this.pollingSub.unsubscribe(); this.pollingSub = null; }
              this.statusErrorSignal.set(err?.message || 'status-failed');
              this.statusFailedSignal.set(true);
              this.statusCompletedSignal.set(false);
              this.statusLoadingSignal.set(false);
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
