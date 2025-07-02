import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { TuiRoot } from '@taiga-ui/core';
	import {TuiActionBar} from '@taiga-ui/kit';
import { OAuthService } from 'angular-oauth2-oidc';

@Component({
  selector: 'app-landing-page',
  imports: [TuiRoot, TuiActionBar, TranslocoDirective],
  standalone: true,
  templateUrl: './landingPage.component.html',
  styleUrl: './landingPage.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPageComponent { 
  auth = inject(OAuthService);

}
