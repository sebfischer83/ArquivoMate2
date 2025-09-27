import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiButton } from '@taiga-ui/core';

@Component({
  selector: 'app-content-toolbar',
  standalone: true,
  imports: [CommonModule, TuiButton],
  template: `
  <div class="content-toolbar-root" tuiSurface appearance="floating">
    <div class="right">
      <button tuiButton size="xs" appearance="flat" type="button" (click)="toggleWrap.emit()" [iconStart]="wrap() ? '@tui.text' : '@tui.expand'">{{ wrap() ? 'Wrap aus' : 'Wrap an' }}</button>
      <button tuiButton size="xs" appearance="flat" type="button" (click)="copy.emit()" [iconStart]="copied() ? '@tui.check' : '@tui.copy'">{{ copied() ? 'Kopiert' : 'Copy' }}</button>
    </div>
  </div>
  `,
  styleUrls: ['./content-toolbar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContentToolbarComponent {
  @Input({required:true}) wrap = signal(true);
  @Input({required:true}) copied = signal(false);
  @Output() toggleWrap = new EventEmitter<void>();
  @Output() copy = new EventEmitter<void>();
}
