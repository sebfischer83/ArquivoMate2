import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel, HubConnectionState, HttpTransportType } from '@microsoft/signalr';
import { OAuthService } from 'angular-oauth2-oidc';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection!: HubConnection;
  private auth = inject(OAuthService);
  private onReconnected?: () => void;
  private pendingHandlers: Array<{ event: string, callback: (data: any) => void }> = [];

  constructor() { }
  startConnection(hubUrl: string): Promise<void> {
    console.log('Starting SignalR connection to:', hubUrl);
    
    // Verbesserte Builder-Konfiguration
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Authentication
        accessTokenFactory: () => {
          const token = this.auth.getAccessToken();
          console.log('🔑 Providing access token for SignalR:', token ? 'Token available' : 'No token');
          return token || '';
        },        
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
        skipNegotiation: false,
        withCredentials: false,
        timeout: 30000,
        headers: {
          'Authorization': `Bearer ${this.auth.getAccessToken() || ''}`
        }
      })
      .configureLogging(LogLevel.Warning) 
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build();   
    this.hubConnection.onclose((error) => {
      console.error('SignalR connection closed:', error);
      if (error) {
        console.error('Close error details:', {
          message: error.message,
          name: error.name,
          stack: error.stack
        });
      }
    });

    this.hubConnection.onreconnecting((error) => {
      console.warn('SignalR reconnecting:', error);
      if (error) {
        console.warn('Reconnecting error details:', {
          message: error.message,
          name: error.name
        });
      }
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('SignalR reconnected with ID:', connectionId);
      // Registriere Handler nach Reconnect erneut
      this.registerPendingHandlers();
      // Event-Handler müssen nach Reconnect erneut registriert werden
      this.onReconnected?.();
    });    return this.hubConnection
      .start()
      .then(() => {
       
        // Registriere zwischengespeicherte Handler
        this.registerPendingHandlers();
      })
      .catch(err => {
        console.error('❌ SignalR Connection Error: ', err);
        console.error('🔍 Error details:', {
          message: err.message,
          statusCode: err.statusCode,
          errorType: err.errorType,
          url: hubUrl,
          transport: err.transport,
          // Zusätzliche Debug-Informationen
          hasToken: !!this.auth.getAccessToken(),
          tokenLength: this.auth.getAccessToken()?.length || 0
        });
      });
  }

  // Hilfsmethoden für Debugging
  getConnectionState(): HubConnectionState {
    return this.hubConnection?.state || HubConnectionState.Disconnected;
  }

  isConnected(): boolean {
    return this.hubConnection?.state === HubConnectionState.Connected;
  }

  setReconnectCallback(callback: () => void): void {
    this.onReconnected = callback;
  }
  on<T>(event: string, callback: (data: T) => void): void {
    // Handler zwischenspeichern
    this.pendingHandlers.push({ event, callback });

    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      console.log(`🔗 Registering SignalR handler for event: ${event}`);
      this.hubConnection.on(event, callback);
    } else {
      console.log(`📋 Queueing handler for '${event}' - will register when connected`);
    }
  }

  private registerPendingHandlers(): void {
    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      console.log(`🔗 Registering ${this.pendingHandlers.length} pending SignalR handlers`);
      
      for (const handler of this.pendingHandlers) {
        console.log(`   - Registering: ${handler.event}`);
        this.hubConnection.on(handler.event, handler.callback);
      }
    }
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

}
