import { createContext, useContext, useState, useEffect, useRef, useCallback } from 'react';
import apiClient, { selectBackend, setUnauthorizedHandler } from '../config/apiClient';
import { getBackendMode, getBackendRevision } from '../config/backend';
import { getSharedRequest } from '../helpers/sharedRequest';

const AuthContext = createContext(null);
const KEEP_ALIVE_INTERVAL_MS = 60 * 1000;
const ACTIVE_WINDOW_MS = 5 * 60 * 1000;

const getLoginErrorMessage = (error) => {
  // Timeout, offline, DNS-fel eller blockerad CORS-förfrågan ger inget svar från servern.
  if (!error.response) {
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
      return 'Servern (API) svarade inte i tid. Försök igen eller kontakta administratören.';
    }

    return 'Kan inte nå servern (API). Kontrollera din internetanslutning eller kontakta administratören.';
  }

  const { status, data } = error.response;
  const serverMessage = typeof data?.message === 'string' ? data.message : '';

  if (status === 401) {
    return serverMessage || 'Vänligen kontrollera dina inloggningsuppgifter och försök igen.';
  }

  if (status === 423) {
    return serverMessage || 'Ditt konto är låst. Kontakta administratören för att låsa upp kontot.';
  }

  if (status === 429) {
    return 'För många inloggningsförsök. Vänta en stund och försök igen.';
  }

  if (status === 503) {
    return serverMessage || 'Servern kan inte ansluta till databasen. Kontakta administratören.';
  }

  if (status >= 500) {
    return `Ett serverfel uppstod vid inloggning (status ${status}). Kontakta administratören.`;
  }

  return serverMessage || `Inloggningen misslyckades (status ${status}). Kontakta administratören.`;
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [backendMode, setMode] = useState(getBackendMode);
  const isAuthenticatedRef = useRef(false);
  const keepAliveInFlightRef = useRef(false);
  const lastActivityRef = useRef(Date.now());

  const updateAuthState = useCallback((nextUser) => {
    setUser(nextUser);
    const nextIsAuthenticated = Boolean(nextUser);
    setIsAuthenticated(nextIsAuthenticated);
    isAuthenticatedRef.current = nextIsAuthenticated;
  }, []);

  const changeBackend = useCallback((mode) => {
    if (isAuthenticatedRef.current) {
      throw new Error('Log out before changing backend.');
    }
    selectBackend(mode);
    setMode(mode);
  }, []);

  const forceSessionExpired = useCallback(() => {
    if (!isAuthenticatedRef.current) return;

    updateAuthState(null);
    selectBackend(getBackendMode());
    // AuthProvider is outside RouterProvider, so redirect via location (full reload),
    // stash the current path so LoginPage can return the user here after re-login.
    const currentPath = `${window.location.pathname}${window.location.search}`;
    if (currentPath && currentPath !== '/login') {
      sessionStorage.setItem('postLoginRedirect', currentPath);
    }
    window.location.assign('/login');
  }, [updateAuthState]);

  const checkAuth = useCallback(async () => {
    const revision = getBackendRevision();
    const baseURL = apiClient.defaults.baseURL;
    try {
      const response = await getSharedRequest(`auth:me:bootstrap:${baseURL}:${revision}`,
        () => apiClient.get('/auth/me', { baseURL }));
      if (revision === getBackendRevision()) updateAuthState(response.data);
    } catch (error) {
      if (revision !== getBackendRevision()) return;
      if (error.response?.status === 401) {
        updateAuthState(null);
      } else {
        console.error('Auth check failed:', error);
        updateAuthState(null);
      }
    } finally {
      if (revision === getBackendRevision()) setLoading(false);
    }
  }, [updateAuthState]);

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  useEffect(() => {
    setUnauthorizedHandler(forceSessionExpired);

    return () => {
      setUnauthorizedHandler(null);
    };
  }, [forceSessionExpired]);

  useEffect(() => {
    const markActivity = () => {
      lastActivityRef.current = Date.now();
    };

    const activityEvents = ['mousedown', 'keydown', 'touchstart', 'scroll'];

    activityEvents.forEach((eventName) => {
      window.addEventListener(eventName, markActivity, { passive: true });
    });

    return () => {
      activityEvents.forEach((eventName) => {
        window.removeEventListener(eventName, markActivity);
      });
    };
  }, []);

  useEffect(() => {
    if (!isAuthenticated) return undefined;

    const tickKeepAlive = async () => {
      if (keepAliveInFlightRef.current) return;
      if (document.visibilityState === 'hidden') return;

      const isUserActive = Date.now() - lastActivityRef.current <= ACTIVE_WINDOW_MS;
      if (!isUserActive) return;

      keepAliveInFlightRef.current = true;

      const revision = getBackendRevision();
      try {
        await apiClient.get('/auth/me', {
          headers: {
            'X-Session-KeepAlive': 'true',
          },
        });
      } catch (error) {
        if (revision === getBackendRevision() && error.response?.status === 401) {
          forceSessionExpired();
        }
      } finally {
        keepAliveInFlightRef.current = false;
      }
    };

    const intervalId = window.setInterval(tickKeepAlive, KEEP_ALIVE_INTERVAL_MS);
    return () => window.clearInterval(intervalId);
  }, [isAuthenticated, forceSessionExpired]);

  const login = async (email, password) => {
    const revision = getBackendRevision();
    try {
      console.log('🔐 Attempting login...');
      const response = await apiClient.post('/auth/login', {
        email,
        password,
      });
      if (revision !== getBackendRevision()) {
        return { success: false, error: 'Servervalet ändrades. Försök logga in igen.' };
      }
      
      console.log('✅ Login successful! (HttpOnly cookie set by server)');
      
      // Use user data from login response
      if (response.data.user) {
        updateAuthState(response.data.user);
      } else {
        // If login doesn't return user data, set a basic user object
        updateAuthState(response.data);
      }
      
      return { success: true, data: response.data };
    } catch (error) {
      console.error('❌ Login failed:', error.response?.data || error.message);

      return {
        success: false,
        error: getLoginErrorMessage(error),
      };
    }
  };

  const logout = async () => {
    try {
      await apiClient.post('/auth/logout');
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      updateAuthState(null);
      selectBackend(getBackendMode());
    }
  };

  const value = {
    user,
    loading,
    isAuthenticated,
    backendMode,
    changeBackend,
    login,
    logout,
    checkAuth,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
