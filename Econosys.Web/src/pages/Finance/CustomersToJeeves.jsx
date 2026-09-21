import { useEffect, useMemo, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, CheckCircle, ChevronDown, ChevronUp, Loader2, Search, XCircle } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const columns = [
    { key: 'id', label: 'Kundnr', align: 'left', width: '9%', sortable: false },
    { key: 'name', label: 'Namn', align: 'left', width: '24%', sortBy: 'name' },
    { key: 'orgNr', label: 'Org nr', align: 'left', width: '14%', sortable: false },
    { key: 'email', label: 'Email', align: 'left', width: '20%', sortBy: 'email' },
    { key: 'active', label: 'Aktiv', align: 'left', width: '7%', sortable: false },
    { key: 'lastActivity', label: 'Senaste aktivitet', align: 'left', width: '16%', sortBy: 'lastactivity' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

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

const CustomersToJeeves = () => {
    const navigate = useNavigate();
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'name', direction: 'asc' });
    const [processing, setProcessing] = useState(false);
    const [useTestApi, setUseTestApi] = useState(false);
    const [processingResults, setProcessingResults] = useState({});

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((current) => ({ ...current, pageNumber: 1 }));
        }, 300);

        return () => window.clearTimeout(timer);
    }, [searchInput]);

    const requestBody = useMemo(() => ({
        searchTerm: searchTerm.trim() || null,
        active: true,
        sortBy: sortConfig.key,
        sortDescending: sortConfig.direction === 'desc',
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
    }), [pagination.pageNumber, pagination.pageSize, searchTerm, sortConfig.direction, sortConfig.key]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const response = await getSharedRequest(
                    `jeeves-customers:search:${JSON.stringify(requestBody)}`,
                    () => apiClient.post('/customers/search', requestBody),
                );
                if (!isActive) return;

                const data = response?.data ?? {};
                const pageNumber = data.pageNumber ?? pagination.pageNumber;
                const totalPages = data.totalPages ?? 0;
                setRows((data.items ?? []).map((row) => ({ ...row, selected: false })));
                setPagination((current) => ({
                    ...current,
                    pageNumber,
                    pageSize: data.pageSize ?? current.pageSize,
                    totalCount: data.totalCount ?? 0,
                    totalPages,
                    hasPreviousPage: pageNumber > 1,
                    hasNextPage: totalPages > 0 && pageNumber < totalPages,
                }));
                setProcessingResults({});
            } catch (error) {
                console.error('Failed to load customers for Jeeves sync:', error);
                if (!isActive) return;
                setRows([]);
                setPagination((current) => ({ ...current, ...initialPagination }));
            } finally {
                if (isActive) {
                    setSearchLoaded(true);
                    setLoading(false);
                }
            }
        };

        loadRows();
        return () => {
            isActive = false;
        };
    }, [pagination.pageNumber, pagination.pageSize, requestBody]);

    const handleSort = (column) => {
        if (!column.sortBy) return;

        setSortConfig((current) => ({
            key: column.sortBy,
            direction: current.key === column.sortBy && current.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((current) => ({ ...current, pageNumber: 1 }));
    };

    const handlePageChange = (nextPage) => {
        if (nextPage < 1 || (pagination.totalPages > 0 && nextPage > pagination.totalPages)) return;
        setPagination((current) => ({ ...current, pageNumber: nextPage }));
    };

    const toggleCustomer = (customerId) => {
        setRows((currentRows) => currentRows.map((row) => (
            row.id === customerId ? { ...row, selected: !row.selected } : row
        )));
    };

    const syncSelectedCustomers = async () => {
        const selectedRows = rows.filter((row) => row.selected);
        if (selectedRows.length === 0 || processing) return;

        setProcessing(true);
        setProcessingResults({});

        for (const row of selectedRows) {
            setProcessingResults((current) => ({
                ...current,
                [row.id]: { status: 'processing', message: 'Synkar...' },
            }));

            try {
                const response = await apiClient.post('/finance/sync-customers', {
                    customerIds: [row.id],
                    useTestApi,
                });
                const result = response?.data?.results?.find((item) => item.customerId === row.id);

                setProcessingResults((current) => ({
                    ...current,
                    [row.id]: result?.succeeded
                        ? { status: 'success', message: useTestApi ? 'Testsynkad' : result.message }
                        : { status: 'error', message: result?.message || 'Kunden saknas eller kunde inte synkas' },
                }));
            } catch (error) {
                setProcessingResults((current) => ({
                    ...current,
                    [row.id]: {
                        status: 'error',
                        message: error.response?.data?.message || error.message || 'Fel vid synkning',
                    },
                }));
            }
        }

        setProcessing(false);
    };

    const selectedCount = rows.filter((row) => row.selected).length;
    const allSelected = rows.length > 0 && rows.every((row) => row.selected);
    const showSkeleton = loading && !searchLoaded;

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className={`relative z-20 flex items-center gap-4 overflow-visible whitespace-nowrap pb-2 mt-2 pl-16 ${processing ? 'pointer-events-none opacity-70' : ''}`}>
                <div className="relative w-56">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchInput}
                        onChange={(event) => setSearchInput(event.target.value)}
                        placeholder="Sök kund"
                        className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                    />
                </div>
                <button
                    type="button"
                    onClick={syncSelectedCustomers}
                    disabled={selectedCount === 0 || loading}
                    className="ml-12 h-7 rounded-full border border-lime-700 bg-lime-700 px-4 text-xs text-white transition hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-50"
                >
                    Synka valda kunder
                </button>
                <LabeledCheckbox
                    label="Använd test-API"
                    checked={useTestApi}
                    onChange={setUseTestApi}
                    disabled={processing}
                    color="green"
                />
                {selectedCount > 0 && <span className="text-xs text-gray-500">Valda kunder: <strong>{selectedCount}</strong></span>}

                <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={TABLE_FONT_STYLE}>
                    <span>Rader <strong>{pagination.totalCount}</strong></span>
                    <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                    <div className="flex gap-1">
                        <button type="button" onClick={() => handlePageChange(pagination.pageNumber - 1)} disabled={loading || !pagination.hasPreviousPage} className="disabled:cursor-not-allowed disabled:opacity-40" aria-label="Föregående sida">
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button type="button" onClick={() => handlePageChange(pagination.pageNumber + 1)} disabled={loading || !pagination.hasNextPage} className="disabled:cursor-not-allowed disabled:opacity-40" aria-label="Nästa sida">
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="mt-3 min-h-0 flex-1 overflow-auto border-t border-gray-300 py-1">
                <table className="w-full table-fixed border-collapse text-xs" style={TABLE_FONT_STYLE}>
                    <colgroup>
                        <col style={{ width: '4%' }} />
                        {columns.map((column) => <col key={column.key} style={{ width: column.width }} />)}
                        <col style={{ width: '20%' }} />
                    </colgroup>
                    <thead className="">
                        <tr className="text-tiny text-gray-500">
                            <th className="w-10 px-2 py-1 text-left text-tiny font-medium tracking-wider text-gray-400">
                                <SelectCircleCheckbox
                                    checked={allSelected}
                                    onChange={() => setRows((currentRows) => currentRows.map((row) => ({ ...row, selected: !allSelected })))}
                                    ariaLabel="Markera alla kunder"
                                />
                            </th>
                            {columns.map((column) => {
                                const isSorted = sortConfig.key === column.sortBy;
                                return (
                                    <th key={column.key} onClick={() => handleSort(column)} className={`${column.sortBy ? 'cursor-pointer' : 'cursor-default'} px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500`}>
                                        <span className="inline-flex items-center gap-1">
                                            {column.label}
                                            {column.sortBy && (isSorted
                                                ? (sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />)
                                                : <ChevronUp className="h-3 w-3 opacity-0" />)}
                                        </span>
                                    </th>
                                );
                            })}
                            <th className="pl-4 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Status</th>
                        </tr>
                    </thead>
                    <tbody className={!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index}>{Array.from({ length: columns.length + 2 }).map((__, cellIndex) => <td key={cellIndex} className="px-2 py-1"><Skeleton height={16} /></td>)}</tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr><td colSpan={columns.length + 2} className="px-4 py-8 text-center text-gray-400">Inga aktiva kunder hittades.</td></tr>
                        ) : rows.map((row) => {
                            const result = processingResults[row.id];
                            return (
                                <tr key={row.id} className={`h-6 cursor-pointer border-b border-gray-100 ${row.selected ? 'bg-lime-100/80' : 'hover:bg-lime-200/70'}`} onClick={() => toggleCustomer(row.id)}>
                                    <td className="px-2 py-1" onClick={(event) => event.stopPropagation()}><SelectCircleCheckbox checked={Boolean(row.selected)} onChange={() => toggleCustomer(row.id)} ariaLabel={`Markera kund ${row.id}`} /></td>
                                    <td className="truncate px-2 py-1 text-gray-800"><button type="button" onClick={(event) => { event.stopPropagation(); navigate(`/order/customers/${row.id}`); }} className="text-slate-700 hover:text-slate-900 hover:underline">{row.id}</button></td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.name}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.orgNr}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.email}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.active ? 'Ja' : 'Nej'}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.lastActivity}</td>
                                    <td className="pl-4 py-1 text-xs">
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

export default CustomersToJeeves;
