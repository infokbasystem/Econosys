/**
 * Returns the ISO week number (1–53) for a given date.
 */
const SWEDISH_LOCALE = 'sv-SE';
const SWEDISH_TIME_ZONE = 'Europe/Stockholm';

const SWEDISH_DATE_FORMATTER = new Intl.DateTimeFormat(SWEDISH_LOCALE, {
    timeZone: SWEDISH_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
});

const toValidDate = (value) => {
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
};

const getISOWeek = (date) => {
    const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
    d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
    const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
    return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
};

/**
 * Formats a date as "v.26 2026" (ISO week + year).
 */
export const formatDeliveryWeek = (value) => {
    if (!value) return '';
    const date = new Date(value);
    const week = getISOWeek(date);
    // Use the year of the Thursday of that week (ISO week year)
    const thursday = new Date(date);
    thursday.setDate(date.getDate() + 4 - (date.getDay() || 7));
    return `v.${week} ${thursday.getFullYear()}`;
};

/**
 * Formats a delivery date: uses week format if weekMode is true, otherwise yyyy-mm-dd.
 */
export const formatDeliveryDate = (value, weekMode) => {
    if (!value) return '';
    if (weekMode) return formatDeliveryWeek(value);
    return new Date(value).toLocaleDateString(SWEDISH_LOCALE, {
        timeZone: SWEDISH_TIME_ZONE,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
};

export const formatDateShort = (value) => {
    if (!value) return '';
    return new Date(value).toLocaleDateString(SWEDISH_LOCALE, {
        timeZone: SWEDISH_TIME_ZONE,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
};

export const formatDateTime = (value) => {
    if (!value) return '';
    return new Date(value).toLocaleString(SWEDISH_LOCALE, {
        timeZone: SWEDISH_TIME_ZONE,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    });
};

export const toSwedishDateInputValue = (value) => {
    const date = toValidDate(value);
    return date ? SWEDISH_DATE_FORMATTER.format(date) : '';
};

export const fromDateInputToSwedishIso = (value) => {
    if (!value) return null;

    const [year, month, day] = value.split('-').map((part) => Number(part));
    if (!year || !month || !day) {
        return null;
    }

    const paddedYear = String(year).padStart(4, '0');
    const paddedMonth = String(month).padStart(2, '0');
    const paddedDay = String(day).padStart(2, '0');
    return `${paddedYear}-${paddedMonth}-${paddedDay}T00:00:00`;
};

export const getSwedishTodayDateString = () => toSwedishDateInputValue(new Date());

export const toSwedishDateBoundaryIso = (value, endOfDay = false) => {
    const datePart = toSwedishDateInputValue(value);
    if (!datePart) return null;
    return `${datePart}T${endOfDay ? '23:59:59' : '00:00:00'}`;
};
