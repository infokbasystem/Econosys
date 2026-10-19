import { getBackendRevision } from '../config/backend.js';

const inflightRequestMap = new Map();

export const getSharedRequest = (key, requestFactory) => {
    const sessionKey = `${getBackendRevision()}:${key}`;
    if (inflightRequestMap.has(sessionKey)) {
        return inflightRequestMap.get(sessionKey);
    }

    const requestPromise = Promise.resolve()
        .then(requestFactory)
        .finally(() => {
            if (inflightRequestMap.get(sessionKey) === requestPromise) {
                inflightRequestMap.delete(sessionKey);
            }
        });

    inflightRequestMap.set(sessionKey, requestPromise);
    return requestPromise;
};
