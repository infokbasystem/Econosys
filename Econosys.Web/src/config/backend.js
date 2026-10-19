export const DEMO_API_URL = 'https://econosysapi.abloyd.se/api';
const NORMAL_API_URL = import.meta.env?.VITE_API_URL || 'https://localhost:5001/api';
const STORAGE_KEY = 'econosys.backendMode';
const storedMode = globalThis.localStorage?.getItem(STORAGE_KEY);
const legacyMode = globalThis.sessionStorage?.getItem(STORAGE_KEY);
let backendMode = (storedMode ?? legacyMode) === 'demo' ? 'demo' : 'login';
if (storedMode == null && legacyMode === 'demo') {
  globalThis.localStorage?.setItem(STORAGE_KEY, 'demo');
}
globalThis.sessionStorage?.removeItem(STORAGE_KEY);
let backendRevision = 0;

export const getBackendMode = () => backendMode;
export const getBackendRevision = () => backendRevision;
export const getApiBaseUrl = () => backendMode === 'demo' ? DEMO_API_URL : NORMAL_API_URL;

export const setBackendMode = (mode) => {
  if (mode !== 'login' && mode !== 'demo') {
    throw new Error(`Unknown backend mode: ${mode}`);
  }
  globalThis.localStorage?.setItem(STORAGE_KEY, mode);
  backendMode = mode;
  backendRevision += 1;
};
