import { inject, Injectable, signal } from '@angular/core';
import { SignalrService } from './signalr.service';
import { DocumentProcessingNotification } from '../models/document-processing-notification';

@Injectable({
  providedIn: 'root'
})
export class StateService {
  readonly documentNotification = signal<DocumentProcessingNotification[]>([]);

  private signalRService = inject(SignalrService);
  private isInitialized = false;

  constructor() {
    // Event-Handler werden nicht sofort registriert
    console.log('✅ StateService initialized (handlers will be registered on demand)');
  }

  /**
   * Initialisiert die SignalR Event-Handler
   * Wird automatisch beim ersten Zugriff aufgerufen
   */
  private initializeEventHandlers(): void {
    if (this.isInitialized) {
      return;
    }

    try {
      // Event-Handler registrieren
      this.signalRService.on('documentprocessingnotification', (notification: DocumentProcessingNotification) => {
        console.log('Received documentprocessingnotification:', notification);
        this.documentNotification.update((prev) => [...prev, notification]);
      });

      this.isInitialized = true;
      console.log('✅ StateService event handlers initialized');
    } catch (error) {
      console.warn('⚠️ Could not initialize StateService event handlers:', error);
    }
  }

  /**
   * Stellt sicher, dass Event-Handler initialisiert sind
   * Kann von außen aufgerufen werden
   */
  ensureInitialized(): void {
    this.initializeEventHandlers();
  }
}
