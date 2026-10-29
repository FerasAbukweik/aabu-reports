// Latin digits (0-9) like the rest of the app, Arabic words
const RELATIVE = new Intl.RelativeTimeFormat('ar-u-nu-latn', { numeric: 'auto' });
const DATE = new Intl.DateTimeFormat('ar-u-nu-latn', { day: 'numeric', month: 'long', year: 'numeric' });
const DATE_TIME = new Intl.DateTimeFormat('ar-u-nu-latn', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
});

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** "now", "5 minutes ago", "yesterday"... for the last 7 days, the date itself after that */
export function formatRelativeDate(iso: string, now: number = Date.now()): string {
  const time = new Date(iso).getTime();
  if (Number.isNaN(time)) return '';

  const diff = now - time;

  if (diff < MINUTE) return 'الآن';
  if (diff < HOUR) return RELATIVE.format(-Math.floor(diff / MINUTE), 'minute');
  if (diff < DAY) return RELATIVE.format(-Math.floor(diff / HOUR), 'hour');
  if (diff < 7 * DAY) return RELATIVE.format(-Math.floor(diff / DAY), 'day');

  return DATE.format(time);
}

/** the full date and time, for the tooltip */
export function formatFullDate(iso: string): string {
  const time = new Date(iso).getTime();

  return Number.isNaN(time) ? '' : DATE_TIME.format(time);
}
