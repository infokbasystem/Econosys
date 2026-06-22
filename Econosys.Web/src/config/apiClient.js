import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:5001/api';
const SLOW_REQUEST_THRESHOLD_MS = 1000;
const REQUEST_HISTORY_LIMIT = 500;
let unauthorizedHandler = null;
const requestHistory = [];

const getNowMs = () => {
  if (typeof performance !== 'undefined' && typeof performance.now === 'function') {
    return performance.now();
  }

  return Date.now();
};

const createRequestId = () => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const toFullUrl = (config) => `${config?.baseURL || ''}${config?.url || ''}`;

const toUpperMethod = (method) => (method || 'GET').toUpperCase();

const toEndpoint = (url) => {
  if (!url) return '';

  try {
    return new URL(url).pathname;
  } catch {
    return String(url).split('?')[0];
  }
};

const toDurationMs = (startedAt) => {
  if (!startedAt) return null;
  return Number((getNowMs() - startedAt).toFixed(1));
};

const logSlowRequest = (durationMs, payload) => {
  if (durationMs == null || durationMs < SLOW_REQUEST_THRESHOLD_MS) {
    return;
  }

  console.warn('[API][SlowRequest]', {
    ...payload,
    thresholdMs: SLOW_REQUEST_THRESHOLD_MS,
    durationMs,
  });
};

const pushRequestHistory = (entry) => {
  requestHistory.push(entry);

  if (requestHistory.length > REQUEST_HISTORY_LIMIT) {
    requestHistory.splice(0, requestHistory.length - REQUEST_HISTORY_LIMIT);
  }
};

export const getApiRequestHistory = ({ sinceMs = 0, include } = {}) => requestHistory.filter((entry) => {
  const isAfterSince = sinceMs <= 0 || entry.timestampMs >= sinceMs;
  if (!isAfterSince) return false;

  if (typeof include === 'function') {
    return include(entry);
  }

  return true;
});

export const logApiRequestSummary = ({
  title = 'API request summary',
  sinceMs = 0,
  include,
  top = 5,
} = {}) => {
  const selected = getApiRequestHistory({ sinceMs, include });

  if (selected.length === 0) {
    console.log('[API][Summary]', {
      title,
      requestCount: 0,
    });

    return;
  }

  const withDuration = selected.filter((entry) => Number.isFinite(entry.durationMs));
  const totalDurationMs = Number(withDuration.reduce((sum, entry) => sum + entry.durationMs, 0).toFixed(1));
  const averageDurationMs = withDuration.length
    ? Number((totalDurationMs / withDuration.length).toFixed(1))
    : null;

  const slowestRequests = [...withDuration]
    .sort((a, b) => b.durationMs - a.durationMs)
    .slice(0, top)
    .map((entry) => ({
      requestId: entry.requestId,
      method: entry.method,
      endpoint: entry.endpoint,
      status: entry.status,
      durationMs: entry.durationMs,
    }));

  const endpointStatsMap = withDuration.reduce((acc, entry) => {
    const key = `${entry.method} ${entry.endpoint}`;
    const current = acc.get(key) || {
      method: entry.method,
      endpoint: entry.endpoint,
      count: 0,
      totalDurationMs: 0,
      averageDurationMs: 0,
    };

    current.count += 1;
    current.totalDurationMs += entry.durationMs;
    current.averageDurationMs = Number((current.totalDurationMs / current.count).toFixed(1));
    acc.set(key, current);
    return acc;
  }, new Map());

  const slowestEndpoints = [...endpointStatsMap.values()]
    .sort((a, b) => b.totalDurationMs - a.totalDurationMs)
    .slice(0, top)
    .map((entry) => ({
      ...entry,
      totalDurationMs: Number(entry.totalDurationMs.toFixed(1)),
    }));

  console.log('[API][Summary]', {
    title,
    requestCount: selected.length,
    totalDurationMs,
    averageDurationMs,
    slowRequestThresholdMs: SLOW_REQUEST_THRESHOLD_MS,
    slowestRequests,
    slowestEndpoints,
  });
};

export const setUnauthorizedHandler = (handler) => {
  unauthorizedHandler = handler;
};

const shouldHandleUnauthorized = (error) => {
  const status = error.response?.status;
  if (status !== 401) return false;

  const requestUrl = toFullUrl(error.config);

  // Do not treat failed login/logout as session-expired redirects.
  if (requestUrl.includes('/auth/login') || requestUrl.includes('/auth/logout')) {
    return false;
  }

  return true;
};

const apiClient = axios.create({
  baseURL: API_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true,
});

// Log requests
apiClient.interceptors.request.use(
  (config) => {
    const requestId = createRequestId();
    const startedAt = getNowMs();

    config.metadata = {
      requestId,
      startedAt,
    };

    console.log('[API][RequestStart]', {
      requestId,
      method: toUpperMethod(config.method),
      url: toFullUrl(config),
      data: config.data,
      headers: config.headers,
    });

    return config;
  },
  (error) => Promise.reject(error)
);

// Handle response errors globally
apiClient.interceptors.response.use(
  (response) => {
    const requestId = response.config?.metadata?.requestId || 'unknown';
    const durationMs = toDurationMs(response.config?.metadata?.startedAt);
    const payload = {
      requestId,
      method: toUpperMethod(response.config?.method),
      url: toFullUrl(response.config),
      status: response.status,
      durationMs,
      data: response.data,
    };

    pushRequestHistory({
      requestId,
      method: payload.method,
      url: payload.url,
      endpoint: toEndpoint(payload.url),
      status: payload.status,
      durationMs: payload.durationMs,
      timestampMs: Date.now(),
    });

    console.log('[API][RequestEnd]', payload);
    logSlowRequest(durationMs, payload);

    return response;
  },
  (error) => {
    const requestId = error.config?.metadata?.requestId || 'unknown';
    const durationMs = toDurationMs(error.config?.metadata?.startedAt);
    const payload = {
      requestId,
      method: toUpperMethod(error.config?.method),
      url: toFullUrl(error.config),
      message: error.message,
      status: error.response?.status,
      durationMs,
      data: error.response?.data,
      code: error.code,
    };

    pushRequestHistory({
      requestId,
      method: payload.method,
      url: payload.url,
      endpoint: toEndpoint(payload.url),
      status: payload.status,
      durationMs: payload.durationMs,
      code: payload.code,
      timestampMs: Date.now(),
    });

    if (error.code === 'ERR_CANCELED') {
      console.warn('[API][RequestCanceled]', payload);
    } else {
      console.error('[API][RequestError]', payload);
    }

    logSlowRequest(durationMs, payload);

    if (shouldHandleUnauthorized(error) && typeof unauthorizedHandler === 'function') {
      unauthorizedHandler(error);
    }

    return Promise.reject(error);
  }
);

export default apiClient;
