import { ChangeDetectionStrategy, Component, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { DocumentsService } from '../../client/services/documents.service';
import { WeatherForecastService } from '../../client/services/weather-forecast.service';
import { KeyValuePipe, NgForOf, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { tuiAsPortal, TuiPortals, TuiThemeColorService } from '@taiga-ui/cdk';
import {
  TuiAppearance,
  TuiButton,
  TuiDataList,
  TuiDropdown,
  TuiDropdownService,
  TuiIcon,
  TuiLink,
  TuiRoot,
  TuiTextfield,
} from '@taiga-ui/core';
import {
  TuiAvatar,
  TuiBadge,
  TuiBadgeNotification,
  TuiBreadcrumbs,
  TuiChevron,
  TuiDataListDropdownManager,
  TuiFade,
  TuiSwitch,
  TuiTabs,
} from '@taiga-ui/kit';
import { TuiNavigation } from '@taiga-ui/layout';
import { WA_LOCAL_STORAGE, WA_WINDOW } from '@ng-web-apis/common';
import { TUI_DARK_MODE, TUI_DARK_MODE_KEY } from '@taiga-ui/core';
import { SignalrService } from '../../services/signalr.service';
import { OAuthService } from 'angular-oauth2-oidc';


const ICON =
  "data:image/svg+xml,%0A%3Csvg width='32' height='32' viewBox='0 0 32 32' fill='none' xmlns='http://www.w3.org/2000/svg'%3E%3Crect width='32' height='32' rx='8' fill='url(%23paint0_linear_2036_35276)'/%3E%3Cmask id='mask0_2036_35276' style='mask-type:alpha' maskUnits='userSpaceOnUse' x='6' y='5' width='20' height='21'%3E%3Cpath d='M18.2399 9.36607C21.1347 10.1198 24.1992 9.8808 26 7.4922C26 7.4922 21.5645 5 16.4267 5C11.2888 5 5.36726 8.69838 6.05472 16.6053C6.38707 20.4279 6.65839 23.7948 6.65839 23.7948C8.53323 22.1406 9.03427 19.4433 8.97983 16.9435C8.93228 14.7598 9.55448 12.1668 12.1847 10.4112C14.376 8.94865 16.4651 8.90397 18.2399 9.36607Z' fill='url(%23paint1_linear_2036_35276)'/%3E%3Cpath d='M11.3171 20.2647C9.8683 17.1579 10.7756 11.0789 16.4267 11.0789C20.4829 11.0789 23.1891 12.8651 22.9447 18.9072C22.9177 19.575 22.9904 20.2455 23.2203 20.873C23.7584 22.3414 24.7159 24.8946 24.7159 24.8946C23.6673 24.5452 22.8325 23.7408 22.4445 22.7058L21.4002 19.921L21.2662 19.3848C21.0202 18.4008 20.136 17.7104 19.1217 17.7104H17.5319L17.6659 18.2466C17.9119 19.2306 18.7961 19.921 19.8104 19.921L22.0258 26H10.4754C10.7774 24.7006 12.0788 23.2368 11.3171 20.2647Z' fill='url(%23paint2_linear_2036_35276)'/%3E%3C/mask%3E%3Cg mask='url(%23mask0_2036_35276)'%3E%3Crect x='4' y='4' width='24' height='24' fill='white'/%3E%3C/g%3E%3Cdefs%3E%3ClinearGradient id='paint0_linear_2036_35276' x1='0' y1='0' x2='32' y2='32' gradientUnits='userSpaceOnUse'%3E%3Cstop stop-color='%23A681D4'/%3E%3Cstop offset='1' stop-color='%237D31D4'/%3E%3C/linearGradient%3E%3ClinearGradient id='paint1_linear_2036_35276' x1='6.0545' y1='24.3421' x2='28.8119' y2='3.82775' gradientUnits='userSpaceOnUse'%3E%3Cstop offset='0.0001' stop-opacity='0.996458'/%3E%3Cstop offset='0.317708'/%3E%3Cstop offset='1' stop-opacity='0.32'/%3E%3C/linearGradient%3E%3ClinearGradient id='paint2_linear_2036_35276' x1='6.0545' y1='24.3421' x2='28.8119' y2='3.82775' gradientUnits='userSpaceOnUse'%3E%3Cstop offset='0.0001' stop-opacity='0.996458'/%3E%3Cstop offset='0.317708'/%3E%3Cstop offset='1' stop-opacity='0.32'/%3E%3C/linearGradient%3E%3C/defs%3E%3C/svg%3E%0A";


@Component({
  standalone: true,
  selector: 'app-main-area',
  imports: [RouterOutlet,
    FormsModule,
    TuiRoot,
    KeyValuePipe,
    NgForOf,
    NgIf,
    RouterLink,
    RouterLinkActive,
    TuiAppearance,
    TuiAvatar,
    TuiBadge,
    TuiBadgeNotification,
    TuiButton,
    TuiChevron,
    TuiDataList,
    TuiDataListDropdownManager,
    TuiDropdown,
    TuiFade,
    TuiIcon,
    TuiNavigation,
    TuiSwitch,
    TuiTabs,
    TuiTextfield,
  ],
  templateUrl: './main-area.page.html',
  styleUrl: './main-area.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainAreaComponent implements OnInit, OnDestroy {
  private readonly key = inject(TUI_DARK_MODE_KEY);
  private readonly storage = inject(WA_LOCAL_STORAGE);
  private readonly media = inject(WA_WINDOW).matchMedia('(prefers-color-scheme: dark)');
  private documentsService = inject(DocumentsService);
  private weatherService = inject(WeatherForecastService);
  private signalRService = inject(SignalrService);
  private auth = inject(OAuthService);
  protected readonly darkMode = inject(TUI_DARK_MODE);
  private readonly themeService = inject(TuiThemeColorService);

  protected expanded = signal(false);
  protected open = false;
  protected switch = false;
  protected readonly routes: any = {};

  protected readonly drawer = {
    Components: [
      { name: 'Button', icon: ICON },
      { name: 'Input', icon: ICON },
      { name: 'Tooltip', icon: ICON },
    ],
    Essentials: [
      { name: 'Getting started', icon: ICON },
      { name: 'Showcase', icon: ICON },
      { name: 'Typography', icon: ICON },
    ],
  };

  /**
   *
   */
  constructor() {
    this.darkMode.set(this.storage.getItem(this.key) === 'true' || this.media.matches);

    effect(() => {
      if (this.darkMode()) {
        this.storage.setItem(this.key, 'true');
        this.themeService.color = "black";
      } else {
        this.storage.removeItem(this.key);
        this.storage.setItem(this.key, 'false');
        this.themeService.color = "var(--tui-background-accent-1)";
      }
    });

  }

  toggleDarkmode(): void {
    this.darkMode.set(!this.darkMode());
  }

  ngOnInit(): void {
    this.debugSignalR();
  }

  private async debugSignalR(): Promise<void> {
    console.log('🔍 Starting SignalR Debug...');
    console.log('Frontend Origin:', window.location.origin);
    
    // 1. Teste Backend-Erreichbarkeit erst
    const baseUrls = [
      'https://localhost:5000'
    ];

    for (const baseUrl of baseUrls) {
      console.log(`\n🌐 Testing server: ${baseUrl}`);
      
      try {
        // Teste einfachen API-Call
        const healthResponse = await fetch(`${baseUrl}/api/health`, {
          method: 'GET',
          mode: 'cors',
          credentials: 'include'
        });
        console.log(`✅ Health check: ${healthResponse.status} ${healthResponse.statusText}`);
        
        // Teste CORS preflight für SignalR
        const corsTest = await fetch(`${baseUrl}/hubs/documents/negotiate`, {
          method: 'OPTIONS',
          mode: 'cors',
          credentials: 'include',
          headers: {
            'Access-Control-Request-Method': 'POST',
            'Access-Control-Request-Headers': 'content-type'
          }
        });
        console.log(`🔄 CORS preflight: ${corsTest.status}`, corsTest.headers);
        
        // Teste SignalR negotiate
        const negotiateResponse = await fetch(`${baseUrl}/hubs/documents/negotiate?negotiateVersion=1`, {
          method: 'POST',
          mode: 'cors',
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${this.auth.getAccessToken() || ''}`
          }
        });
        console.log(`🤝 Negotiate: ${negotiateResponse.status}`, await negotiateResponse.text());
        
        if (negotiateResponse.ok) {
          await this.connectSignalR(`${baseUrl}/hubs/documents`);
          return;
        }
        
      } catch (error) {
        console.log(`❌ Failed: ${baseUrl}`, error);
      }
    }
    
    console.error('❌ No working SignalR endpoint found');
    console.log('💡 Check backend CORS configuration and ensure server is running');
  }

  private async connectSignalR(url: string): Promise<void> {
    try {
      await this.signalRService.startConnection(url);
      this.setupSignalREventHandlers();
      console.log('✅ SignalR connected successfully');
    } catch (error) {
      console.error('❌ SignalR connection failed:', error);
    }
  }

  private setupSignalREventHandlers(): void {
    const currentUserId = this.getCurrentUserId();
    console.log('👤 Setting up SignalR handlers for user:', currentUserId);

    // Event Handler für allgemeine Dokumenten-Updates
    this.signalRService.on<any>('DocumentProcessed', (data) => {
      console.log('📄 Document processed:', data);
      this.handleDocumentProcessed(data);
    });

    // Event Handler für Upload-Status Updates
    this.signalRService.on<any>('DocumentUploadProgress', (data) => {
      console.log('📤 Upload progress:', data);
      this.handleUploadProgress(data);
    });

    // Event Handler für Verarbeitungsfehler
    this.signalRService.on<any>('DocumentProcessingError', (data) => {
      console.log('❌ Processing error:', data);
      this.handleProcessingError(data);
    });

    // Event Handler für User-spezifische Benachrichtigungen
    this.signalRService.on<any>('UserNotification', (data) => {
      console.log('🔔 User notification:', data);
      this.handleUserNotification(data);
    });
  }

  private getCurrentUserId(): string | null {
    // Hole User ID aus JWT Token Claims
    const token = this.auth.getAccessToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.sub || payload.userId || payload.id || null;
    } catch {
      return null;
    }
  }

  private handleDocumentProcessed(data: any): void {
    // Zeige Toast-Notification oder update UI
    console.log('✅ Document processing completed:', data);
    // Hier könntest du z.B. eine Taiga UI Notification anzeigen
  }

  private handleUploadProgress(data: any): void {
    // Update Progress Bar oder Status
    console.log('⏳ Upload progress:', data.progress + '%');
  }

  private handleProcessingError(data: any): void {
    // Zeige Error-Notification
    console.error('❌ Document processing failed:', data.error);
  }

  private handleUserNotification(data: any): void {
    // Allgemeine User-Benachrichtigung
    console.log('📱 Notification for user:', data);
  }

    ngOnDestroy(): void {
    this.signalRService.stopConnection();
  }

  protected handleToggle(): void {
    this.expanded.update((e) => !e);
  }

}
