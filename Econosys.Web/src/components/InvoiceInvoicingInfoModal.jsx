import { createPortal } from 'react-dom';

export default function InvoiceInvoicingInfoModal({ orders, onClose }) {
    return createPortal(
        <div
            className="fixed inset-0 z-[70] flex items-start justify-center bg-black/50 p-4 pt-20"
            onMouseDown={(event) => {
                if (event.target === event.currentTarget) onClose();
            }}
        >
            <section
                role="dialog"
                aria-modal="true"
                aria-labelledby="invoice-invoicing-info-title"
                className="relative flex max-h-[min(85vh,calc(100vh-6rem))] w-full max-w-3xl flex-col rounded-sm bg-[#f4f3f2] p-6 shadow-xl"
                onKeyDown={(event) => {
                    if (event.key === 'Escape') onClose();
                }}
            >
                <div className="relative mb-5 flex items-center justify-center">
                    <h2 id="invoice-invoicing-info-title" className="px-6 text-center text-sm font-semibold">
                        Faktureringsinfo per kundorder
                    </h2>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label="Stäng"
                        className="absolute right-0 mb-1 text-l leading-none text-gray-400 hover:text-gray-600"
                    >
                        ×
                    </button>
                </div>
                <div className="min-h-0 overflow-auto">
                    {orders.length === 0 ? (
                        <p className="py-4 text-center text-xs text-gray-500">Inga kopplade kundorder.</p>
                    ) : (
                        <table className="w-full table-fixed text-left text-xs">
                            <thead className="border-b border-gray-400 text-tiny uppercase text-gray-600">
                                <tr>
                                    <th className="w-1/3 px-3 py-2">Kundorder</th>
                                    <th className="px-3 py-2">Faktureringsinfo</th>
                                </tr>
                            </thead>
                            <tbody>
                                {orders.map((order) => (
                                    <tr key={order.id} className="border-b border-gray-200">
                                        <td className="break-words px-3 py-2">{order.customerOrderNr || order.id}</td>
                                        <td className="whitespace-pre-wrap break-words px-3 py-2">
                                            {order.invoicingInfo?.trim() || 'Ingen faktureringsinfo'}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>
                <div className="mt-6 flex justify-end">
                    <button
                        type="button"
                        onClick={onClose}
                        className="shadow-md/30 bg-orange-400 px-5 p-[5px] text-xs text-white hover:bg-orange-600"
                    >
                        Stäng
                    </button>
                </div>
            </section>
        </div>,
        document.body,
    );
}
