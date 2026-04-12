import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:5001/api';

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
    // Let the AuthContext handle 401 errors instead of auto-redirecting
    return Promise.reject(error);
  }
);

export default apiClient;
