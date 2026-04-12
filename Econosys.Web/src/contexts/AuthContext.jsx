import { createContext, useContext, useState, useEffect } from 'react';
import apiClient from '../config/apiClient';

const AuthContext = createContext(null);

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

  // Check if user is authenticated on mount
  useEffect(() => {
    checkAuth();
  }, []);

  const checkAuth = async () => {
    try {
      console.log('🔍 Checking auth...');
      const response = await apiClient.get('/auth/me');
      console.log('✅ Auth check successful:', response.data);
      setUser(response.data);
      setIsAuthenticated(true);
    } catch (error) {
      // 401 on initial load is expected - user is not logged in yet
      console.log('⚠️ Auth check failed (expected on first load):', {
        message: error.message,
        status: error.response?.status,
        statusText: error.response?.statusText,
        data: error.response?.data,
      });
      setUser(null);
      setIsAuthenticated(false);
    } finally {
      setLoading(false);
    }
  };

  const login = async (email, password) => {
    try {
      console.log('🔐 Attempting login...');
      const response = await apiClient.post('/auth/login', {
        email,
        password,
      });
      
      console.log('✅ Login successful! (HttpOnly cookie set by server)');
      
      // Use user data from login response
      if (response.data.user) {
        setUser(response.data.user);
      } else {
        // If login doesn't return user data, set a basic user object
        setUser(response.data);
      }
      setIsAuthenticated(true);
      
      return { success: true, data: response.data };
    } catch (error) {
      console.error('❌ Login failed:', error.response?.data);
      return {
        success: false,
        error: error.response?.data?.message || 'Vänligen kontrollera dina inloggningsuppgifter och försök igen.',
      };
    }
  };

  const logout = async () => {
    try {
      await apiClient.post('/auth/logout');
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      setUser(null);
      setIsAuthenticated(false);
    }
  };

  const value = {
    user,
    loading,
    isAuthenticated,
    login,
    logout,
    checkAuth,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
