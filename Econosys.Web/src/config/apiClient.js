import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:5001/api';
let unauthorizedHandler = null;

export const setUnauthorizedHandler = (handler) => {
  unauthorizedHandler = handler;
};

const shouldHandleUnauthorized = (error) => {
  const status = error.response?.status;
  if (status !== 401) return false;

  const requestUrl = `${error.config?.baseURL || ''}${error.config?.url || ''}`;

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
    console.log('📤 Request:', {
      method: config.method.toUpperCase(),
      url: config.baseURL + config.url,
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
    console.log('📥 Response:', {
      status: response.status,
      data: response.data,
    });
    return response;
  },
  (error) => {
    console.error('❌ Error:', {
      message: error.message,
      status: error.response?.status,
      data: error.response?.data,
    });

    if (shouldHandleUnauthorized(error) && typeof unauthorizedHandler === 'function') {
      unauthorizedHandler(error);
    }

    return Promise.reject(error);
  }
);

export default apiClient;
