import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiTabs } from '@taiga-ui/kit';
import { TuiBadge } from '@taiga-ui/kit';

/**
 * Standalone Tabs-Header für das Dokument. Kapselt Taiga UI Tabs.
 * Sticky-Verhalten wird durch umgebendes Layout (parent) gesteuert per CSS.
 */
@Component({
  selector: 'app-document-tabs',
  standalone: true,
  imports: [CommonModule, TuiTabs, TuiBadge],
  template: `
    <div class="doc-tabs" [class.sticky]="sticky">
      <tui-tabs [(activeItemIndex)]="_active" (activeItemIndexChange)="onIndexChange($event)" size="m">
        <button tuiTab type="button" iconStart="@tui.file-text">Details</button>
        <button tuiTab type="button" iconStart="@tui.align-left">Inhalt</button>
        <button tuiTab type="button" iconStart="@tui.history">Historie</button>
        <button tuiTab type="button" iconStart="@tui.message-square">
          Notizen
          <tui-badge *ngIf="notesCount && notesCount > 0" size="s" appearance="primary" class="ml-badge">{{ notesCount }}</tui-badge>
        </button>
      </tui-tabs>
    </div>
  `,
  styleUrls: ['./document-tabs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentTabsComponent {
  @Input() sticky = true;
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
