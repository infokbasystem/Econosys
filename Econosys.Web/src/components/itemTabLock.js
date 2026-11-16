const ITEM_DETAIL_ROUTES = [
    { segments: ['order', 'calculations'], listPath: '/order/calculations' },
    { segments: ['order', 'products'], listPath: '/order/products' },
    { segments: ['order', 'customers'], listPath: '/order/customers' },
    { segments: ['order', 'suppliers'], listPath: '/order/suppliers' },
    { segments: ['order', 'inquiries'], listPath: '/order/inquiries' },
    { segments: ['order', 'quotations'], listPath: '/order/quotations' },
    { segments: ['order', 'supplierorders'], listPath: '/order/supplierorders' },
    { segments: ['order', 'customerorders'], listPath: '/order/customerorders' },
    { segments: ['logistics', 'transportorder'], listPath: '/logistics/transportorderoverivew' },
    { segments: ['logistics', 'calloff'], listPath: '/logistics/calloffoverview' },
    { segments: ['management', 'deviations'], listPath: '/management/deviations' },
    { segments: ['finance', 'invoice'], listPath: '/finance/searchinvoice' },
    { segments: ['budget'], listPath: '/budget' },
];

const tabLockRequests = new Map();

export const getItemTabLock = (pathname) => {
    const segments = pathname.split('/').filter(Boolean);
    const route = ITEM_DETAIL_ROUTES.find(({ segments: routeSegments }) =>
        routeSegments.length === segments.length - 1
        && routeSegments.every((segment, index) => segment === segments[index]));
    const id = segments.at(-1);

    if (!route || !/^\d+$/.test(id ?? '')) return null;

    const resource = route.segments.join(':');
    return {
        lockName: `econosys:item:${resource}:${id}`,
        listPath: route.listPath,
    };
};

export const startItemTabLock = (lockName, lockManager, onStatus, onError) => {
    let active = true;
    let releaseLock;
    const holdLock = new Promise((resolve) => {
        releaseLock = resolve;
    });

    if (!lockManager?.request) {
        onStatus('unsupported');
        return () => {
            active = false;
            releaseLock();
        };
    }

    const requestLock = () => {
        if (!active) return undefined;
        return lockManager.request(
            lockName,
            { mode: 'exclusive', ifAvailable: true },
            (lock) => {
                if (!active) return undefined;
                onStatus(lock ? 'acquired' : 'duplicate');
                return lock ? holdLock : undefined;
            },
        );
    };

    // Finish releasing this document's previous request before reacquiring the same item.
    const previousRequest = tabLockRequests.get(lockName) ?? Promise.resolve();
    const request = previousRequest.then(requestLock, requestLock);
    tabLockRequests.set(lockName, request);
    const finishRequest = () => {
        if (tabLockRequests.get(lockName) === request) tabLockRequests.delete(lockName);
    };
    request.then(finishRequest, (error) => {
        finishRequest();
        if (active) onError(error);
    });

    return () => {
        active = false;
        releaseLock();
    };
};
