import assert from 'node:assert/strict';
import test from 'node:test';
import { getTabTableTarget, getVerticalTableTarget, handleCalculationTableKeyDown } from './calculationTableNavigation.js';

const fixture = (layout) => {
    const frameCallbacks = [];
    const document = {
        activeElement: null,
        defaultView: {
            getComputedStyle: (element) => ({ visibility: element.visibility ?? 'visible' }),
            requestAnimationFrame: (callback) => frameCallbacks.push(callback),
        },
        querySelectorAll: () => controls,
    };
    const table = { tBodies: [{ rows: [] }] };
    const controls = [];
    for (const cells of layout) {
        const row = { cells: [], closest: () => table };
        table.tBodies[0].rows.push(row);
        for (const definitions of cells) {
            const cell = { cellIndex: row.cells.length, parentElement: row, querySelectorAll: () => cell.controls };
            row.cells.push(cell);
            cell.controls = definitions.map((definition) => {
                const element = {
                    tabIndex: 0,
                    type: 'text',
                    ownerDocument: document,
                    isConnected: true,
                    getClientRects() { return this.hidden ? [] : [{}]; },
                    closest(selector) {
                        if (selector === 'td') return cell;
                        if (selector === 'table') return table;
                        if (selector.startsWith('[hidden]')) return this.inert ? {} : null;
                        return this;
                    },
                    matches(selector) {
                        if (selector.startsWith(':disabled')) return this.disabled || this.readOnly;
                        if (selector === 'input[type="radio"]') return this.type === 'radio';
                        return this.type === 'text';
                    },
                    focus() { document.activeElement = this; },
                    select() { this.selected = true; },
                    getRootNode: () => document,
                    ...definition,
                };
                controls.push(element);
                return element;
            });
        }
    }
    const event = (source, key, extras = {}) => ({
        currentTarget: table,
        target: source,
        key,
        preventDefault() { this.defaultPrevented = true; },
        stopPropagation() { this.stopped = true; },
        ...extras,
    });
    return { table, controls, document, frameCallbacks, event };
};

test('vertical navigation skips disabled, read-only, hidden and inert rows', () => {
    const { table, controls } = fixture([
        [[{}]], [[{ disabled: true }]], [[{ readOnly: true }]], [[{ hidden: true }]],
        [[{ inert: true }]], [[{ visibility: 'hidden' }]], [[{}]],
    ]);
    assert.equal(getVerticalTableTarget(table, controls[0], 1), controls[6]);
    assert.equal(getVerticalTableTarget(table, controls[6], -1), controls[0]);
    assert.equal(getVerticalTableTarget(table, controls[0], -1), null);
    assert.equal(getVerticalTableTarget(table, controls[6], 1), null);
});

test('composite cells retain the control position including disabled siblings', () => {
    const { table, controls } = fixture([
        [[{ type: 'button' }], [{}, { type: 'select' }]],
        [[{ type: 'button' }], [{ disabled: true }, { type: 'select' }]],
        [[{ type: 'button' }], [{}, { type: 'select' }]],
    ]);
    assert.equal(getVerticalTableTarget(table, controls[1], 1), controls[7]);
    assert.equal(getVerticalTableTarget(table, controls[2], 1), controls[5]);
    assert.equal(getVerticalTableTarget(table, controls[0], 1), controls[3]);
});

test('Enter and Shift+Enter follow tab order and skip unavailable controls', () => {
    const { controls, event, document } = fixture([[[{}], [{ disabled: true }], [{}], [{ tabIndex: -1 }], [{}]]]);
    handleCalculationTableKeyDown(event(controls[0], 'Enter'));
    assert.equal(document.activeElement, controls[2]);
    handleCalculationTableKeyDown(event(controls[4], 'Enter', { shiftKey: true }));
    assert.equal(document.activeElement, controls[2]);
    assert.equal(getTabTableTarget(controls[4], 1), null);
});

test('tab order respects positive tabindex and radio groups', () => {
    const { controls } = fixture([[
        [{}], [{ tabIndex: 2 }], [{ tabIndex: 1 }],
        [{ type: 'radio', name: 'choice' }], [{ type: 'radio', name: 'choice', checked: true }], [{}],
    ]]);
    assert.equal(getTabTableTarget(controls[2], 1), controls[1]);
    assert.equal(getTabTableTarget(controls[1], 1), controls[0]);
    assert.equal(getTabTableTarget(controls[0], 1), controls[4]);
    assert.equal(getTabTableTarget(controls[4], 1), controls[5]);
});

test('navigation consumes keys before controls activate and selects text after focus formatting', () => {
    const { controls, event, document, frameCallbacks } = fixture([[[{}]], [[{}]]]);
    const keyEvent = event(controls[0], 'ArrowDown');
    handleCalculationTableKeyDown(keyEvent);
    assert.equal(document.activeElement, controls[1]);
    assert.equal(keyEvent.defaultPrevented, true);
    assert.equal(keyEvent.stopped, true);
    assert.equal(controls[1].selected, undefined);
    frameCallbacks[0]();
    assert.equal(controls[1].selected, true);
});

test('boundaries consume navigation without wrapping or triggering an action', () => {
    const { controls, event, document } = fixture([[[{ type: 'button' }]]]);
    for (const key of ['ArrowUp', 'ArrowDown', 'Enter']) {
        const keyEvent = event(controls[0], key);
        handleCalculationTableKeyDown(keyEvent);
        assert.equal(keyEvent.defaultPrevented, true);
        assert.equal(keyEvent.stopped, true);
        assert.equal(document.activeElement, null);
    }
});

test('empty and all-disabled tables have no navigation destinations', () => {
    const empty = fixture([]);
    assert.equal(getTabTableTarget({ ownerDocument: empty.document }, 1), null);
    const { table, controls, event } = fixture([[[{}]], [[{ disabled: true }]]]);
    assert.equal(getVerticalTableTarget(table, controls[0], 1), null);
    const keyEvent = event(controls[1], 'Enter');
    handleCalculationTableKeyDown(keyEvent);
    assert.equal(keyEvent.stopped, undefined);
});

test('deferred selection never steals selection after focus has moved', () => {
    const { controls, event, frameCallbacks, document } = fixture([[[{}]], [[{}]]]);
    handleCalculationTableKeyDown(event(controls[0], 'ArrowDown'));
    document.activeElement = controls[0];
    frameCallbacks[0]();
    assert.equal(controls[1].selected, undefined);
});

test('unrelated keys, shortcuts, composition and handled events are unchanged', () => {
    const { controls, event } = fixture([[[{}]], [[{}]]]);
    for (const [key, extras] of [
        ['Tab', {}], [' ', {}], ['ArrowLeft', {}], ['Enter', { altKey: true }],
        ['Enter', { ctrlKey: true }], ['Enter', { metaKey: true }],
        ['Enter', { isComposing: true }], ['Enter', { nativeEvent: { isComposing: true } }],
        ['ArrowDown', { shiftKey: true }], ['Enter', { defaultPrevented: true }],
    ]) {
        const keyEvent = event(controls[0], key, extras);
        handleCalculationTableKeyDown(keyEvent);
        assert.equal(keyEvent.stopped, undefined);
    }
});
