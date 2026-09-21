import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, CheckCircle, Loader2, XCircle } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import DateRangePicker from '../../components/DaterangePicker';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import apiClient from '../../config/apiClient';
import { formatDateShort } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 20;

const toQueryDate = (value) => {
    if (!value) return null;

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return null;

    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};

const formatAmount = (value) => new Intl.NumberFormat('sv-SE', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
}).format(Number(value) || 0);

function SelectCircleCheckbox({ checked, onChange, ariaLabel }) {
    return (
        <button
            type="button"
            role="checkbox"
            aria-checked={checked}
            aria-label={ariaLabel}
            onClick={onChange}
            className="inline-flex h-[15px] w-[15px] shrink-0 items-center justify-center rounded-full bg-[#f1f3f2] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-lime-700"
        >
            <span className={`inline-flex h-[15px] w-[15px] items-center justify-center rounded-full border transition-colors ${checked ? 'border-[#368b3f] bg-[#3f9848]' : 'border-[#c8cfcb] bg-white'}`}>
                <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-2.5 w-2.5 text-white ${checked ? 'opacity-100' : 'opacity-0'}`}>
                    <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </span>
        </button>
    );
}

const InvoicesToAccount = () => {
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [error, setError] = useState(null);
    const [fromDate, setFromDate] = useState(null);
    const [toDate, setToDate] = useState(null);
    const [invoiceNumber, setInvoiceNumber] = useState('');
    const [page, setPage] = useState(1);
    const [totalPages, setTotalPages] = useState(0);
    const [totalCount, setTotalCount] = useState(0);
    const [refreshVersion, setRefreshVersion] = useState(0);
    const [processing, setProcessing] = useState(false);
    const [useTestApi, setUseTestApi] = useState(false);
    const [processingResults, setProcessingResults] = useState({});
    const requestSequenceRef = useRef(0);
    const skeletonTimerRef = useRef(null);

    useEffect(() => {
        let isActive = true;
        const requestId = ++requestSequenceRef.current;
        const timer = window.setTimeout(async () => {
            const parsedInvoiceNumber = invoiceNumber.trim() === '' ? null : Number(invoiceNumber);
            const requestBody = {
                pageNumber: page,
                pageSize: PAGE_SIZE,
                invoiceDateFrom: toQueryDate(fromDate),
                invoiceDateTo: toQueryDate(toDate),
                invoiceNumber: Number.isInteger(parsedInvoiceNumber) && parsedInvoiceNumber > 0 ? parsedInvoiceNumber : null,
            };

            setLoading(true);
            setError(null);
            setShowSkeleton(false);
            skeletonTimerRef.current = window.setTimeout(() => setShowSkeleton(true), 200);

            try {
                const response = await getSharedRequest(
                    `finance:unbooked-invoices:${JSON.stringify(requestBody)}`,
                    () => apiClient.post('/finance/unbooked-invoices', requestBody),
                );
                if (!isActive || requestId !== requestSequenceRef.current) return;

                const data = response?.data ?? {};
                setRows((data.items ?? []).map((row) => ({ ...row, selected: false })));
                setTotalPages(data.totalPages ?? 0);
                setTotalCount(data.totalCount ?? 0);
            } catch (requestError) {
                if (!isActive || requestId !== requestSequenceRef.current) return;
                setRows([]);
                setTotalPages(0);
                setTotalCount(0);
                setError(requestError.response?.data?.message || requestError.message || 'Kunde inte hämta fakturor.');
            } finally {
                if (isActive && requestId === requestSequenceRef.current) {
                    window.clearTimeout(skeletonTimerRef.current);
                    setShowSkeleton(false);
                    setLoading(false);
                }
            }
        }, 250);

        return () => {
            isActive = false;
            window.clearTimeout(timer);
            window.clearTimeout(skeletonTimerRef.current);
        };
    }, [fromDate, invoiceNumber, page, refreshVersion, toDate]);

    const updateFilters = (updates) => {
        if ('fromDate' in updates) setFromDate(updates.fromDate);
        if ('toDate' in updates) setToDate(updates.toDate);
        if ('invoiceNumber' in updates) setInvoiceNumber(updates.invoiceNumber);
        setPage(1);
    };

    const toggleInvoice = (invoiceId) => {
        setRows((currentRows) => currentRows.map((row) => (
            row.invoiceId === invoiceId ? { ...row, selected: !row.selected } : row
        )));
    };

    const bookSelectedInvoices = async () => {
        const selectedRows = rows.filter((row) => row.selected);
        if (selectedRows.length === 0 || processing) return;

        setProcessing(true);
        setProcessingResults({});

        for (const row of selectedRows) {
            setProcessingResults((current) => ({
                ...current,
                [row.invoiceId]: { status: 'processing', message: 'Bokför...' },
            }));

            try {
                const response = await apiClient.post('/finance/book-invoices', { invoiceIds: [row.invoiceId], useTestApi });
                const result = response?.data;
                const exportResult = result?.results?.find((item) => item.invoiceId === row.invoiceId);
                const succeeded = Boolean(exportResult?.succeeded);

                setProcessingResults((current) => ({
                    ...current,
                    [row.invoiceId]: succeeded
                        ? { status: 'success', message: useTestApi ? 'Testexporterad' : exportResult.message }
                        : { status: 'error', message: exportResult?.message || 'Redan bokförd eller saknas' },
                }));
            } catch (requestError) {
                setProcessingResults((current) => ({
                    ...current,
                    [row.invoiceId]: {
                        status: 'error',
                        message: requestError.response?.data?.message || requestError.message || 'Fel vid bokföring',
                    },
                }));
            }
        }

        setProcessing(false);
        if (!useTestApi) {
            setRefreshVersion((version) => version + 1);
        }
    };

    const selectedCount = rows.filter((row) => row.selected).length;
    const allSelected = rows.length > 0 && rows.every((row) => row.selected);

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className={`relative z-20 flex items-center gap-4 overflow-visible whitespace-nowrap pb-2 mt-2 pl-16 ${loading || processing ? 'pointer-events-none opacity-70' : ''}`}>
                <div className="flex flex-wrap items-center gap-4">
                    <DateRangePicker
                        presets={['this-month', 'last-month', 'last-3-months', 'last-12-months', 'last-year', 'year-to-date']}
                        placeholder="Välj period"
                        onApply={({ startDate, endDate }) => updateFilters({ fromDate: startDate ?? null, toDate: endDate ?? null })}
                        triggerRadius="full"
                        triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                        openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                        closedTriggerClassName="border-lime-600 hover:border-lime-700"
                        widthClassName="w-60"
                    />
                    <input
                        id="invoice-number"
                        type="number"
                        min="1"
                        value={invoiceNumber}
                        onChange={(event) => updateFilters({ invoiceNumber: event.target.value })}
                        className="h-7 w-[120px] rounded-full border border-lime-600 bg-white px-3 text-xs text-gray-700 outline-none transition focus:border-lime-700"
                        placeholder="Fakturanr."
                    />
                    <button
                        type="button"
                        onClick={bookSelectedInvoices}
                        disabled={selectedCount === 0}
                        className="ml-20 mr-20 h-7 rounded-full border border-lime-700 bg-lime-700 px-4 text-xs text-white transition hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        Bokför valda fakturor
                    </button>
                    <LabeledCheckbox
                        label="Använd test-API"
                        checked={useTestApi}
                        onChange={setUseTestApi}
                        disabled={processing}
                        color="green"
                    />
                    {selectedCount > 0 && <span className="text-xs text-gray-500">Valda fakturor: <strong>{selectedCount}</strong></span>}
                </div>

                <div className="ml-auto mr-4 flex items-center gap-3 text-xs text-gray-700">
                    <span>Fakturor <strong>{totalCount}</strong></span>
                    <span>Sida <strong>{page}</strong> av <strong>{Math.max(1, totalPages)}</strong></span>
                    <div className="flex gap-1">
                        <button type="button" onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page <= 1 || loading || processing} className="disabled:cursor-not-allowed disabled:opacity-50" aria-label="Föregående sida">
                            <ArrowLeftCircle className="h-5 w-5 text-red-300 hover:text-red-400" />
                        </button>
                        <button type="button" onClick={() => setPage((current) => Math.min(totalPages, current + 1))} disabled={loading || processing || page >= totalPages} className="disabled:cursor-not-allowed disabled:opacity-50" aria-label="Nästa sida">
                            <ArrowRightCircle className="h-5 w-5 text-red-300 hover:text-red-400" />
                        </button>
                    </div>
                </div>
            </div>

            {error && <div className="mt-3 rounded border border-red-400 bg-red-100 px-4 py-3 text-xs text-red-700">{error}</div>}

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className="min-w-full table-fixed" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead className="">
                        <tr>
                            <th className="w-10 px-2 py-1 text-left text-tiny font-medium tracking-wider text-gray-400">
                                <SelectCircleCheckbox
                                    checked={allSelected}
                                    onChange={() => setRows((currentRows) => currentRows.map((row) => ({ ...row, selected: !allSelected })))}
                                    ariaLabel="Markera alla fakturor"
                                />
                            </th>
                            <th className="w-[100px] px-2 py-1.5 text-left text-tiny font-medium tracking-wider text-gray-400">Fakturanr</th>
                            <th className="w-[260px] px-2 py-1.5 text-left text-tiny font-medium tracking-wider text-gray-400">Kund</th>
                            <th className="w-[120px] px-2 py-1.5 text-left text-tiny font-medium tracking-wider text-gray-400">Fakturadatum</th>
                            <th className="w-[120px] px-2 py-1.5 text-left text-tiny font-medium tracking-wider text-gray-400">Förfallodatum</th>
                            <th className="w-[140px] px-2 py-1.5 text-right text-tiny font-medium tracking-wider text-gray-400">Belopp ink moms</th>
                            <th className=" pl-10 py-1.5 text-left text-tiny font-medium tracking-wider text-gray-400">Status</th>
                        </tr>
                    </thead>
                    <tbody className={!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}>
                        {showSkeleton ? Array.from({ length: 8 }).map((_, index) => (
                            <tr key={`invoice-skeleton-${index}`} className="border-b border-gray-100">
                                {Array.from({ length: 7 }).map((__, cellIndex) => <td key={cellIndex} className="px-2 py-2"><Skeleton height={14} circle={cellIndex === 0} width={cellIndex === 0 ? 15 : undefined} /></td>)}
                            </tr>
                        )) : rows.length === 0 && !loading ? (
                            <tr><td colSpan="7" className="px-6 py-14 text-center text-sm text-gray-500">Inga fakturor att visa</td></tr>
                        ) : rows.map((row) => {
                            const result = processingResults[row.invoiceId];
                            return (
                                <tr key={row.invoiceId} className={`cursor-pointer border-b border-gray-100 ${row.selected ? 'bg-lime-100' : 'hover:bg-lime-50'}`} onClick={() => toggleInvoice(row.invoiceId)}>
                                    <td className="px-2 py-[4px]" onClick={(event) => event.stopPropagation()}><SelectCircleCheckbox checked={Boolean(row.selected)} onChange={() => toggleInvoice(row.invoiceId)} ariaLabel={`Markera faktura ${row.invoiceNumber}`} /></td>
                                    <td className="px-2 py-[6px] text-xs text-gray-800"><Link to={`/finance/invoice/${row.invoiceId}`} target="_blank" rel="noopener noreferrer" onClick={(event) => event.stopPropagation()} className="text-sky-700 underline-offset-2 hover:text-sky-800 hover:underline">{row.invoiceNumber ?? row.invoiceId}</Link></td>
                                    <td className="px-2 py-[6px] text-xs text-gray-800">{row.customerName}</td>
                                    <td className="px-2 py-[6px] text-xs text-gray-800">{formatDateShort(row.invoiceDate)}</td>
                                    <td className="px-2 py-[6px] text-xs text-gray-800">{formatDateShort(row.dueDate)}</td>
                                    <td className="px-2 py-[6px] text-right text-xs text-gray-800">{formatAmount(row.amount)} <span className="text-gray-400">SEK</span></td>
                                    <td className="pl-10 text-xs">
                                        {result?.status === 'processing' && <span className="inline-flex items-center text-blue-600"><Loader2 className="mr-2 h-4 w-4 animate-spin" />{result.message}</span>}
                                        {result?.status === 'success' && <span className="inline-flex items-center text-green-600"><CheckCircle className="mr-2 h-4 w-4" />{result.message}</span>}
                                        {result?.status === 'error' && <span className="inline-flex items-center text-red-600"><XCircle className="mr-2 h-4 w-4" />{result.message}</span>}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default InvoicesToAccount;