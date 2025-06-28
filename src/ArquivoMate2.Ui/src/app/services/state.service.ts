import { inject, Injectable, signal } from '@angular/core';
import { SignalrService } from './signalr.service';
import { DocumentProcessingNotification } from '../models/document-processing-notification';
import { ImportHistoryService } from '../client/services';
import { DocumentProcessingStatus } from '../models/document-processing-status';
import { DevConsole } from '../utils/console';
import * as lodash from 'lodash';

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
        
        // Check if notification with same documentId already exists using lodash
        const existingNotification = lodash.find(this.documentNotification(), { documentId: notification.documentId });
        
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
            // Remove existing notification with same documentId
            const filtered = lodash.filter(prev, (n) => n.documentId !== notification.documentId);
            // Add the new notification
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
    this.historyService.apiHistoryInprogressCountGet$Plain().subscribe((val) => {
      DevConsole.log('Initial documents in progress count:', val);
      this.documentsInProgressCound.set(val);
    })
  }

  ensureInitialized(): void {
    this.initializeEventHandlers();
  }
}
