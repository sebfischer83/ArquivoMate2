import { ChangeDetectionStrategy, Component, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TuiThemeColorService } from '@taiga-ui/cdk';
import { TuiButton, TuiIcon, TuiPopup, TUI_DARK_MODE, TUI_DARK_MODE_KEY } from '@taiga-ui/core';
import { TuiBadgeNotification, TuiChevron, TuiDrawer } from '@taiga-ui/kit';
import { TuiHeader, TuiNavigation } from '@taiga-ui/layout';
import { WA_LOCAL_STORAGE, WA_WINDOW } from '@ng-web-apis/common';
import { SignalrService } from '../../services/signalr.service';
import { StateService } from '../../services/state.service';
import { OAuthService } from 'angular-oauth2-oidc';
import { TasksComponent } from '../sidebar/tasks/tasks.component';
import { LanguageSwitcherComponent } from '../../components/language-switcher/language-switcher.component';

@Component({
  standalone: true,
  selector: 'app-main-area',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TuiBadgeNotification,
    TuiButton,
    TuiChevron,
    TuiDrawer,
    TuiIcon,
    TuiNavigation,
    TuiHeader,
    TuiPopup,
    TasksComponent,
    LanguageSwitcherComponent,
  ],
  templateUrl: './main-area.page.html',
  styleUrl: './main-area.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MainAreaComponent implements OnInit, OnDestroy {
  private readonly key = inject(TUI_DARK_MODE_KEY);
  private readonly storage = inject(WA_LOCAL_STORAGE);
  private readonly media = inject(WA_WINDOW).matchMedia('(prefers-color-scheme: dark)');
  private readonly signalRService = inject(SignalrService);
  protected readonly stateService = inject(StateService);
  private readonly auth = inject(OAuthService);
  protected readonly darkMode = inject(TUI_DARK_MODE);
  private readonly themeService = inject(TuiThemeColorService);

  protected readonly expanded = signal(false);
  protected readonly openDrawer = signal(false);

  constructor() {
    this.darkMode.set(this.storage.getItem(this.key) === 'true' || this.media.matches);

    effect(() => {
      if (this.darkMode()) {
        this.storage.setItem(this.key, 'true');
        this.themeService.color = 'black';
      } else {
        this.storage.setItem(this.key, 'false');
        this.themeService.color = 'var(--tui-background-accent-1)';
      }
    });
  }

  toggleDarkmode(): void {
    this.darkMode.set(!this.darkMode());
  }

  async ngOnInit(): Promise<void> {
    const baseUrl = 'http://localhost:5000';
    await this.connectSignalR(`${baseUrl}/hubs/documents`);
  }

  private async connectSignalR(url: string): Promise<void> {
    try {
      await this.signalRService.startConnection(url);
      this.setupSignalREventHandlers();
    } catch (error) {
      console.warn('⚠️ Connection failed ', error);
    }
  }

  private setupSignalREventHandlers(): void {
    const currentUserId = this.getCurrentUserId();
    if (currentUserId) {
      this.stateService.ensureInitialized();
    }
  }

  private getCurrentUserId(): string | null {
    const token = this.auth.getAccessToken();
    if (!token) {
      return null;
    }

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.sub || payload.userId || payload.id || null;
    } catch {
      return null;
    }
  }

  ngOnDestroy(): void {
    this.signalRService.stopConnection();
  }

  protected handleToggle(): void {
    this.expanded.update(state => !state);
  }
}
