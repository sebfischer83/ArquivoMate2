import { ChangeDetectionStrategy, Component, Input, Signal, computed, signal, effect } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { prettyPrintJson } from 'pretty-print-json';
import { TuiButton } from '@taiga-ui/core';
// Removed DomSanitizer usage to avoid unsafe HTML bindings; using tokenization instead
import { CommonModule } from '@angular/common';
import { DocumentEventDto } from '../../client/models/document-event-dto';

// We rely on pretty-print-json for fallback static formatting.
// For interaction (collapse/expand) we build a lightweight JSON tree model.

@Component({
	standalone: true,
	selector: 'app-document-history',
	imports: [CommonModule, TuiButton],
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
	copied = signal(false);
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

	formattedJson: Signal<SafeHtml | ''> = computed(() => {
		const raw = this.rawPrettyJson();
		if (!raw) return '';
		if (!this.parsed()) {
			// raw might already be non-minified string (not JSON) -> escape
			const esc = raw.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
			return this.sanitizer.bypassSecurityTrustHtml(`<span class=\"ppj-raw\">${esc}</span>`);
		}
		try {
			const html = prettyPrintJson.toHtml(this.parsed(), { indent: 2, quoteKeys: true });
			return this.sanitizer.bypassSecurityTrustHtml(html);
		} catch {
			return '';
		}
	});

	// Parsed object for tree (null if invalid JSON)
	parsed: Signal<any | null> = computed(() => {
		const ev = this.selectedEvent();
		if (!ev?.data) return null;
		try { return JSON.parse(ev.data); } catch { return null; }
	});

	// Tree node model
	private buildTree(value: any, path: string, key?: string, depth = 0): JsonTreeNode {
		const type = Array.isArray(value)
			? 'array'
			: value !== null && typeof value === 'object'
				? 'object'
				: 'value';
		const node: JsonTreeNode = {
			path,
			key,
			type: type as any,
			value,
			collapsed: (type === 'object' || type === 'array') && (depth >= 2 || (Array.isArray(value) ? value.length > 30 : Object.keys(value || {}).length > 30)),
			children: []
		};
		if (type === 'object') {
			for (const k of Object.keys(value ?? {})) {
				node.children!.push(this.buildTree(value[k], `${path}.${k}`, k, depth + 1));
			}
		} else if (type === 'array') {
			value.forEach((v: any, i: number) => node.children!.push(this.buildTree(v, `${path}[${i}]`, String(i), depth + 1)));
		}
		return node;
	}

	// Root tree state persists; treeVersion used only to trigger change detection when mutating nodes
	treeRoot = signal<JsonTreeNode | null>(null);
	private treeVersion = signal(0);

	private _treeInit = effect(() => {
		const parsed = this.parsed();
		if (parsed === null) {
			this.treeRoot.set(null);
			return;
		}
		// Rebuild once when parsed changes
		this.treeRoot.set(this.buildTree(parsed, '$'));
	});

	toggle(node: JsonTreeNode) {
		if (node.type === 'value') return;
		node.collapsed = !node.collapsed;
		this.treeVersion.update(v => v + 1);
	}

	collapseAll() { this.walkTree(n => { if (n.type !== 'value') n.collapsed = true; }); }
	expandAll() { this.walkTree(n => { if (n.type !== 'value') n.collapsed = false; }); }

	private walkTree(mutator: (n: JsonTreeNode) => void) {
		const root = this.treeRoot();
		if (!root) return;
		const stack: JsonTreeNode[] = [root];
		while (stack.length) {
			const n = stack.pop()!;
			mutator(n);
			if (n.children) stack.push(...n.children);
		}
		this.treeVersion.update(v => v + 1);
	}

	copyJson() {
		const text = this.rawPrettyJson();
		if (!text) return;
		if (navigator?.clipboard?.writeText) {
			navigator.clipboard.writeText(text)
				.then(() => this.markCopied())
				.catch(() => { this.fallbackCopy(text); this.markCopied(); });
		} else {
			this.fallbackCopy(text);
			this.markCopied();
		}
	}

	private markCopied() {
		this.copied.set(true);
		setTimeout(() => this.copied.set(false), 2000);
	}

	private fallbackCopy(text: string) {
		try {
			const ta = document.createElement('textarea');
			ta.value = text;
			ta.style.position = 'fixed';
			ta.style.opacity = '0';
			document.body.appendChild(ta);
			ta.select();
			document.execCommand('copy');
			document.body.removeChild(ta);
		} catch { /* ignore */ }
	}

	valueClass(value: any): string {
		if (value === null) return 'jt-null';
		const t = typeof value;
		if (t === 'string') return 'jt-string';
		if (t === 'number') return 'jt-number';
		if (t === 'boolean') return 'jt-boolean';
		return 'jt-unknown';
	}

	formatValue(value: any): string {
		if (value === null) return 'null';
		if (typeof value === 'string') return '"' + value + '"';
		return String(value);
	}

	constructor(private sanitizer: DomSanitizer) {}

	selectEvent(ev: DocumentEventDto) {
		this.selectedEvent.set(ev);
	}

	trackByTime = (_: number, ev: DocumentEventDto) => ev.occurredOn ?? Math.random().toString(36);

	// Expose a boolean for template placeholder condition
	get hasJson() { return !!this.rawPrettyJson(); }
	// Expose version for template change detection hook
	get treeVersionValue() { return this.treeVersion(); }
}

interface JsonTreeNode {
	path: string;
	key?: string;
	type: 'object' | 'array' | 'value';
	value: any;
	children?: JsonTreeNode[];
	collapsed: boolean;
}
