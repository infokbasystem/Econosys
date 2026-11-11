import assert from 'node:assert/strict';
import { setImmediate } from 'node:timers/promises';
import { test } from 'node:test';
import {
    DEFERRED_RELOAD_INTERVAL_MS,
    startAppUpdateChecker,
    UPDATE_CHECK_INTERVAL_MS,
} from './appUpdateChecker.js';
import { isAppUpdateBlocked, isEditingControl, registerAppUpdateGuard } from './appUpdateGuards.js';

const fixture = (context) => {
    context.mock.timers.enable({ apis: ['setInterval', 'setTimeout'] });
    const page = new EventTarget();
    page.visibilityState = 'visible';
    const browser = new EventTarget();
    const storage = new Map();
    let version = { buildId: 'current-build' };
    let blocked = false;
    let reloads = 0;
    const requests = [];
    const statuses = [];
    Object.assign(browser, {
        location: { href: 'https://example.invalid/order/supplierorders/123', reload: () => { reloads += 1; } },
        sessionStorage: { getItem: (key) => storage.get(key), setItem: (key, value) => storage.set(key, value) },
        setInterval, clearInterval, setTimeout, clearTimeout,
        fetch: async (url, options) => {
            requests.push({ url, options });
            return { ok: true, json: async () => version };
        },
    });
    const start = () => startAppUpdateChecker({
        buildId: 'current-build',
        versionUrl: '/version.json',
        isBlocked: () => blocked,
        onStatus: (status) => statuses.push(status),
        browser,
        page,
    });
    return {
        browser, page, requests, statuses, start,
        setVersion: (value) => { version = value; },
        setBlocked: (value) => { blocked = value; },
        get reloads() { return reloads; },
    };
};

test('checks immediately and exactly every 60 seconds without reloading the same build', async (context) => {
    const app = fixture(context);
    const stop = app.start();
    await setImmediate();
    assert.equal(app.requests.length, 1);
    assert.equal(app.requests[0].url.pathname, '/version.json');
    assert.ok(app.requests[0].url.searchParams.has('t'));
    assert.equal(app.requests[0].options.cache, 'no-store');
    context.mock.timers.tick(UPDATE_CHECK_INTERVAL_MS - 1);
    await setImmediate();
    assert.equal(app.requests.length, 1);
    context.mock.timers.tick(1);
    await setImmediate();
    assert.equal(app.requests.length, 2);
    assert.equal(app.reloads, 0);
    stop();
});

test('reloads a changed build, including a deployment rollback', async (context) => {
    const app = fixture(context);
    app.setVersion({ buildId: 'older-deployed-build' });
    const stop = app.start();
    await setImmediate();
    assert.equal(app.reloads, 1);
    context.mock.timers.tick(UPDATE_CHECK_INTERVAL_MS);
    await setImmediate();
    assert.equal(app.reloads, 1);
    stop();
});

test('defers while dirty or saving and reloads within one second once safe', async (context) => {
    const app = fixture(context);
    app.setVersion({ buildId: 'new-build' });
    app.setBlocked(true);
    const stop = app.start();
    await setImmediate();
    assert.equal(app.statuses.at(-1), 'pending');
    assert.equal(app.reloads, 0);
    app.setBlocked(false);
    context.mock.timers.tick(DEFERRED_RELOAD_INTERVAL_MS - 1);
    assert.equal(app.reloads, 0);
    context.mock.timers.tick(1);
    assert.equal(app.reloads, 1);
    stop();
});

test('checks on tab return and focus, skips hidden tabs and overlapping checks', async (context) => {
    const app = fixture(context);
    app.page.visibilityState = 'hidden';
    const stop = app.start();
    await setImmediate();
    assert.equal(app.requests.length, 0);
    app.page.visibilityState = 'visible';
    app.page.dispatchEvent(new Event('visibilitychange'));
    app.browser.dispatchEvent(new Event('focus'));
    await setImmediate();
    assert.equal(app.requests.length, 1);
    app.browser.dispatchEvent(new Event('focus'));
    await setImmediate();
    assert.equal(app.requests.length, 2);
    stop();
});

test('invalid versions and failed requests never reload and retry on the next interval', async (context) => {
    const app = fixture(context);
    const warn = context.mock.method(console, 'warn', () => {});
    app.setVersion({ buildId: '' });
    const stop = app.start();
    await setImmediate();
    assert.equal(app.statuses.at(-1), 'error');
    assert.equal(app.reloads, 0);
    app.browser.fetch = async () => ({ ok: false, status: 503 });
    context.mock.timers.tick(UPDATE_CHECK_INTERVAL_MS);
    await setImmediate();
    assert.equal(app.reloads, 0);
    app.browser.fetch = async () => { throw new Error('Offline'); };
    app.browser.dispatchEvent(new Event('focus'));
    await setImmediate();
    assert.equal(app.reloads, 0);
    assert.equal(warn.mock.callCount(), 3);
    app.browser.fetch = async () => ({ ok: true, json: async () => ({ buildId: 'new-build' }) });
    context.mock.timers.tick(UPDATE_CHECK_INTERVAL_MS);
    await setImmediate();
    assert.equal(app.reloads, 1);
    stop();
});

test('revalidates a deferred update on focus and cancels it if the current build is deployed again', async (context) => {
    const app = fixture(context);
    app.setVersion({ buildId: 'new-build' });
    app.setBlocked(true);
    const stop = app.start();
    await setImmediate();
    app.setVersion({ buildId: 'current-build' });
    app.browser.dispatchEvent(new Event('focus'));
    await setImmediate();
    app.setBlocked(false);
    context.mock.timers.tick(DEFERRED_RELOAD_INTERVAL_MS);
    assert.equal(app.reloads, 0);
    assert.equal(app.statuses.at(-1), 'current');
    stop();
});

test('stopping aborts the request, removes listeners and timers, and ignores late responses', async (context) => {
    const app = fixture(context);
    let resolve;
    let signal;
    app.browser.fetch = (_url, options) => {
        signal = options.signal;
        return new Promise((complete) => { resolve = complete; });
    };
    const stop = app.start();
    stop();
    assert.equal(signal.aborted, true);
    resolve({ ok: true, json: async () => ({ buildId: 'new-build' }) });
    await setImmediate();
    app.browser.dispatchEvent(new Event('focus'));
    app.page.dispatchEvent(new Event('visibilitychange'));
    context.mock.timers.tick(UPDATE_CHECK_INTERVAL_MS);
    assert.equal(app.reloads, 0);
    assert.deepEqual(app.statuses, []);
});

test('a timed out check releases the in-flight flag so later checks can recover', async (context) => {
    const app = fixture(context);
    context.mock.method(console, 'warn', () => {});
    app.browser.fetch = (_url, { signal }) => new Promise((_resolve, reject) => {
        signal.addEventListener('abort', () => reject(new Error('Aborted')));
    });
    const stop = app.start();
    context.mock.timers.tick(15_000);
    await setImmediate();
    assert.equal(app.statuses.at(-1), 'error');
    app.browser.fetch = async () => ({ ok: true, json: async () => ({ buildId: 'new-build' }) });
    app.browser.dispatchEvent(new Event('focus'));
    await setImmediate();
    assert.equal(app.reloads, 1);
    stop();
});

test('stale HTML after a reload cannot cause an automatic reload loop', async (context) => {
    const app = fixture(context);
    app.setVersion({ buildId: 'new-build' });
    let stop = app.start();
    await setImmediate();
    assert.equal(app.reloads, 1);
    stop();
    stop = app.start();
    await setImmediate();
    assert.equal(app.reloads, 1);
    assert.equal(app.statuses.at(-1), 'reload-blocked');
    stop();
});

test('guards cover multiple editors, unsaved drafts, in-flight saves, and cleanup', () => {
    let dirty = true;
    const removeEditor = registerAppUpdateGuard(() => dirty);
    const removeSave = registerAppUpdateGuard(() => true);
    assert.equal(isAppUpdateBlocked(), true);
    dirty = false;
    assert.equal(isAppUpdateBlocked(), true);
    removeSave();
    assert.equal(isAppUpdateBlocked(), false);
    dirty = true;
    assert.equal(isAppUpdateBlocked(), true);
    removeEditor();
    assert.equal(isAppUpdateBlocked(), false);
    removeEditor();
});

test('focused editing controls protect values that have not yet committed on blur', () => {
    assert.equal(isEditingControl(null), false);
    assert.equal(isEditingControl({ isContentEditable: true }), true);
    assert.equal(isEditingControl({ matches: () => false }), false);
    assert.equal(isEditingControl({ matches: (selector) => selector.includes('input:not([readonly])') }), true);
});
