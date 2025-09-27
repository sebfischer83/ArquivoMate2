import { Injectable } from '@angular/core';
import { TuiAlertService } from '@taiga-ui/core';

export interface ToastOptions {
  /** Auto close timeout in ms (defaults vary by type) */
  autoClose?: number;
  /** If true, bypass duplicate suppression */
  force?: boolean;
  /** Custom dedupe window in ms (overrides default) */
  dedupeWindowMs?: number;
}

// Central toast wrapper with lightweight duplicate suppression to avoid noisy UX (e.g. rapid retry offline).
// Strategy: Track last shown message + timestamp per "channel" (error/info/success). Suppress identical messages
// inside a short rolling window (defaultErrorWindowMs) unless force=true.
@Injectable({ providedIn: 'root' })
export class ToastService {
  constructor(private alerts: TuiAlertService) {}

  // Track last message timestamps per category
  private lastMessageMap = new Map<string, { msg: string; ts: number }>();

  private readonly defaultErrorWindowMs = 2000; // suppress identical error toasts within 2s
  private readonly defaultInfoWindowMs = 1000;
  private readonly defaultSuccessWindowMs = 800;

  private show(kind: 'error' | 'info' | 'success', message: string, opts?: ToastOptions): void {
    const now = Date.now();
    const windowMs = opts?.dedupeWindowMs ?? (kind === 'error' ? this.defaultErrorWindowMs : kind === 'info' ? this.defaultInfoWindowMs : this.defaultSuccessWindowMs);
    const key = kind;
    const last = this.lastMessageMap.get(key);
    if (!opts?.force && last && last.msg === message && now - last.ts < windowMs) {
      return; // suppress duplicate
    }
    this.lastMessageMap.set(key, { msg: message, ts: now });
    const autoClose = opts?.autoClose ?? (kind === 'error' ? 5000 : kind === 'info' ? 4000 : 3000);
    this.alerts.open(message, { autoClose }).subscribe();
  }

  error(message: string, opts?: ToastOptions): void {
    this.show('error', message, opts);
  }

  info(message: string, opts?: ToastOptions): void {
    this.show('info', message, opts);
  }

  success(message: string, opts?: ToastOptions): void {
    this.show('success', message, opts);
  }
}
