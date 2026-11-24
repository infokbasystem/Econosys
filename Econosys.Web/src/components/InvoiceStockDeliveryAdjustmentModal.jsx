import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import apiClient from '../config/apiClient';

export default function InvoiceStockDeliveryAdjustmentModal({ invoiceId, onClose, onAdjusted }) {
    const [deliveries, setDeliveries] = useState(null);
    const [error, setError] = useState('');
    const [retry, setRetry] = useState(0);
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        const controller = new AbortController();
        setDeliveries(null);
        setError('');
        apiClient.get(`/invoices/${invoiceId}/stock-delivery-adjustments`, { signal: controller.signal })
            .then(({ data }) => {
                if (!Array.isArray(data) || data.length === 0) throw new Error('Invalid delivery adjustment response.');
                setDeliveries(data);
            })
            .catch((requestError) => {
                if (controller.signal.aborted) return;
                console.error('Failed to preview stock delivery adjustments:', requestError);
                setError(requestError.response?.data?.detail || 'Kunde inte hämta förslag på leveransjusteringar.');
            });
        return () => controller.abort();
    }, [invoiceId, retry]);

    const adjust = async () => {
        setSaving(true);
        setError('');
        try {
            await apiClient.put(`/invoices/${invoiceId}/stock-delivery-adjustments`, { deliveries });
            onAdjusted();
            onClose();
        } catch (requestError) {
            console.error('Failed to adjust stock deliveries:', requestError);
            setDeliveries(null);
            setError(requestError.response?.data?.detail || 'Kunde inte bekräfta leveransjusteringen. Kontrollera leveranserna innan du försöker igen.');
        } finally {
            setSaving(false);
        }
    };

    return createPortal(
        <div className="fixed inset-0 z-[70] flex items-start justify-center bg-black/50 p-4 pt-20">
            <section role="dialog" aria-modal="true" aria-labelledby="stock-adjustment-title"
                className="relative flex max-h-[min(85vh,calc(100vh-6rem))] w-full max-w-xl flex-col rounded-sm bg-[#f4f3f2] p-6 shadow-xl"
                onKeyDown={(event) => {
                    if (event.key === 'Escape' && !saving) onClose();
                }}>
                <div className="relative mb-5 flex items-center justify-center">
                    <h2 id="stock-adjustment-title" className="px-6 text-center text-sm font-semibold">Justera lagerleveranser?</h2>
                    <button type="button" onClick={onClose} disabled={saving} aria-label="Stäng"
                        className="absolute right-0 mb-1 text-l leading-none text-gray-400 hover:text-gray-600 disabled:cursor-not-allowed disabled:opacity-50">
                        ×
                    </button>
                </div>
                <p className="mb-4 text-xs text-gray-700">
                    Kreditfakturan är sparad. Vill du minska antalet på de befintliga lagerleveranserna med det krediterade antalet?
                    Det påverkar lagersaldot.
                </p>
                {error && (
                    <div role="alert" className="mb-3 text-xs text-red-700">
                        {error}{' '}
                        <button type="button" onClick={() => setRetry((value) => value + 1)} className="underline">
                            Försök igen
                        </button>
                    </div>
                )}
                {!deliveries && !error && <p role="status" className="text-xs">Laddar leveranser…</p>}
                {deliveries && (
                    <div className="min-h-0 overflow-auto">
                        <table className="w-full text-left text-xs">
                            <thead className="border-b border-gray-400 text-tiny uppercase text-gray-600">
                                <tr>
                                    <th className="px-2 py-1">Leverans-id</th>
                                    <th className="px-2 py-1 text-right">Nuvarande antal</th>
                                    <th className="px-2 py-1 text-right">Krediterat</th>
                                    <th className="px-2 py-1 text-right">Nytt antal</th>
                                </tr>
                            </thead>
                            <tbody>
                                {deliveries.map((delivery) => (
                                    <tr key={delivery.deliveryId} className="border-b border-gray-200">
                                        <td className="px-2 py-1">{delivery.deliveryId}</td>
                                        <td className="px-2 py-1 text-right">{delivery.currentQuantity}</td>
                                        <td className="px-2 py-1 text-right">{delivery.creditedQuantity}</td>
                                        <td className="px-2 py-1 text-right">{delivery.newQuantity}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
                <div className="mt-6 flex flex-wrap justify-end gap-3">
                    <button type="button" autoFocus disabled={saving} onClick={onClose}
                        className="shadow-md/30 bg-orange-400 px-5 p-[5px] text-xs text-white hover:bg-orange-600 disabled:cursor-not-allowed disabled:opacity-50">
                        Justera inte
                    </button>
                    <button type="button" disabled={saving || !deliveries} onClick={adjust}
                        className="shadow-md/30 bg-lime-700 px-5 p-[5px] text-xs text-white hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-50">
                        {saving ? 'Justerar…' : 'Justera leveranser'}
                    </button>
                </div>
            </section>
        </div>,
        document.body,
    );
}
