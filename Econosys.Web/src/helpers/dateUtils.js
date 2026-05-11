/**
 * Returns the ISO week number (1–53) for a given date.
 */
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
    return new Date(value).toLocaleDateString('sv-SE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
};

export const formatDateShort = (value) => {
    if (!value) return '';
    return new Date(value).toLocaleDateString('sv-SE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    });
};

export const formatDateTime = (value) => {
    if (!value) return '';
    return new Date(value).toLocaleString('sv-SE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    });
};
