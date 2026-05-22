import { environment } from '../../../environments/environment';

const PERU_TIME_ZONE = environment.timeZone;
const PERU_LOCALE = environment.locale;

function getParts(
  value: Date | string | number,
  options: Intl.DateTimeFormatOptions,
): Record<string, string> {
  const formatter = new Intl.DateTimeFormat('en-CA', {
    timeZone: PERU_TIME_ZONE,
    ...options,
  });

  return formatter.formatToParts(new Date(value)).reduce<Record<string, string>>((acc, part) => {
    if (part.type !== 'literal') {
      acc[part.type] = part.value;
    }

    return acc;
  }, {});
}

export function formatPeruDate(value: Date | string | number = new Date()): string {
  const parts = getParts(value, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });

  return `${parts['year']}-${parts['month']}-${parts['day']}`;
}

export function formatPeruDateTimeLocal(value: Date | string | number = new Date()): string {
  const parts = getParts(value, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  });

  return `${parts['year']}-${parts['month']}-${parts['day']}T${parts['hour']}:${parts['minute']}`;
}

export function formatPeruDateTimeLabel(value: Date | string | number = new Date()): string {
  return new Intl.DateTimeFormat(PERU_LOCALE, {
    timeZone: PERU_TIME_ZONE,
    dateStyle: 'short',
    timeStyle: 'short',
    hourCycle: 'h23',
  }).format(new Date(value));
}
