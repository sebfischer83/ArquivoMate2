import { TuiRoot } from "@taiga-ui/core";
import { Component } from '@angular/core';
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

  constructor(private oauthService: OAuthService) {
    this.oauthService.configure(getAuthConfig());
    this.oauthService.loadDiscoveryDocument().then(() => {
      this.oauthService.tryLoginCodeFlow();
    });
  }
}