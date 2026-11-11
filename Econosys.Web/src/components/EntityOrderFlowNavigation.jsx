import { useEffect, useState } from 'react';
import OrderFlowNavigation from './OrderFlowNavigation.jsx';
import apiClient from '../config/apiClient';
import { getSharedRequest } from '../helpers/sharedRequest';

const EntityOrderFlowNavigation = ({ entityType, entityId, refreshKey, draftItems }) => {
    const [result, setResult] = useState(null);
    const [retry, setRetry] = useState(0);
    const [archivedVisibleFor, setArchivedVisibleFor] = useState(null);
    const requestKey = `${entityType}:${entityId}:order-flow`;
    const hasSavedEntity = Number.isInteger(entityId) && entityId > 0;

    useEffect(() => {
        if (!hasSavedEntity) return;
        let disposed = false;

        const load = () => {
            getSharedRequest(requestKey, () => apiClient.get(`/ordernavigation/${entityType.toLowerCase()}/${entityId}/flow`))
                .then(({ data }) => {
                    if (!disposed) setResult({ requestKey, refreshKey, items: data });
                })
                .catch((error) => {
                    console.error('Failed to load order flow:', error);
                    if (!disposed) {
                        setResult((previous) => ({
                            requestKey,
                            refreshKey,
                            items: previous?.requestKey === requestKey && previous.refreshKey === refreshKey ? previous.items : null,
                            error: 'Kunde inte ladda orderflödet.',
                        }));
                    }
                });
        };
        const refresh = () => {
            if (document.visibilityState === 'visible') load();
        };

        load();
        window.addEventListener('focus', refresh);
        document.addEventListener('visibilitychange', refresh);
        return () => {
            disposed = true;
            window.removeEventListener('focus', refresh);
            document.removeEventListener('visibilitychange', refresh);
        };
    }, [entityId, entityType, hasSavedEntity, refreshKey, requestKey, retry]);

    const currentResult = result?.requestKey === requestKey && result.refreshKey === refreshKey ? result : null;
    const items = hasSavedEntity ? currentResult?.items : draftItems;
    const filterArchived = entityType !== 'calculation';
    const showArchived = archivedVisibleFor === requestKey;
    const hasArchived = filterArchived && items?.some((item) => item.type === 'calculation' && item.isArchived);
    const visibleItems = filterArchived && !showArchived ? items?.filter((item) => !item.isArchived) : items;

    if (!hasSavedEntity && !draftItems) return null;

    return (
        <div className="border-t border-gray-300 pt-4">
            {hasSavedEntity && !currentResult && <p role="status" className="text-center text-xs text-gray-500">Laddar orderflöde...</p>}
            {currentResult?.error && (
                <div role="alert" className="mb-3 text-center text-tiny text-red-700">
                    <p>{currentResult.error}</p>
                    <button type="button" className="mt-1 underline" onClick={() => setRetry((value) => value + 1)}>Försök igen</button>
                </div>
            )}
            {visibleItems && <OrderFlowNavigation items={visibleItems} currentEntityType={entityType} currentEntityId={entityId || 0} />}
            {hasArchived && (
                <button
                    type="button"
                    className="mt-3 px-1 text-xs text-red-600 hover:underline"
                    aria-expanded={showArchived}
                    onClick={() => setArchivedVisibleFor(showArchived ? null : requestKey)}
                >
                    {showArchived ? 'Dölj arkiverade kalkyler' : 'Visa arkiverade kalkyler'}
                </button>
            )}
        </div>
    );
};

export default EntityOrderFlowNavigation;
