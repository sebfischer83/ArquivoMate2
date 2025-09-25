import { ChangeDetectionStrategy, Component, Input, Signal, computed, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { DocumentEventDto } from '../../client/models/document-event-dto';

@Component({
	standalone: true,
	selector: 'app-document-history',
		imports: [CommonModule],
	templateUrl: './document-history.component.html',
	styleUrls: ['./document-history.component.scss'],
	changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentHistoryComponent {
	private _events = signal<DocumentEventDto[]>([]);
	@Input() set events(value: DocumentEventDto[] | null | undefined) {
		this._events.set(value ?? []);
		const list = this.sortedEvents();
		if (list.length) this.selectEvent(list[list.length - 1]);
		else this.selectedEvent.set(null);
	}

	sortedEvents: Signal<DocumentEventDto[]> = computed(() =>
		[...this._events()].sort(
			(a, b) => new Date(a.occurredOn ?? 0).getTime() - new Date(b.occurredOn ?? 0).getTime()
		)
	);

	selectedEvent = signal<DocumentEventDto | null>(null);
	private rawPrettyJson: Signal<string> = computed(() => {
		const ev = this.selectedEvent();
		if (!ev?.data) return '';
		try {
			const parsed = JSON.parse(ev.data);
			return JSON.stringify(parsed, null, 2);
		} catch {
			return ev.data;
		}
	});

	// Lightweight syntax highlighting without adding a heavy dependency
	highlightedJson: Signal<SafeHtml | ''> = computed(() => {
		const content = this.rawPrettyJson();
		if (!content) return '';
		return this.sanitizer.bypassSecurityTrustHtml(this.colorizeJson(content));
	});

	constructor(private readonly sanitizer: DomSanitizer) {}

	private colorizeJson(json: string): string {
		// Escape existing HTML first to prevent accidental injection
		const escape = (str: string) =>
			str
				.replace(/&/g, '&amp;')
				.replace(/</g, '&lt;')
				.replace(/>/g, '&gt;')
				.replace(/\"/g, '&quot;');

		const escaped = escape(json);
		// Regex adapted from common JSON highlighters; differentiates keys vs. values
		return escaped.replace(/("(\\u[a-fA-F0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)/g, match => {
			let cls = 'json-token-number';
			if (match.startsWith('"')) {
				if (match.endsWith(':')) cls = 'json-token-key';
				else cls = 'json-token-string';
			} else if (match === 'true' || match === 'false') cls = 'json-token-boolean';
			else if (match === 'null') cls = 'json-token-null';
			return `<span class="${cls}">${match}</span>`;
		});
	}

	selectEvent(ev: DocumentEventDto) {
		this.selectedEvent.set(ev);
	}

	trackByTime = (_: number, ev: DocumentEventDto) => ev.occurredOn ?? Math.random().toString(36);

	// Expose a boolean for template placeholder condition
	get hasJson() { return !!this.rawPrettyJson(); }
}
