import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState } from '@microsoft/signalr';
import { OAuthService } from 'angular-oauth2-oidc';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection!: HubConnection;
  private auth = inject(OAuthService);

  constructor() { }

  startConnection(hubUrl: string): Promise<void> {
    console.log('Starting SignalR connection to:', hubUrl);
    
    // Builder mit erweiterten Optionen
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Falls Authentication benötigt wird
        accessTokenFactory: () => this.auth.getAccessToken() || '',
        // CORS und andere HTTP-Optionen
        withCredentials: true,
        // Timeout erhöhen
        timeout: 30000,
        // Transport fallback
        skipNegotiation: false
      })
      .configureLogging(LogLevel.Debug)
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build();

    // Erweiterte Event Handler
    this.hubConnection.onclose((error) => {
      console.error('SignalR connection closed:', error);
    });

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR reconnecting:', error);
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
    });

    return this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Connected successfully');
        console.log('Connection State:', this.hubConnection.state);
        console.log('Connection ID:', this.hubConnection.connectionId);
      })
      .catch(err => {
        console.error('SignalR Connection Error: ', err);
        console.error('Error details:', {
          message: err.message,
          statusCode: err.statusCode,
          errorType: err.errorType,
          url: hubUrl
        });
        throw err;
      });
  }

  // Hilfsmethoden für Debugging
  getConnectionState(): HubConnectionState {
    return this.hubConnection?.state || HubConnectionState.Disconnected;
  }

  isConnected(): boolean {
    return this.hubConnection?.state === HubConnectionState.Connected;
  }

  // Test-Methode um Server-Erreichbarkeit zu prüfen
  async testConnection(baseUrl: string): Promise<boolean> {
    try {
      const response = await fetch(`${baseUrl}/negotiate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${this.auth.getAccessToken()}`
        }
      });
      
      console.log('Negotiate response:', response.status, response.statusText);
      return response.ok;
    } catch (error) {
      console.error('Negotiate test failed:', error);
      return false;
    }
  }

  on<T>(event: string, callback: (data: T) => void): void {
    if (this.hubConnection) {
      this.hubConnection.on(event, callback);
    }
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }
}
