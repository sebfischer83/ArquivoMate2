import { Inject, Injectable, signal } from '@angular/core';
import { SignalrService } from './signalr.service';
import { DocumentProcessingNotification } from '../models/document-processing-notification';

@Injectable({
  providedIn: 'root'
})
export class StateService {
  readonly documentNotification = signal<DocumentProcessingNotification[]>([]);

  private signalRService = Inject(SignalrService);

  constructor() {
    this.signalRService.on('DocumentProcessingNotification', (notification: DocumentProcessingNotification) => {
      console.log('Received DocumentProcessingNotification:', notification);
      this.documentNotification.update((prev) => [...prev, notification]);
    });

  }
}
