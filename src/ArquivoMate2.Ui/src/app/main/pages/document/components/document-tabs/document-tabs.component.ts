import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiTabs } from '@taiga-ui/kit';
import { TuiBadge } from '@taiga-ui/kit';
import { TranslocoModule } from '@jsverse/transloco';

/**
 * Standalone Tabs-Header für das Dokument. Kapselt Taiga UI Tabs.
 * Sticky-Verhalten wird durch umgebendes Layout (parent) gesteuert per CSS.
 */
@Component({
  selector: 'app-document-tabs',
  standalone: true,
  imports: [CommonModule, TuiTabs, TuiBadge, TranslocoModule],
  templateUrl: './document-tabs.component.html',
  styleUrls: ['./document-tabs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentTabsComponent {
  @Input() sticky = true;
  @Input() showLabResults = false;
  /** Whether the current document is accepted; parent should pass document()?.accepted */
  @Input() documentAccepted = false;
  /** Pass the features declared for the document type (e.g. ['lab-results']) */
  @Input() documentTypeSystemFeatures: string[] | null = null;

  /** Compatibility helper: tab shows if either the explicit flag is set or the features include 'lab-results' */
  get hasLabResults(): boolean {
    return this.showLabResults || !!(this.documentTypeSystemFeatures && this.documentTypeSystemFeatures.includes('lab-results'));
  }
  @Input() notesCount: number | null = null;
  @Input() set activeIndex(v: number) { this._active = v ?? 0; }
  get activeIndex(): number { return this._active; }
  @Output() activeIndexChange = new EventEmitter<number>();
  _active = 0;
  onIndexChange(i: number) {
    this._active = i;
    this.activeIndexChange.emit(i);
  }
}
