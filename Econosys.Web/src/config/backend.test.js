import assert from 'node:assert/strict';
import { after, test } from 'node:test';

const originalLocalStorage = globalThis.localStorage;
const originalSessionStorage = globalThis.sessionStorage;
const values = new Map();
const tabValues = new Map();
globalThis.localStorage = {
  getItem: (key) => values.get(key) ?? null,
  setItem: (key, value) => values.set(key, value),
  removeItem: (key) => values.delete(key),
};
globalThis.sessionStorage = {
  getItem: (key) => tabValues.get(key) ?? null,
  setItem: (key, value) => tabValues.set(key, value),
  removeItem: (key) => tabValues.delete(key),
};
const backend = await import('./backend.js');
after(() => {
  globalThis.localStorage = originalLocalStorage;
  globalThis.sessionStorage = originalSessionStorage;
});

test('Login uses the existing default API and persists the explicit selection', () => {
  backend.setBackendMode('login');
  assert.equal(backend.getBackendMode(), 'login');
  assert.equal(backend.getApiBaseUrl(), 'https://localhost:5001/api');
  assert.equal(values.get('econosys.backendMode'), 'login');
  assert.equal(tabValues.size, 0);
});

test('Demo selects the remote /api base and survives module reload', async () => {
  backend.setBackendMode('demo');
  assert.equal(backend.getApiBaseUrl(), 'https://econosysapi.abloyd.se/api');
  const restored = await import('./backend.js?demo-reload');
  assert.equal(restored.getBackendMode(), 'demo');
  assert.equal(restored.getApiBaseUrl(), backend.DEMO_API_URL);
});

test('a new tab with empty session storage restores Demo before its first API request', async () => {
  backend.setBackendMode('demo');
  tabValues.clear();
  const newTab = await import('./backend.js?new-tab-without-opener');
  assert.equal(newTab.getBackendMode(), 'demo');
  assert.equal(newTab.getApiBaseUrl(), backend.DEMO_API_URL);
  assert.equal(tabValues.size, 0);
});

test('explicitly selecting Login replaces the remembered Demo mode and invalidates old requests', async () => {
  const revision = backend.getBackendRevision();
  backend.setBackendMode('login');
  assert.ok(backend.getBackendRevision() > revision);
  assert.equal(values.get('econosys.backendMode'), 'login');
  const restored = await import('./backend.js?logout-reload');
  assert.equal(restored.getBackendMode(), 'login');
});

test('existing tab-only Demo selections migrate to shared storage', async () => {
  values.clear();
  tabValues.set('econosys.backendMode', 'demo');
  const migrated = await import('./backend.js?legacy-demo');
  assert.equal(migrated.getBackendMode(), 'demo');
  assert.equal(values.get('econosys.backendMode'), 'demo');
  assert.equal(tabValues.size, 0);
});

test('an explicit shared Login choice takes precedence over stale tab-only Demo state', async () => {
  backend.setBackendMode('login');
  tabValues.set('econosys.backendMode', 'demo');
  const restored = await import('./backend.js?stale-tab');
  assert.equal(restored.getBackendMode(), 'login');
  assert.equal(tabValues.size, 0);
});

test('unknown selections are rejected without changing the backend', () => {
  const revision = backend.getBackendRevision();
  assert.throws(() => backend.setBackendMode('https://untrusted.invalid'), /Unknown backend mode/);
  assert.equal(backend.getBackendMode(), 'login');
  assert.equal(backend.getBackendRevision(), revision);
});
