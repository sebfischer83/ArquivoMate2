import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TuiTitle } from '@taiga-ui/core';
import { TuiFade } from '@taiga-ui/kit';
import { TuiCell } from '@taiga-ui/layout';

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [TuiFade, TuiTitle, TuiCell],
  templateUrl: './tasks.component.html',
  styleUrl: './tasks.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TasksComponent { }
