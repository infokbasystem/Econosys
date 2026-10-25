import { useEffect, useState } from 'react';
import OrderFlowNavigation from '../../components/OrderFlowNavigation.jsx';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';

const CalculationNavigation = ({ calculation }) => {
    const [result, setResult] = useState(null);
    const [retry, setRetry] = useState(0);
    const id = calculation.id;

    useEffect(() => {
        if (!id) return;
        let disposed = false;

        const load = () => {
            getSharedRequest(`calculation:${id}:order-flow`, () => apiClient.get(`/ordernavigation/calculation/${id}/flow`))
                .then(({ data }) => {
                    if (!disposed) setResult({ calculation, items: data });
                })
                .catch((error) => {
                    console.error('Failed to load calculation order flow:', error);
                    if (!disposed) {
                        setResult((previous) => ({
                            calculation,
                            items: previous?.calculation === calculation ? previous.items : null,
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
    }, [calculation, id, retry]);

    const currentResult = result?.calculation === calculation ? result : null;
    const items = id ? currentResult?.items : [
        ...(calculation.product ? [{ type: 'product', ...calculation.product }] : []),
        { type: 'calculation', id: 0, supplierName: 'Ny kalkyl' },
    ];

    return (
        <div className="border-t border-gray-300 pt-4">
            {id > 0 && !currentResult && <p role="status" className="text-center text-xs text-gray-500">Laddar orderflöde...</p>}
            {currentResult?.error && (
                <div role="alert" className="mb-3 text-center text-tiny text-red-700">
                    <p>{currentResult.error}</p>
                    <button type="button" className="mt-1 underline" onClick={() => setRetry((value) => value + 1)}>Försök igen</button>
                </div>
            )}
            {items && <OrderFlowNavigation items={items} currentEntityType="calculation" currentEntityId={id || 0} />}
        </div>
    );
};

export default CalculationNavigation;
