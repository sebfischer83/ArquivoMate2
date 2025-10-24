import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoModule } from '@jsverse/transloco';
import { DocumentDto } from '../../../../../../client/models/document-dto';

@Component({
  selector: 'app-lab-results',
  standalone: true,
  imports: [CommonModule, TranslocoModule],
  templateUrl: './lab-results.component.html',
  styleUrls: ['./lab-results.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabResultsComponent {
  @Input() document: DocumentDto | null = null;
}
