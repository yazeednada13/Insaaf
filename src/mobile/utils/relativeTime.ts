/**
 * Arabic relative time for review timestamps (decision D6).
 * Matches the Figma examples ("منذ دقيقتين", "منذ ساعة", "منذ 3 ساعات", "منذ يومين", "منذ 3 أيام",
 * "منذ أسبوع"); cases beyond those examples ("الآن", weeks 2–4, dates after 30 days) follow the
 * same Arabic number agreement and are not specified by Figma.
 */

interface UnitForms {
  one: string;
  two: string;
  few: string; // 3–10
  many: string; // 11+
}

const MINUTE: UnitForms = { one: 'دقيقة', two: 'دقيقتين', few: 'دقائق', many: 'دقيقة' };
const HOUR: UnitForms = { one: 'ساعة', two: 'ساعتين', few: 'ساعات', many: 'ساعة' };
const DAY: UnitForms = { one: 'يوم', two: 'يومين', few: 'أيام', many: 'يومًا' };
const WEEK: UnitForms = { one: 'أسبوع', two: 'أسبوعين', few: 'أسابيع', many: 'أسبوعًا' };

function ago(count: number, forms: UnitForms): string {
  if (count === 1) {
    return `منذ ${forms.one}`;
  }
  if (count === 2) {
    return `منذ ${forms.two}`;
  }
  return `منذ ${count} ${count <= 10 ? forms.few : forms.many}`;
}

export function formatRelativeTime(isoTimestamp: string, now: Date = new Date()): string {
  const timestamp = new Date(isoTimestamp);
  const seconds = Math.max(0, Math.floor((now.getTime() - timestamp.getTime()) / 1000));

  const minutes = Math.floor(seconds / 60);
  if (minutes < 1) {
    return 'الآن';
  }
  if (minutes < 60) {
    return ago(minutes, MINUTE);
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return ago(hours, HOUR);
  }

  const days = Math.floor(hours / 24);
  if (days < 7) {
    return ago(days, DAY);
  }
  if (days < 30) {
    return ago(Math.floor(days / 7), WEEK);
  }

  // Older than a month: a plain day/month/year date (Western digits, as in the Figma copy).
  return `${timestamp.getDate()}/${timestamp.getMonth() + 1}/${timestamp.getFullYear()}`;
}
