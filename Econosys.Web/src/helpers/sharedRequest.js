const inflightRequestMap = new Map();

export const getSharedRequest = (key, requestFactory) => {
    if (inflightRequestMap.has(key)) {
        return inflightRequestMap.get(key);
    }

    const requestPromise = Promise.resolve()
        .then(requestFactory)
        .finally(() => {
            if (inflightRequestMap.get(key) === requestPromise) {
                inflightRequestMap.delete(key);
            }
        });

    inflightRequestMap.set(key, requestPromise);
    return requestPromise;
};
