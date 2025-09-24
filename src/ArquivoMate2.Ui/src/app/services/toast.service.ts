import { Injectable, inject } from '@angular/core';
import { TuiAlertService } from '@taiga-ui/core';

// Thin wrapper to abstract toast implementation (facilitates future switch or theming adjustments)
@Injectable({ providedIn: 'root' })
export class ToastService {
  private alerts = inject(TuiAlertService);

  error(message: string): void {
    this.alerts.open(message, { autoClose: 5000 }).subscribe();
  }

  info(message: string): void {
    this.alerts.open(message, { autoClose: 4000 }).subscribe();
  }

  success(message: string): void {
    this.alerts.open(message, { autoClose: 3000 }).subscribe();
  }
}
