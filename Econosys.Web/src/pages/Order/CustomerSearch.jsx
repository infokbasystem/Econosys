import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Plus, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import LabeledSwitch from '../../components/LabeledSwitch';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const columns = [
    { key: 'id', label: 'Kundnr', align: 'left', width: '10%', sortable: false },
    { key: 'name', label: 'Namn', align: 'left', width: '28%', sortBy: 'name' },
    { key: 'orgNr', label: 'Org nr', align: 'left', width: '16%', sortable: false },
    { key: 'email', label: 'Email', align: 'left', width: '24%', sortBy: 'email' },
    { key: 'active', label: 'Aktiv', align: 'left', width: '8%', sortable: false },
    { key: 'lastActivity', label: 'Senaste aktivitet', align: 'left', sortBy: 'lastactivity' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const CustomerSearch = () => {
    const navigate = useNavigate();

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [includeInactive, setIncludeInactive] = useState(false);
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'name', direction: 'asc' });
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 300);

        return () => clearTimeout(timer);
    }, [searchInput]);

    const requestBody = useMemo(() => ({
        searchTerm: searchTerm.trim() || null,
        active: includeInactive ? null : true,
        sortBy: sortConfig.key,
        sortDescending: sortConfig.direction === 'desc',
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
    }), [includeInactive, pagination.pageNumber, pagination.pageSize, searchTerm, sortConfig.direction, sortConfig.key]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestKey = `customers:search:${JSON.stringify(requestBody)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/customers/search', requestBody));
                if (!isActive) return;

                const data = response?.data ?? {};
                const totalCount = data.totalCount ?? 0;
                const totalPages = data.totalPages ?? 0;
                const pageNumber = data.pageNumber ?? pagination.pageNumber;

                setRows(data.items ?? []);
                setPagination((prev) => ({
                    ...prev,
                    pageNumber,
                    pageSize: data.pageSize ?? prev.pageSize,
                    totalCount,
                    totalPages,
                    hasPreviousPage: pageNumber > 1,
                    hasNextPage: totalPages > 0 && pageNumber < totalPages,
                }));
            } catch (error) {
                console.error('Failed to load customer search:', error);
                if (!isActive) return;
                setRows([]);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
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

    const showSkeleton = loading && !searchLoaded;

    const handleSort = (column) => {
        const nextKey = column.sortBy;

        if (!nextKey) {
            return;
        }

        setSortConfig((prev) => ({
            key: nextKey,
            direction: prev.key === nextKey && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (nextPage) => {
        if (nextPage < 1) return;
        if (pagination.totalPages > 0 && nextPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const getRowClass = (rowId) => {
        const isSelected = selectedRowId === rowId;
        return [
            'h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'bg-lime-100/80' : '',
            'hover:bg-lime-200/70',
        ].join(' ');
    };

    const handleOpenCustomer = (event, rowId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/customers/${rowId}`);
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="mt-2 flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2">
                <ActionButton
                    label="Ny kund"
                    icon={Plus}
                    onClick={() => navigate('/order/customers/new')}
                    accent="lime"
                />

                <div className="relative ml-16 w-56">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchInput}
                        onChange={(event) => setSearchInput(event.target.value)}
                        placeholder="Sok kund"
                        className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                    />
                </div>

                <LabeledSwitch
                    label="Inkludera inaktiva"
                    name="includeInactive"
                    value={includeInactive}
                    onChange={(_rowId, _field, checked) => {
                        setIncludeInactive(checked);
                        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                    }}
                    disabled={loading}
                    containerClassName="shrink-0"
                />

                <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <span>Rader <strong>{pagination.totalCount}</strong></span>
                    <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                    <div className="flex gap-1">
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber - 1)}
                            disabled={loading || !pagination.hasPreviousPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber + 1)}
                            disabled={loading || !pagination.hasNextPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="mt-3 min-h-0 flex-1 overflow-auto border-t border-gray-300 py-1">
                <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            {columns.map((column) => {
                                const isSorted = sortConfig.key === column.sortBy;

                                return (
                                    <th
                                        key={column.key}
                                        onClick={() => handleSort(column)}
                                        className={`${column.sortBy ? 'cursor-pointer' : 'cursor-default'} px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    >
                                        <span className="inline-flex items-center gap-1">
                                            {column.label}
                                            {column.sortBy ? (
                                                isSorted ? (
                                                    sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                                ) : (
                                                    <ChevronUp className="h-3 w-3 opacity-0" />
                                                )
                                            ) : null}
                                        </span>
                                    </th>
                                );
                            })}
                        </tr>
                    </thead>
                    <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index}>
                                    <td colSpan={columns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400">Inga kunder hittades.</td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr
                                    key={row.id}
                                    className={getRowClass(row.id)}
                                    onClick={() => setSelectedRowId((prev) => (prev === row.id ? null : row.id))}
                                >
                                    <td className="truncate px-2 py-1 text-gray-800">
                                        <button
                                            type="button"
                                            onClick={(event) => handleOpenCustomer(event, row.id)}
                                            className="text-slate-700 hover:text-slate-900 hover:underline"
                                        >
                                            {row.id}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.name}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.orgNr}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.email}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.active ? 'Ja' : 'Nej'}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.lastActivity}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default CustomerSearch;