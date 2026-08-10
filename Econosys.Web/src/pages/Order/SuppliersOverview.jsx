import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import LabeledSwitch from '../../components/LabeledSwitch';
import { getSwedishTodayDateString } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;

const listColumns = [
    { key: 'id', label: 'NR', width: '4%' },
    { key: 'name', label: 'LEVERANTOR', width: '18%' },
    { key: 'address', label: 'ADRESS', width: '16%' },
    { key: 'postalNr', label: 'POSTNR', width: '7%' },
    { key: 'postalAddress', label: 'ORT', width: '10%' },
    { key: 'country', label: 'LAND', width: '8%' },
    { key: 'note', label: 'NOTERING' },
    { key: 'view', label: 'VISA', width: '4%' },
];

const metricGroups = [
    {
        key: 'currentYear',
        label: 'INNEVARANDE AR',
        className: 'bg-green-200',
        columns: ['INK-VARDE', 'FSG-VARDE', 'FRAKT', 'ANT ORDER', 'PA-SLAG'],
    },
    {
        key: 'currentVsPrevious',
        label: 'INNEV. vs FOREG.',
        className: 'bg-yellow-100',
        columns: ['INK-VARDE', 'FSG-VARDE', 'ANT ORDER'],
    },
    {
        key: 'previousYtd',
        label: 'FOREGAENDE YTD',
        className: 'bg-sky-100',
        columns: ['INK-VARDE', 'FSG-VARDE', 'FRAKT', 'ANT ORDER', 'PA-SLAG'],
    },
    {
        key: 'previousYear',
        label: 'FOREGAENDE HELAR',
        className: 'bg-blue-200',
        columns: ['INK-VARDE', 'FSG-VARDE', 'FRAKT', 'ANT ORDER', 'PA-SLAG'],
    },
];

const metricColumnCount = metricGroups.reduce((sum, group) => sum + group.columns.length, 0);

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const SuppliersOverview = () => {
    const navigate = useNavigate();
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [includeInactive, setIncludeInactive] = useState(true);
    const [pagination, setPagination] = useState(initialPagination);
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 350);

        return () => clearTimeout(timer);
    }, [searchInput]);

    const requestBody = useMemo(() => ({
        searchTerm: searchTerm.trim() || null,
        active: includeInactive ? null : true,
        sortBy: 'name',
        sortDescending: false,
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
    }), [includeInactive, pagination.pageNumber, pagination.pageSize, searchTerm]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestKey = `suppliers:search:${JSON.stringify(requestBody)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/suppliers/search', requestBody));
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
                console.error('Failed to load suppliers overview:', error);
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

    const summaryRows = useMemo(
        () => rows.slice(0, 10).map((row) => ({
            id: row.id,
            name: row.name,
        })),
        [rows]
    );

    const showSkeleton = loading && !searchLoaded;

    const handlePageChange = (nextPage) => {
        if (nextPage < 1) return;
        if (pagination.totalPages > 0 && nextPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const escapeCsv = (value) => `"${String(value ?? '').replace(/"/g, '""')}"`;

    const toCsvLine = (values) => values.map(escapeCsv).join(';');

    const exportVisibleRows = () => {
        if (!rows.length) return;

        const headers = ['NR', 'LEVERANTOR', 'ADRESS', 'POSTNR', 'ORT', 'LAND', 'NOTERING', 'AKTIV'];
        const lines = [
            toCsvLine(headers),
            ...rows.map((row) => toCsvLine([
                row.id,
                row.name,
                row.address,
                row.postalNr,
                row.postalAddress,
                row.country,
                row.note,
                row.active ? 'Ja' : 'Nej',
            ])),
        ];

        const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `leverantorer-visade-${getSwedishTodayDateString()}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    const exportAllRows = async () => {
        try {
            const requestBodyAll = {
                ...requestBody,
                pagination: {
                    pageNumber: 1,
                    pageSize: 10000,
                },
            };
            const requestKey = `suppliers:search:all:${JSON.stringify(requestBodyAll)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/suppliers/search', requestBodyAll));
            const allRows = response?.data?.items ?? [];
            if (!allRows.length) return;

            const headers = ['NR', 'LEVERANTOR', 'ADRESS', 'POSTNR', 'ORT', 'LAND', 'NOTERING', 'AKTIV'];
            const lines = [
                toCsvLine(headers),
                ...allRows.map((row) => toCsvLine([
                    row.id,
                    row.name,
                    row.address,
                    row.postalNr,
                    row.postalAddress,
                    row.country,
                    row.note,
                    row.active ? 'Ja' : 'Nej',
                ])),
            ];

            const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `leverantorer-alla-${getSwedishTodayDateString()}.csv`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export all suppliers:', error);
        }
    };

    const getRowClass = (rowId) => {
        if (selectedRowId === rowId) {
            return 'cursor-pointer border-b border-amber-200 bg-amber-100';
        }

        return 'cursor-pointer border-b border-gray-100 hover:bg-amber-50';
    };

    const handleOpenSupplier = (event, supplierId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/suppliers/${supplierId}`);
    };

    return (
        <div className="flex h-full flex-col gap-5 pt-1 pb-4 ps-5 pe-10">
            <div className="grid grid-cols-[1fr] gap-14 px-2 mt-2">
                {/* <section className="overflow-hidden">
                    <div className="border-b border-gray-300 pb-2 text-center text-xs text-gray-500">
                        <span className="inline-flex items-center gap-4">
                            <span><span className="mr-1 inline-block h-2.5 w-2.5 bg-orange-500 align-middle" /> riskzon</span>
                            <span><span className="mr-1 inline-block h-2.5 w-2.5 bg-teal-500 align-middle" /> ser bra ut</span>
                            <span><span className="mr-1 inline-block h-2.5 w-2.5 bg-gray-400 align-middle" /> neutralt</span>
                        </span>
                    </div>
                    <div className="mt-6 text-center text-sm text-gray-500">Far fundera pa innehall har</div>
                </section> */}

                <section className="overflow-hidden">
                    <div className="mb-2 flex items-center justify-end gap-8 px-2 text-xs text-gray-500">
                        <span>Up and Down</span>
                        <span>Exportera till EXCEL</span>
                        <span>Visa info</span>
                    </div>

                    <div className="overflow-auto">
                        <table className="min-w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <thead>
                                <tr className="border border-gray-200 text-[10px] text-gray-600">
                                    <th rowSpan={2} className="border border-gray-200 px-2 py-1 text-left font-medium">LEVERANTOR</th>
                                    {metricGroups.map((group) => (
                                        <th key={group.key} colSpan={group.columns.length} className={`border border-gray-200 px-2 py-1 text-center font-medium ${group.className}`}>
                                            {group.label}
                                        </th>
                                    ))}
                                </tr>
                                <tr className="border border-gray-200 text-[10px] text-gray-500">
                                    {metricGroups.map((group) => (
                                        group.columns.map((column) => (
                                            <th key={`${group.key}-${column}`} className={`border border-gray-200 px-2 py-1 text-right font-medium ${group.className}`}>
                                                {column}
                                            </th>
                                        ))
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {showSkeleton ? (
                                    Array.from({ length: 10 }).map((_, index) => (
                                        <tr key={index}>
                                            <td className="border border-gray-200 px-2 py-1" colSpan={metricColumnCount + 1}><Skeleton height={14} /></td>
                                        </tr>
                                    ))
                                ) : summaryRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={metricColumnCount + 1} className="border border-gray-200 px-4 py-4 text-center text-gray-400">Inga leverantorer hittades.</td>
                                    </tr>
                                ) : (
                                    summaryRows.map((row) => (
                                        <tr key={row.id} className="border border-gray-200">
                                            <td className="border border-gray-200 px-2 py-1 text-left text-gray-700">{row.name}</td>
                                            {Array.from({ length: metricColumnCount }).map((_, index) => (
                                                <td key={`${row.id}-${index}`} className="border border-gray-200 px-2 py-1 text-right text-gray-500">-</td>
                                            ))}
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>
                </section>
            </div>

            <section className="flex min-h-0 flex-1 flex-col mt-8">
                <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-1">
                    <button
                        type="button"
                        onClick={() => navigate('/order/suppliers/new')}
                        className="w-40 bg-green-700 px-4 py-[5px] text-xs text-white shadow-md/30 hover:bg-green-800"
                    >
                        Skapa ny leverantör
                    </button>

                    <div className="relative ml-8 w-44">
                        <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                        <input
                            type="text"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                            placeholder="Leverantorsnamn"
                            className="w-full rounded-sm border border-gray-300 bg-white py-1 pl-7 pr-2 text-xs focus:outline-none"
                        />
                    </div>

                    <LabeledSwitch
                        label="Visa aktiva/ej aktiva"
                        name="includeInactive"
                        value={includeInactive}
                        onChange={(_rowId, _field, checked) => {
                            setIncludeInactive(checked);
                            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                        }}
                        disabled={loading}
                        containerClassName="shrink-0"
                    />

                    <button
                        type="button"
                        onClick={exportVisibleRows}
                        disabled={!rows.length}
                        className="ml-8 text-xs font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        EXCEL, VISADE
                    </button>

                    <button
                        type="button"
                        onClick={exportAllRows}
                        disabled={loading}
                        className="text-xs font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        EXCEL, ALLT
                    </button>

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

                <div className="border-t border-gray-300 py-1 mt-4 min-h-0 flex-1 overflow-auto">
                    <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                        <colgroup>
                            {listColumns.map((column) => (
                                <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr>
                                {listColumns.map((column) => (
                                    <th key={column.key} className="px-2 py-2 text-left text-[10px] font-medium text-gray-500">
                                        {column.label}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                            {showSkeleton ? (
                                Array.from({ length: 12 }).map((_, index) => (
                                    <tr key={index}>
                                        <td colSpan={listColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                    </tr>
                                ))
                            ) : rows.length === 0 ? (
                                <tr>
                                    <td colSpan={listColumns.length} className="px-4 py-8 text-center text-gray-400">Inga leverantorer hittades.</td>
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
                                                onClick={(event) => handleOpenSupplier(event, row.id)}
                                                className="underline-offset-2 hover:underline"
                                            >
                                                {row.id}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(event) => handleOpenSupplier(event, row.id)}
                                                className="underline-offset-2 hover:underline"
                                            >
                                                {row.name}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">{[row.address, row.address2].filter(Boolean).join(' ')}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.postalNr}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.postalAddress}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.country}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={row.note}>{row.note}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">Visa</td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    );
};

export default SuppliersOverview;