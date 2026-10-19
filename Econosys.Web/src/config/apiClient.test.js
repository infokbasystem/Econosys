import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import axios from 'axios';
import apiClient, { selectBackend, setUnauthorizedHandler } from './apiClient.js';
import { getBackendMode } from './backend.js';
import { getSharedRequest } from '../helpers/sharedRequest.js';

const response = (config, data = {}) => ({ config, data, status: 200, statusText: 'OK', headers: {} });
const unauthorized = (config) => new axios.AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
  ...response(config), status: 401,
});
const deferred = () => {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
};
afterEach(() => {
  setUnauthorizedHandler(null);
  selectBackend('login');
});

test('demo login and subsequent requests use the demo API with cookies', async () => {
  selectBackend('demo');
  for (const url of ['/auth/login', '/auth/me', '/supplierorders/1', '/auth/logout']) {
    await apiClient.request({
      url, method: 'post',
      adapter: async (config) => {
        assert.equal(config.baseURL, 'https://econosysapi.abloyd.se/api');
        assert.equal(config.withCredentials, true);
        return response(config);
      },
    });
  }
});

test('Demo blocks normal-backend overrides and absolute URLs before sending a request', async () => {
  const normalBaseURL = apiClient.defaults.baseURL;
  selectBackend('demo');
  for (const config of [
    { url: '/auth/me', baseURL: normalBaseURL },
    { url: `${normalBaseURL}/auth/me` },
    { url: '//localhost:5001/api/auth/me' },
    { url: 'https://econosysapi.abloyd.se/api/../../auth/me' },
    { url: 'https://econosysapi.abloyd.se/api-other/auth/me' },
    { url: 'https://econosysapi.abloyd.se.untrusted.invalid/api/auth/me' },
  ]) {
    await assert.rejects(apiClient.request({
      ...config,
      adapter: async () => assert.fail('Non-demo requests must never reach the adapter'),
    }), { code: 'ERR_BAD_OPTION_VALUE', message: 'Demo mode only allows requests to the demo API.' });
  }
  assert.equal(getBackendMode(), 'demo');
});

test('Demo permits absolute demo API URLs and local blob downloads', async () => {
  selectBackend('demo');
  for (const config of [
    { url: 'https://econosysapi.abloyd.se/api/attachment/test.pdf' },
    { url: 'blob:https://localhost:5173/test-document', baseURL: '' },
  ]) {
    let sent = false;
    await apiClient.request({
      ...config,
      adapter: async (requestConfig) => {
        sent = true;
        return response(requestConfig);
      },
    });
    assert.equal(sent, true);
  }
});

test('failed demo login does not expire the session, reset mode or retry against Login', async () => {
  selectBackend('demo');
  let calls = 0;
  setUnauthorizedHandler(() => assert.fail('Login 401 is not session expiry'));
  await assert.rejects(apiClient.post('/auth/login', {}, {
    adapter: async (config) => { calls += 1; throw unauthorized(config); },
  }));
  assert.equal(calls, 1);
  assert.equal(getBackendMode(), 'demo');
});

test('network errors keep Demo selected and are surfaced to the caller', async () => {
  selectBackend('demo');
  await assert.rejects(apiClient.get('/auth/me', {
    adapter: async (config) => { throw new axios.AxiosError('Network Error', 'ERR_NETWORK', config); },
  }), /Network Error/);
  assert.equal(getBackendMode(), 'demo');
});

test('an active session 401 invokes expiry handling on its selected backend', async () => {
  selectBackend('demo');
  let expired = false;
  setUnauthorizedHandler(() => { expired = true; });
  await assert.rejects(apiClient.get('/customerorders', { adapter: async (config) => { throw unauthorized(config); } }));
  assert.equal(expired, true);
  assert.equal(getBackendMode(), 'demo');
});

test('responses from a previous backend session cannot populate the new session', async () => {
  selectBackend('demo');
  const started = deferred();
  const completed = deferred();
  const request = apiClient.get('/auth/me', {
    adapter: async (config) => {
      started.resolve();
      await completed.promise;
      return response(config, { email: 'old-session@example.invalid' });
    },
  });
  await started.promise;
  selectBackend('login');
  completed.resolve();
  await assert.rejects(request, (error) => axios.isCancel(error));
});

test('ending a demo session invalidates in-flight requests without switching to Login', async () => {
  selectBackend('demo');
  const started = deferred();
  const completed = deferred();
  const request = apiClient.get('/auth/me', {
    adapter: async (config) => {
      started.resolve();
      await completed.promise;
      return response(config);
    },
  });
  await started.promise;
  selectBackend(getBackendMode());
  completed.resolve();
  await assert.rejects(request, (error) => axios.isCancel(error));
  assert.equal(getBackendMode(), 'demo');
  assert.equal(apiClient.defaults.baseURL, 'https://econosysapi.abloyd.se/api');
});

test('late 401 responses from a previous backend do not expire the new session', async () => {
  selectBackend('demo');
  const started = deferred();
  const completed = deferred();
  let expired = false;
  setUnauthorizedHandler(() => { expired = true; });
  const request = apiClient.get('/auth/me', {
    adapter: async (config) => {
      started.resolve();
      await completed.promise;
      throw unauthorized(config);
    },
  });
  await started.promise;
  selectBackend('login');
  completed.resolve();
  await assert.rejects(request);
  assert.equal(expired, false);
});

test('delayed cleanup targeting the previous backend cannot expire the current session', async () => {
  selectBackend('demo');
  const oldBaseURL = apiClient.defaults.baseURL;
  selectBackend('login');
  let expired = false;
  setUnauthorizedHandler(() => { expired = true; });
  await assert.rejects(apiClient.post('/budget/1/ownership/release', null, {
    baseURL: oldBaseURL,
    adapter: async (config) => {
      assert.equal(config.baseURL, oldBaseURL);
      throw unauthorized(config);
    },
  }));
  assert.equal(expired, false);
});

test('shared requests are deduplicated only within the same backend session', async () => {
  selectBackend('demo');
  const completed = deferred();
  const first = getSharedRequest('same-record', () => completed.promise);
  assert.equal(getSharedRequest('same-record', () => assert.fail('Should deduplicate')), first);
  selectBackend('login');
  const second = getSharedRequest('same-record', async () => 'normal-record');
  assert.notEqual(second, first);
  completed.resolve('demo-record');
  assert.deepEqual(await Promise.all([first, second]), ['demo-record', 'normal-record']);
});
