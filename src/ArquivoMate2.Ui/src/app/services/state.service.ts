import { inject, Injectable, signal } from '@angular/core';
import { SignalrService } from './signalr.service';
import { DocumentProcessingNotification } from '../models/document-processing-notification';
import { ImportHistoryService } from '../client/services';
import { DocumentProcessingStatus } from '../models/document-processing-status';
import { DevConsole } from '../utils/console';

@Injectable({
  providedIn: 'root'
})
export class StateService {
  readonly documentNotification = signal<DocumentProcessingNotification[]>([]);
  readonly documentsInProgressCound = signal<number>(0);

  private signalRService = inject(SignalrService);
  private isInitialized = false;
  private historyService = inject(ImportHistoryService);

  constructor() {
  }

  private initializeEventHandlers(): void {
    if (this.isInitialized) {
      return;
    }

    try {
      // Event-Handler registrieren
      this.signalRService.on('documentprocessingnotification', (notification: DocumentProcessingNotification) => {
        DevConsole.log('Received documentprocessingnotification:', notification);
        
  // Check if notification with same documentId already exists (native find)
  const existingNotification = this.documentNotification().find(n => n.documentId === notification.documentId);
        
        if (!existingNotification) {
          // Add new notification if not already present
          this.documentNotification.update((prev) => [...prev, notification]);
          
          if (notification.status === DocumentProcessingStatus.InProgress) {
            this.documentsInProgressCound.update((count) => count + 1);
          }
        } else {
          // Remove existing notification and add the new one
          DevConsole.log('Notification with documentId already exists, replacing:', notification.documentId);
          this.documentNotification.update((prev) => {
            const filtered = prev.filter(n => n.documentId !== notification.documentId);
            return [...filtered, notification];
          });
          
          // Update count if status changed from/to InProgress
          if (existingNotification.status === DocumentProcessingStatus.InProgress && 
              notification.status !== DocumentProcessingStatus.InProgress) {
            // Was InProgress, now it's not - decrease count
            this.documentsInProgressCound.update((count) => Math.max(0, count - 1));
          } else if (existingNotification.status !== DocumentProcessingStatus.InProgress && 
                     notification.status === DocumentProcessingStatus.InProgress) {
            // Was not InProgress, now it is - increase count
            this.documentsInProgressCound.update((count) => count + 1);
          }
        }

      });

      this.initializeServices();

      this.isInitialized = true;
    } catch (error) {
      console.warn('⚠️ Could not initialize StateService event handlers:', error);
    }
  }

  private initializeServices(): void {
    this.historyService.apiHistoryInprogressCountGet$Plain().subscribe((resp: any) => {
      const ok = resp?.success !== false;
      const val: number = ok ? (resp?.data ?? 0) : 0;
      DevConsole.log('Initial documents in progress count (enveloped):', val, resp);
      this.documentsInProgressCound.set(val);
    })
  }

  ensureInitialized(): void {
    this.initializeEventHandlers();
  }
}
