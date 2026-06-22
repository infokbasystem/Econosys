export const formatNumber = (value, digits = 0) => {
    if (value === null || value === undefined) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
    }).format(Number(value));
};

export const parseNullableNumber = (value) => {
    if (value === null || value === undefined || value === '') return null;
    if (typeof value === 'number') return Number.isFinite(value) ? value : null;
    const normalized = String(value).replace(/\s/g, '').replace(',', '.');
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};

export const parseNullableInt = (value) => {
    if (value === null || value === undefined || value === '') return null;
    if (typeof value === 'number') return Number.isInteger(value) ? value : Math.trunc(value);
    const parsed = parseInt(String(value), 10);
    return Number.isNaN(parsed) ? null : parsed;
};

export const roundTo2 = (value) => Math.round((Number(value) + Number.EPSILON) * 100) / 100;
