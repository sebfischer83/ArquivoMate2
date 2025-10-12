import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TuiButton, TuiTitle } from '@taiga-ui/core';
import { TuiHeader } from '@taiga-ui/layout';

interface SettingsSection {
  readonly label: string;
  readonly description: string;
  readonly link: string;
}

@Component({
  standalone: true,
  selector: 'app-settings',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TuiButton, TuiHeader, TuiTitle],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsComponent {
  protected readonly sections: readonly SettingsSection[] = [
    {
      label: 'E-Mail',
      description: 'Postfächer und Abrufregeln verwalten',
      link: 'email',
    },
  ];

  // No body scroll workaround anymore — layout is handled by main-area flex rules
}
