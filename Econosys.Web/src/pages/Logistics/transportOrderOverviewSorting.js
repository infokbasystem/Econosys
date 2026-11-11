const collator = new Intl.Collator('sv-SE', { numeric: true, sensitivity: 'base' });

function getSortValue(row, key) {
    if (key === 'deliveryTime' || key === 'confirmedDeliveryTime') {
        return row[`${key}SortDate`] ?? row[key];
    }

    return row[key];
}

export function sortTransportOrderOverviewRows(rows, { key, direction }) {
    if (!key) return rows;

    return [...rows].sort((a, b) => {
        const left = getSortValue(a, key);
        const right = getSortValue(b, key);
        const leftEmpty = left == null || left === '';
        const rightEmpty = right == null || right === '';

        if (leftEmpty || rightEmpty) {
            return Number(leftEmpty) - Number(rightEmpty);
        }

        const comparison = typeof left === 'number' && typeof right === 'number'
            ? left - right
            : collator.compare(String(left), String(right));

        return direction === 'desc' ? -comparison : comparison;
    });
}
