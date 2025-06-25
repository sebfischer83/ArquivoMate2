import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TuiTitle } from '@taiga-ui/core';
import { TuiBadge, TuiFade } from '@taiga-ui/kit';
import { TuiCard, TuiCardLarge, TuiCardMedium, TuiCell } from '@taiga-ui/layout';

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [TuiCard, TuiCardLarge, TuiCardMedium, TuiBadge, TuiFade, TuiTitle, TuiCell],
  templateUrl: './tasks.component.html',
  styleUrl: './tasks.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksComponent { }
