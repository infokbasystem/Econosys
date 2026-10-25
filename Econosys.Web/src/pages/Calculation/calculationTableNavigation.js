const CONTROL_SELECTOR = 'input, select, textarea, button, a[href], [tabindex], [contenteditable="true"]';
const TAB_SELECTOR = `${CONTROL_SELECTOR}, area[href], summary, iframe, object, embed`;

const isAvailable = (element) => {
    if (element.tabIndex < 0 || element.matches(':disabled, [readonly], input[type="hidden"]')
        || element.closest('[hidden], [inert], [aria-hidden="true"]')
        || element.getClientRects().length === 0) return false;
    const style = element.ownerDocument.defaultView.getComputedStyle(element);
    return style.visibility !== 'hidden' && style.visibility !== 'collapse';
};

const cellControls = (cell) => Array.from(cell.querySelectorAll(CONTROL_SELECTOR));

export const getVerticalTableTarget = (table, source, direction) => {
    const cell = source.closest('td');
    const row = cell?.parentElement;
    if (!row || row.closest('table') !== table) return null;
    const controlIndex = cellControls(cell).indexOf(source);
    const rows = Array.from(table.tBodies).flatMap((body) => Array.from(body.rows));
    const index = rows.indexOf(row);
    if (index < 0 || controlIndex < 0) return null;

    for (let next = index + direction; next >= 0 && next < rows.length; next += direction) {
        const targetCell = rows[next].cells[cell.cellIndex];
        const target = targetCell && cellControls(targetCell)[controlIndex];
        if (target && isAvailable(target)) return target;
    }
    return null;
};

export const getTabTableTarget = (source, direction) => {
    const candidates = Array.from(source.ownerDocument.querySelectorAll(TAB_SELECTOR)).filter(isAvailable);
    const controls = candidates.filter((element) => {
        if (!element.matches('input[type="radio"]') || !element.name) return true;
        const group = candidates.filter((other) => other.matches('input[type="radio"]')
            && other.name === element.name && other.form === element.form
            && other.getRootNode() === element.getRootNode());
        return element === (group.find((other) => other.checked) ?? group[0]);
    });
    controls.sort((left, right) => (
        (left.tabIndex > 0 ? left.tabIndex : Infinity) - (right.tabIndex > 0 ? right.tabIndex : Infinity)
    ));
    const index = controls.indexOf(source);
    return index < 0 ? null : controls[index + direction] ?? null;
};

export const handleCalculationTableKeyDown = (event) => {
    if (event.defaultPrevented || event.isComposing || event.nativeEvent?.isComposing
        || event.altKey || event.ctrlKey || event.metaKey
        || !['ArrowUp', 'ArrowDown', 'Enter'].includes(event.key)
        || (event.shiftKey && event.key !== 'Enter')) return;

    const table = event.currentTarget;
    const source = event.target.closest(CONTROL_SELECTOR);
    if (!source || source.closest('table') !== table || !isAvailable(source)) return;
    const target = event.key === 'Enter'
        ? getTabTableTarget(source, event.shiftKey ? -1 : 1)
        : getVerticalTableTarget(table, source, event.key === 'ArrowDown' ? 1 : -1);

    event.preventDefault();
    event.stopPropagation();
    if (!target) return;
    target.focus();
    if (target.matches('input[type="text"], input:not([type]), textarea')) {
        target.ownerDocument.defaultView.requestAnimationFrame(() => {
            if (target.isConnected && target.ownerDocument.activeElement === target) target.select();
        });
    }
};
