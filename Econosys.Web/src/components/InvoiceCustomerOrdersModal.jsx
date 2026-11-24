import { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import apiClient from '../config/apiClient';
import { getSwedishTodayDateString } from '../helpers/dateUtils';
import useAppUpdateGuard from '../hooks/useAppUpdateGuard';

export default function InvoiceCustomerOrdersModal({ invoiceId, onClose, onSaved }) {
    const [orders, setOrders] = useState(null);
    const [originalOrders, setOriginalOrders] = useState(null);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const changes = useMemo(() => (orders ?? []).filter((order) => {
        const original = originalOrders?.find((item) => item.id === order.id);
        return original && (original.completed !== order.completed || original.completedDate !== order.completedDate);
    }), [orders, originalOrders]);
    useAppUpdateGuard(changes.length > 0);

    useEffect(() => {
        const controller = new AbortController();
        apiClient.get(`/invoices/${invoiceId}/customer-orders`, { signal: controller.signal })
            .then(({ data }) => {
                if (!Array.isArray(data)) throw new Error('Invalid customer order response.');
                setOrders(data);
                setOriginalOrders(data);
            })
            .catch((requestError) => {
                if (controller.signal.aborted) return;
                console.error('Failed to load invoice customer orders:', requestError);
                setError('Kunde inte ladda ordererkännandena.');
            });
        return () => controller.abort();
    }, [invoiceId]);

    const updateOrder = (id, values) => setOrders((previous) => previous.map((order) => (
        order.id === id ? { ...order, ...values } : order
    )));

    const save = async () => {
        setSaving(true);
        setError('');
        try {
            await apiClient.put(`/invoices/${invoiceId}/customer-orders`, {
                orders: changes.map(({ id, completed, completedDate }) => ({ id, completed, completedDate: completedDate || null })),
            });
            onSaved();
            onClose();
        } catch (requestError) {
            console.error('Failed to update customer order completion:', requestError);
            setError(requestError.response?.data?.detail || 'Kunde inte uppdatera ordererkännandena.');
        } finally {
            setSaving(false);
        }
    };

    return createPortal(
        <div className="fixed inset-0 z-[70] flex items-start justify-center bg-black/50 p-4 pt-20">
            <section role="dialog" aria-modal="true" aria-labelledby="invoice-orders-title"
                className="relative flex max-h-[min(85vh,calc(100vh-6rem))] w-full max-w-4xl flex-col rounded-sm bg-[#f4f3f2] p-6 shadow-xl"
                onKeyDown={(event) => {
                    if (event.key === 'Escape' && !saving) onClose();
                }}
            >
                <div className="relative mb-5 flex items-center justify-center">
                    <h2 id="invoice-orders-title" className="px-6 text-center text-sm font-semibold">Vilka ordererkännanden är slutlevererade?</h2>
                    <button type="button" onClick={onClose} disabled={saving} aria-label="Stäng"
                        className="absolute right-0 mb-1 text-l leading-none text-gray-400 hover:text-gray-600 disabled:cursor-not-allowed disabled:opacity-50">
                        ×
                    </button>
                </div>
                {error && <p role="alert" className="mb-3 text-xs text-red-700">{error}</p>}
                {!orders && !error && <p role="status" className="text-xs">Laddar ordererkännanden…</p>}
                {orders && (
                    <div className="min-h-0 overflow-auto">
                        <table className="w-full min-w-[700px] text-left text-xs">
                            <thead className="border-b border-gray-400 text-tiny uppercase text-gray-600">
                                <tr>
                                    <th className="px-2 py-1">Sätt klar</th>
                                    <th className="px-2 py-1">Klardatum</th>
                                    <th className="px-2 py-1">Ordererk.</th>
                                    <th className="px-2 py-1">Lager</th>
                                    <th className="px-2 py-1 text-right">Upplaga/inlev.</th>
                                    <th className="px-2 py-1 text-right">Levererat</th>
                                    <th className="px-2 py-1 text-right">Fakturerat</th>
                                    <th className="px-2 py-1 text-right">Kvar</th>
                                </tr>
                            </thead>
                            <tbody>
                                {orders.length === 0 && <tr><td colSpan={8} className="py-4 text-center text-gray-500">Inga kopplade ordererkännanden.</td></tr>}
                                {orders.map((order) => (
                                    <tr key={order.id} className="border-b border-gray-200">
                                        <td className="px-2 py-1">
                                            <input type="checkbox" checked={order.completed} disabled={saving} className="accent-lime-700"
                                                aria-label={`Sätt ${order.customerOrderNr || order.id} klar`}
                                                onChange={(event) => updateOrder(order.id, {
                                                    completed: event.target.checked,
                                                    completedDate: event.target.checked && !order.completedDate ? getSwedishTodayDateString() : order.completedDate,
                                                })}
                                            />
                                        </td>
                                        <td className="px-2 py-1">
                                            <input type="date" value={order.completedDate || ''} disabled={saving}
                                                aria-label={`Klardatum för ${order.customerOrderNr || order.id}`}
                                                onChange={(event) => updateOrder(order.id, { completedDate: event.target.value || null })}
                                                className="rounded border border-gray-300 bg-white px-1 py-1"
                                            />
                                        </td>
                                        <td className="px-2 py-1">{order.customerOrderNr}</td>
                                        <td className="px-2 py-1">{order.inventoryName}</td>
                                        <td className="px-2 py-1 text-right">{order.edition}</td>
                                        <td className="px-2 py-1 text-right">{order.delivered}</td>
                                        <td className="px-2 py-1 text-right">{order.invoiced}</td>
                                        <td className="px-2 py-1 text-right">{order.remaining}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
                <div className="mt-6 flex flex-wrap justify-end gap-3">
                    <button type="button" disabled={saving} onClick={onClose}
                        className="shadow-md/30 bg-orange-400 px-5 p-[5px] text-xs text-white hover:bg-orange-600 disabled:cursor-not-allowed disabled:opacity-50">
                        Avbryt
                    </button>
                    <button type="button" disabled={saving || !orders || changes.length === 0} onClick={save}
                        className="shadow-md/30 bg-lime-700 px-5 p-[5px] text-xs text-white hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-50">
                        {saving ? 'Sparar…' : 'Markera som klara / ej klara'}
                    </button>
                </div>
            </section>
        </div>,
        document.body,
    );
}
