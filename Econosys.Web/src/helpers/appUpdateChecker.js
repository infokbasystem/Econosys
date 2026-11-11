export const UPDATE_CHECK_INTERVAL_MS = 60_000;
export const DEFERRED_RELOAD_INTERVAL_MS = 1_000;
const RELOAD_STORAGE_KEY = 'econosys:app-update-reload';

export function startAppUpdateChecker({
    buildId,
    versionUrl,
    isBlocked,
    onStatus,
    browser = window,
    page = document,
}) {
    let stopped = false;
    let checking = false;
    let pendingBuildId = null;
    let reloading = false;
    let controller;

    const tryReload = () => {
        if (stopped || reloading || !pendingBuildId || page.visibilityState !== 'visible') return;
        if (isBlocked()) {
            onStatus('pending');
            return;
        }

        const reloadKey = `${buildId}:${pendingBuildId}`;
        try {
            if (browser.sessionStorage.getItem(RELOAD_STORAGE_KEY) === reloadKey) {
                onStatus('reload-blocked');
                return;
            }
            browser.sessionStorage.setItem(RELOAD_STORAGE_KEY, reloadKey);
        } catch (error) {
            console.warn('[AppUpdate] Could not persist reload protection:', error);
            onStatus('reload-blocked');
            return;
        }
        reloading = true;
        browser.location.reload();
    };

    const check = async () => {
        if (stopped || checking || reloading || page.visibilityState !== 'visible') return;
        checking = true;
        controller = new AbortController();
        const timeout = browser.setTimeout(() => controller.abort(), 15_000);
        try {
            const url = new URL(versionUrl, browser.location.href);
            url.searchParams.set('t', String(Date.now()));
            const response = await browser.fetch(url, {
                cache: 'no-store',
                credentials: 'same-origin',
                signal: controller.signal,
            });
            if (!response.ok) throw new Error(`Version check returned HTTP ${response.status}.`);
            const version = await response.json();
            if (typeof version?.buildId !== 'string' || !version.buildId.trim()) {
                throw new Error('Version check returned an invalid build ID.');
            }
            if (stopped) return;
            pendingBuildId = version.buildId !== buildId ? version.buildId : null;
            onStatus(pendingBuildId ? 'pending' : 'current');
            tryReload();
        } catch (error) {
            if (!stopped) {
                console.warn('[AppUpdate] Version check failed; will retry:', error);
                // A failed check must not reload based on stale deployment information.
                pendingBuildId = null;
                onStatus('error');
            }
        } finally {
            browser.clearTimeout(timeout);
            checking = false;
        }
    };

    const onResume = () => { void check(); };
    page.addEventListener('visibilitychange', onResume);
    browser.addEventListener('focus', onResume);
    const checkInterval = browser.setInterval(onResume, UPDATE_CHECK_INTERVAL_MS);
    const reloadInterval = browser.setInterval(() => {
        if (!checking) tryReload();
    }, DEFERRED_RELOAD_INTERVAL_MS);
    void check();

    return () => {
        stopped = true;
        controller?.abort();
        browser.clearInterval(checkInterval);
        browser.clearInterval(reloadInterval);
        page.removeEventListener('visibilitychange', onResume);
        browser.removeEventListener('focus', onResume);
    };
}
