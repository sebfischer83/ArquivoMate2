import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { NgOptimizedImage, DatePipe } from '@angular/common';
import { TuiBadge } from '@taiga-ui/kit';
import { DocumentListItemDto } from '../../client/models';

/**
 * Standalone presentational component for a single document card.
 * Keeps responsibility limited to displaying document metadata and emitting click events.
 * Future extensions: context menu, selection checkbox, status badges.
 */
@Component({
  selector: 'am-document-card',
  standalone: true,
  imports: [NgOptimizedImage, DatePipe, TuiBadge],
  templateUrl: './document-card.component.html',
  styleUrls: ['./document-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentCardComponent {
  /** The document to display */
  @Input({ required: true }) document!: DocumentListItemDto;
  /** Optional flag for showing a busy loader overlay (not currently used by grid) */
  @Input() busy = false;
  /** Visual variant controlling density & layout */
  @Input() variant: 'regular' | 'compact' | 'mini' = 'compact';
  /** Emitted when user clicks the card */
  @Output() cardClick = new EventEmitter<DocumentListItemDto>();

  onClick() {
    if (!this.document) return;
    this.cardClick.emit(this.document);
  }
}
