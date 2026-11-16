import assert from 'node:assert/strict';
import test from 'node:test';
import { setImmediate } from 'node:timers/promises';
import { getItemTabLock, startItemTabLock } from './itemTabLock.js';

const createAsyncLockManager = () => {
    const heldLocks = new Set();
    return {
        request: (name, _options, callback) => {
            const lock = heldLocks.has(name) ? null : { name };
            if (lock) heldLocks.add(name);
            return Promise.resolve()
                .then(() => callback(lock))
                .finally(() => {
                    if (lock) heldLocks.delete(name);
                });
        },
    };
};

test('creates a separate lock for each supported item detail route', () => {
    const paths = [
        ['/order/calculations/12', 'order:calculations', '/order/calculations'],
        ['/order/products/12', 'order:products', '/order/products'],
        ['/order/customers/12', 'order:customers', '/order/customers'],
        ['/order/suppliers/12', 'order:suppliers', '/order/suppliers'],
        ['/order/inquiries/12', 'order:inquiries', '/order/inquiries'],
        ['/order/quotations/12', 'order:quotations', '/order/quotations'],
        ['/order/supplierorders/12', 'order:supplierorders', '/order/supplierorders'],
        ['/order/customerorders/12', 'order:customerorders', '/order/customerorders'],
        ['/logistics/transportorder/12', 'logistics:transportorder', '/logistics/transportorderoverivew'],
        ['/logistics/calloff/12', 'logistics:calloff', '/logistics/calloffoverview'],
        ['/management/deviations/12', 'management:deviations', '/management/deviations'],
        ['/finance/invoice/12', 'finance:invoice', '/finance/searchinvoice'],
        ['/budget/12', 'budget', '/budget'],
    ];

    for (const [pathname, resource, listPath] of paths) {
        assert.deepEqual(getItemTabLock(pathname), {
            lockName: `econosys:item:${resource}:12`,
            listPath,
        });
    }

    assert.notEqual(getItemTabLock('/order/calculations/12').lockName, getItemTabLock('/order/calculations/13').lockName);
});

test('does not lock list pages, new drafts, or non-numeric routes', () => {
    for (const pathname of [
        '/order/calculations',
        '/order/calculations/new',
        '/order/calculations/not-an-id',
        '/order/customers/12/extra',
        '/budget/new',
    ]) {
        assert.equal(getItemTabLock(pathname), null);
    }
});

test('holds an acquired lock until cleanup and uses a non-blocking exclusive request', async () => {
    let requestOptions;
    let heldLock;
    const statuses = [];
    const lockManager = {
        request: (name, options, callback) => {
            assert.equal(name, 'econosys:item:order:calculations:12');
            requestOptions = options;
            heldLock = callback({});
            return Promise.resolve(heldLock);
        },
    };

    const cleanup = startItemTabLock(
        'econosys:item:order:calculations:12',
        lockManager,
        (status) => statuses.push(status),
        (error) => { throw error; },
    );

    await setImmediate();
    assert.deepEqual(requestOptions, { mode: 'exclusive', ifAvailable: true });
    assert.deepEqual(statuses, ['acquired']);
    cleanup();
    await heldLock;
});

test('reports duplicate tabs, unsupported browsers, and lock errors', async () => {
    const statuses = [];
    let lockError;
    const duplicateCleanup = startItemTabLock(
        'item',
        { request: (_name, _options, callback) => Promise.resolve(callback(null)) },
        (status) => statuses.push(status),
        (error) => { lockError = error; },
    );
    await setImmediate();
    assert.deepEqual(statuses, ['duplicate']);
    duplicateCleanup();

    const unsupportedCleanup = startItemTabLock(
        'item',
        null,
        (status) => statuses.push(status),
        (error) => { lockError = error; },
    );
    assert.equal(statuses.at(-1), 'unsupported');
    unsupportedCleanup();

    const expectedError = new Error('lock failed');
    const errorCleanup = startItemTabLock(
        'item',
        { request: () => Promise.reject(expectedError) },
        (status) => statuses.push(status),
        (error) => { lockError = error; },
    );
    await setImmediate();
    assert.equal(lockError, expectedError);
    errorCleanup();
});

test('effect cleanup and replay do not mistake the previous request for another tab', async () => {
    const lockManager = createAsyncLockManager();
    const statuses = [];
    const errors = [];
    const firstCleanup = startItemTabLock(
        'effect-replay',
        lockManager,
        (status) => statuses.push(`first:${status}`),
        (error) => errors.push(error),
    );
    firstCleanup();
    const secondCleanup = startItemTabLock(
        'effect-replay',
        lockManager,
        (status) => statuses.push(`second:${status}`),
        (error) => errors.push(error),
    );

    try {
        await setImmediate();
        assert.deepEqual(statuses, ['second:acquired']);
        assert.deepEqual(errors, []);
    } finally {
        secondCleanup();
    }
});

test('reopening an item waits for this tab to finish releasing its previous lock', async () => {
    const lockManager = createAsyncLockManager();
    const statuses = [];
    const errors = [];
    const firstCleanup = startItemTabLock(
        'reopened-item',
        lockManager,
        (status) => statuses.push(`first:${status}`),
        (error) => errors.push(error),
    );
    await setImmediate();
    firstCleanup();
    const secondCleanup = startItemTabLock(
        'reopened-item',
        lockManager,
        (status) => statuses.push(`second:${status}`),
        (error) => errors.push(error),
    );

    try {
        await setImmediate();
        assert.deepEqual(statuses, ['first:acquired', 'second:acquired']);
        assert.deepEqual(errors, []);
    } finally {
        secondCleanup();
    }
});
