import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { RefreshCw, X } from 'lucide-react';
import apiClient from '../config/apiClient';
import { formatDateShort } from '../helpers/dateUtils';
import { formatNumber } from '../helpers/numberUtils';
import { formatHistoryQuantity, getHistoryDetailUrl, getHistoryTransportLink } from '../helpers/orderHistory';
import { getSharedRequest } from '../helpers/sharedRequest';

const linkClass = 'text-sky-800 hover:underline focus-visible:underline focus-visible:outline-sky-600';
const showNumber = (value) => value == null ? '—' : formatNumber(value);
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const HistoryDetails = ({ row, onClose }) => {
    const [details, setDetails] = useState(null);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let active = true;
        const controller = new AbortController();
        apiClient.get(getHistoryDetailUrl(row), { signal: controller.signal })
            .then((response) => {
                if (active) setDetails(response.data);
            })
            .catch((requestError) => {
                if (!active) return;
                console.error('Failed to load order history details:', requestError);
                setError(requestError?.response?.data?.message || 'Kunde inte hämta historikposten. Stäng och försök igen.');
            })
            .finally(() => {
                if (active) setLoading(false);
            });
        return () => {
            active = false;
            controller.abort();
        };
    }, [row]);

    useEffect(() => {
        const onKeyDown = (event) => {
            if (event.key === 'Escape') onClose();
        };
        document.addEventListener('keydown', onKeyDown);
        return () => {
            document.removeEventListener('keydown', onKeyDown);
        };
    }, [onClose]);

    const stocktake = row.type === 'StockTaking';
    const fields = [
        ['Datum', formatDateShort(row.date)],
        ['Typ', row.typeName],
        ['Status', row.status],
        ['Till', row.to],
        ['Antal', showNumber(details?.nrOfItems)],
        ['Antal pallar', showNumber(details?.nrOfPallets)],
        ...(stocktake ? [
            ['Differens antal', showNumber(details?.diffNrOfItems)],
            ['Differens pallar', showNumber(details?.diffNrOfPallets)],
        ] : [
            ['Lager', details?.inventoryName],
            ['Leveransnr', details?.deliveryNr ?? details?.callOff],
            ['Antal/pall', showNumber(details?.editionPerPallet)],
            ['Pallformat (L × B × H)', [details?.palletLength, details?.palletWidth, details?.palletHeight].map(showNumber).join(' × ')],
            ['Staplingsbar', details?.palletIsStackable ? 'Ja' : 'Nej'],
        ]),
    ];

    return createPortal(
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
            <section
                role="dialog"
                aria-modal="true"
                aria-labelledby="order-history-details-title"
                className="w-full max-w-lg rounded-sm bg-white p-6 shadow-xl"
                onClick={(event) => event.stopPropagation()}
                onKeyDown={(event) => {
                    if (event.key !== 'Tab') return;
                    const buttons = event.currentTarget.querySelectorAll('button, a[href]');
                    const first = buttons[0];
                    const last = buttons[buttons.length - 1];
                    if (event.shiftKey && document.activeElement === first) {
                        event.preventDefault();
                        last?.focus();
                    } else if (!event.shiftKey && document.activeElement === last) {
                        event.preventDefault();
                        first?.focus();
                    }
                }}
            >
                <div className="mb-4 flex items-center justify-between gap-4">
                    <h3 id="order-history-details-title" className="text-sm font-semibold">{row.typeName} #{row.id}</h3>
                    <button type="button" autoFocus onClick={onClose} aria-label="Stäng historikpost" className="rounded p-1 hover:bg-gray-100">
                        <X size={18} />
                    </button>
                </div>
                {loading ? <p role="status" className="text-xs text-gray-500">Hämtar historikpost…</p>
                    : error ? <p role="alert" className="text-xs text-red-700">{error}</p>
                        : <dl className="grid grid-cols-[1fr_2fr] gap-x-4 gap-y-2 text-xs">
                            {fields.map(([label, value]) => <div key={label} className="contents">
                                <dt className="text-gray-500">{label}</dt>
                                <dd className="break-words">{value || '—'}</dd>
                            </div>)}
                        </dl>}
                {stocktake && row.stockTakingId && (
                    <Link to={`/logistics/stocktakings?stockTakingId=${row.stockTakingId}`} className={`${linkClass} mt-5 inline-block text-xs`}>
                        Visa hela inventeringen
                    </Link>
                )}
            </section>
        </div>,
        document.body,
    );
};

const HistoryList = ({ entityType, entityId, refreshKey }) => {
    const [history, setHistory] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [refresh, setRefresh] = useState(0);
    const [selectedRow, setSelectedRow] = useState(null);
    const detailTriggerRef = useRef(null);
    const closeDetails = useCallback(() => {
        setSelectedRow(null);
        detailTriggerRef.current?.focus();
    }, []);
    const resource = entityType === 'supplierOrder' ? 'supplierorders' : 'customerorders';

    useEffect(() => {
        if (!(entityId > 0)) return undefined;
        let active = true;
        const url = `/orderhistory/${resource}/${entityId}`;
        getSharedRequest(`${url}:${refreshKey ?? ''}:${refresh}`, () => apiClient.get(url))
            .then((response) => {
                if (!Array.isArray(response.data?.rows) || typeof response.data?.showStockBalance !== 'boolean') {
                    throw new Error('Invalid order history response.');
                }
                if (active) setHistory(response.data);
            })
            .catch((requestError) => {
                if (!active) return;
                console.error('Failed to load order history:', requestError);
                setError(requestError?.response?.data?.message || 'Kunde inte hämta orderhistoriken.');
            })
            .finally(() => {
                if (active) setLoading(false);
            });
        return () => { active = false; };
    }, [entityId, resource, refreshKey, refresh]);

    if (!(entityId > 0)) return null;

    const rows = history?.rows ?? [];
    const showStock = history?.showStockBalance ?? true;
    const columns = showStock ? 10 : 9;
    return (
        <section className="mt-6 mb-6 w-full" aria-label="Orderhistorik">
            <div className="mb-3 flex items-center justify-between gap-4">
                {/* <h3 className="text-sm font-semibold text-gray-600">Historik</h3> */}
                <button type="button" onClick={() => {
                    setLoading(true);
                    setError('');
                    setHistory(null);
                    setSelectedRow(null);
                    setRefresh((value) => value + 1);
                }} disabled={loading}
                    className="inline-flex items-center gap-1 text-xs text-gray-600 hover:underline disabled:opacity-50">
                    <RefreshCw size={13} className={loading ? 'animate-spin' : ''} /> Uppdatera historik
                </button>
            </div>
            {error && <p role="alert" className="mb-3 rounded border border-red-200 bg-red-50 p-3 text-xs text-red-700">{error}</p>}
            <div className="overflow-x-auto">
                <table className="w-full text-xs" style={TABLE_FONT_STYLE}>
                    <thead className="border-t border-gray-300 text-tiny text-gray-500">
                        <tr>
                            {['Datum', 'Typ', 'Status', 'Till', 'TO/Avrop', 'Faktura', 'Antal', 'Prod rest', ...(showStock ? ['Lagersaldo'] : []), 'Visa'].map((label, index) => (
                                <th key={label} scope="col" className={`px-2 py-1.5 font-medium whitespace-nowrap ${index >= 4 ? 'text-right' : 'text-left'}`}>{label}</th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? <tr><td colSpan={columns} role="status" className="px-2 py-5 text-center text-gray-500">Hämtar historik…</td></tr>
                            : !error && rows.length === 0 ? <tr><td colSpan={columns} className="px-2 py-5 text-center text-gray-500">Ingen historik att visa.</td></tr>
                                : rows.map((row) => {
                                    const transport = getHistoryTransportLink(row);
                                    const detailUrl = getHistoryDetailUrl(row);
                                    return (
                                        <tr key={`${row.type}-${row.id}`} className="border-b border-gray-200/60 hover:bg-lime-100/60">
                                            <td className="px-2 py-1.5 whitespace-nowrap">{formatDateShort(row.date)}</td>
                                            <td className="px-2 py-1.5 whitespace-nowrap">{row.typeName}</td>
                                            <td className="px-2 py-1.5 whitespace-nowrap">{row.status}</td>
                                            <td className="max-w-52 truncate px-2 py-1.5" title={row.to}>{row.to}</td>
                                            <td className="px-2 py-1.5 text-right">
                                                {transport && <Link to={transport.to} className={linkClass}>{transport.label}</Link>}
                                            </td>
                                            <td className="px-2 py-1.5 text-right">
                                                <div className="flex flex-col items-end gap-1">
                                                    {row.invoices.map((invoice) => <Link key={invoice.id} to={`/finance/invoice/${invoice.id}`} className={linkClass}>{invoice.number ?? invoice.id}</Link>)}
                                                </div>
                                            </td>
                                            <td className="px-2 py-1.5 text-right whitespace-nowrap tabular-nums">{formatHistoryQuantity(row)}</td>
                                            <td className="px-2 py-1.5 text-right tabular-nums">{formatNumber(row.productionBalance)}</td>
                                            {showStock && <td className="px-2 py-1.5 text-right tabular-nums">{formatNumber(row.stockBalance)}</td>}
                                            <td className="px-2 py-1.5 text-right">
                                                {detailUrl && <button type="button" onClick={(event) => {
                                                    detailTriggerRef.current = event.currentTarget;
                                                    setSelectedRow(row);
                                                }} className={linkClass} aria-label={`Visa ${row.typeName.toLowerCase()} ${row.id}`}>Visa</button>}
                                                {row.type === 'Invoice' && <Link to={`/finance/invoice/${row.id}`} className={linkClass}>Visa</Link>}
                                            </td>
                                        </tr>
                                    );
                                })}
                    </tbody>
                </table>
            </div>
            {selectedRow && <HistoryDetails row={selectedRow} onClose={closeDetails} />}
        </section>
    );
};

const OrderHistory = ({ entityType, entityId, refreshKey }) => entityId > 0 ? (
    <HistoryList
        key={`${entityType}:${entityId}:${refreshKey ?? ''}`}
        entityType={entityType}
        entityId={entityId}
        refreshKey={refreshKey}
    />
) : null;

export default OrderHistory;
