import { TUI_DARK_MODE, TuiRoot } from "@taiga-ui/core";
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OAuthService } from "angular-oauth2-oidc";
import { getAuthConfig } from "./app.config";

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TuiRoot],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected title = 'ArquivoMate2.Ui';
  protected readonly darkMode = inject(TUI_DARK_MODE);

  constructor(private oauthService: OAuthService) {
    this.oauthService.configure(getAuthConfig());
    this.oauthService.loadDiscoveryDocument().then(() => {
      this.oauthService.tryLoginCodeFlow();
    });
  }
}