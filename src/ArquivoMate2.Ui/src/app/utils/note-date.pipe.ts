import { Pipe, PipeTransform } from '@angular/core';

// Lightweight date formatting pipe for note timestamps.
// Accepts ISO string or falsy; outputs localized date+time (de-DE fallback) or '-'.
@Pipe({ name: 'noteDate', standalone: true, pure: true })
export class NoteDatePipe implements PipeTransform {
  private readonly fmt = new Intl.DateTimeFormat('de-DE', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit'
  });
  transform(value?: string | null): string {
    if (!value) return '-';
    const d = new Date(value);
    if (isNaN(d.getTime())) return value; // fall back to raw
    return this.fmt.format(d);
  }
}