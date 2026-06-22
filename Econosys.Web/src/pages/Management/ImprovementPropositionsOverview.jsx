import React, { useEffect, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ImprovementPropositionModal from '../../components/ImprovementPropositionModal';

const PAGE_SIZE = 20;

const AREA_CODE_OPTIONS = [
    { value: null, label: 'Alla' },
    { value: 'SALES', label: 'Försäljning' },
    { value: 'ORDER', label: 'Order' },
    { value: 'DELIVERY', label: 'Leverans' },
    { value: 'INVOICE', label: 'Faktura' },
    { value: 'QUALITY_ENVIRONMENT', label: 'Kvalitet/Miljö' },
    { value: 'SUPPLIER', label: 'Leverantör' },
];

const AREA_CODE_TO_ENUM = {
    SALES: 0,
    ORDER: 1,
    DELIVERY: 2,
    INVOICE: 3,
    QUALITY_ENVIRONMENT: 4,
    SUPPLIER: 5,
};

const STATUS_CODE_TO_ENUM = {
    NEW: 0,
    POSTPONED: 1,
    FINISHED: 2,
    ONGOING: 3,
};

const STATUS_ENUM_TO_CODE = {
    0: 'NEW',
    1: 'POSTPONED',
    2: 'FINISHED',
    3: 'ONGOING',
};

const AREA_ENUM_TO_CODE = {
    0: 'SALES',
    1: 'ORDER',
    2: 'DELIVERY',
    3: 'INVOICE',
    4: 'QUALITY_ENVIRONMENT',
    5: 'SUPPLIER',
};

const getStatusLabel = (code) => {
    const normalizedCode = typeof code === 'number' ? STATUS_ENUM_TO_CODE[code] : code;
    switch (normalizedCode) {
        case 'NEW': return 'Ny';
        case 'ONGOING': return 'Pågående';
        case 'POSTPONED': return 'Uppskjuten';
        case 'FINISHED': return 'Avslutad';
        default: return normalizedCode ?? '';
    }
};

const normalizeStatusCode = (code) => {
    if (typeof code === 'number') return STATUS_ENUM_TO_CODE[code] ?? '';
    return code ?? '';
};

const getAreaLabel = (code) => {
    const normalizedCode = typeof code === 'number' ? AREA_ENUM_TO_CODE[code] : code;
    const opt = AREA_CODE_OPTIONS.find((o) => o.value === normalizedCode);
    return opt ? opt.label : (normalizedCode ?? '');
};

const formatDate = (ts) => {
    if (!ts) return '';
    return new Date(ts).toLocaleDateString('sv-SE');
};

const openColumns = [
    { key: 'id', label: '#', width: '3%' },
    { key: 'createdByName', label: 'Skapad av', width: '7%' },
    { key: 'createdTimestamp', label: 'Datum', width: '8%' },
    { key: 'responsible', label: 'Ansvarig', width: '7%' },
    { key: 'areaCode', label: 'Område', width: '7%' },
    { key: 'statusCode', label: 'Status', width: '7%' },
    { key: 'description', label: 'Beskrivning', width: '' },
    { key: 'proposedMeasure', label: 'Föreslagen åtgärd' },
    { key: 'followUp', label: 'Uppföljning' },
];

const closedColumns = [
    { key: 'id', label: '#', width: '3%' },
    { key: 'createdByName', label: 'Skapad av', width: '7%' },
    { key: 'createdTimestamp', label: 'Datum', width: '8%' },
    { key: 'responsible', label: 'Ansvarig', width: '7%' },
    { key: 'areaCode', label: 'Område', width: '7%' },
    { key: 'statusCode', label: 'Status', width: '7%' },
    { key: 'description', label: 'Beskrivning', width: '' },
    { key: 'proposedMeasure', label: 'Föreslagen åtgärd' },
    { key: 'followUp', label: 'Uppföljning' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const ImprovementPropositionsOverview = () => {
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');

    const [openItems, setOpenItems] = useState([]);
    const [openLoading, setOpenLoading] = useState(true);
    const [hasOpenSnapshot, setHasOpenSnapshot] = useState(false);

    const [closedRows, setClosedRows] = useState([]);
    const [closedLoading, setClosedLoading] = useState(true);
    const [hasClosedSnapshot, setHasClosedSnapshot] = useState(false);

    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'createdTimestamp', direction: 'desc' });
    const [selectedAreaCode, setSelectedAreaCode] = useState(null);

    const [selectedOpenId, setSelectedOpenId] = useState(null);
    const [selectedClosedId, setSelectedClosedId] = useState(null);

    const [modalOpen, setModalOpen] = useState(false);
    const [editItem, setEditItem] = useState(null);
    const [refreshKey, setRefreshKey] = useState(0);

    const debounceRef = useRef(null);

    // Debounce search input
    useEffect(() => {
        if (debounceRef.current) clearTimeout(debounceRef.current);
        debounceRef.current = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 400);
        return () => clearTimeout(debounceRef.current);
    }, [searchInput]);

    // Load open items (NEW, ONGOING, POSTPONED)
    useEffect(() => {
        let cancelled = false;
        setOpenLoading(true);

        apiClient.post('/ImprovementPropositions/search', {
            pagination: null,
        })
            .then((response) => {
                if (cancelled) return;
                const allItems = response.data?.items ?? [];
                const openOnly = allItems.filter((item) => {
                    const status = normalizeStatusCode(item.statusCode);
                    return status === 'NEW' || status === 'ONGOING' || status === 'POSTPONED';
                });
                setOpenItems(openOnly);
                setHasOpenSnapshot(true);
            })
            .catch(() => {
                if (!cancelled) setOpenItems([]);
            })
            .finally(() => {
                if (!cancelled) setOpenLoading(false);
            });

        return () => { cancelled = true; };
    }, [refreshKey]);

    // Load closed items (FINISHED) with pagination + area filter
    useEffect(() => {
        let cancelled = false;
        setClosedLoading(true);

        apiClient.post('/ImprovementPropositions/search', {
            searchTerm: searchTerm || null,
            statusCode: STATUS_CODE_TO_ENUM.FINISHED,
            areaCode: selectedAreaCode !== null ? AREA_CODE_TO_ENUM[selectedAreaCode] : null,
            pagination: {
                pageNumber: pagination.pageNumber,
                pageSize: pagination.pageSize,
            },
            sortBy: sortConfig.key,
            sortDescending: sortConfig.direction === 'desc',
        })
            .then((res) => {
                if (cancelled) return;
                const data = res.data;
                setClosedRows(data?.items ?? []);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: data?.totalCount ?? 0,
                    totalPages: data?.totalPages ?? 0,
                    hasPreviousPage: data?.pageNumber > 1,
                    hasNextPage: data?.pageNumber < (data?.totalPages ?? 0),
                }));
                setHasClosedSnapshot(true);
            })
            .catch(() => {
                if (!cancelled) setClosedRows([]);
            })
            .finally(() => {
                if (!cancelled) setClosedLoading(false);
            });

        return () => { cancelled = true; };
    }, [searchTerm, pagination.pageNumber, pagination.pageSize, sortConfig, selectedAreaCode, refreshKey]);

    const handleSort = (key) => {
        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (targetPage) => {
        if (targetPage < 1 || targetPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: targetPage }));
    };

    const handleAreaCodeChange = (value) => {
        setSelectedAreaCode(value);
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleOpenProposalModal = (item) => {
        setEditItem(item);
        setModalOpen(true);
    };

    const getOpenRowClass = (item) => {
        const base = 'cursor-pointer border-b border-gray-100 transition-colors ';
        if (item.id === selectedOpenId) return base + 'bg-blue-50';
        if (normalizeStatusCode(item.statusCode) === 'POSTPONED') return base + 'hover:bg-gray-50 text-gray-400';
        return base + 'hover:bg-gray-50';
    };

    const getClosedRowClass = (row) => {
        const base = 'cursor-pointer border-b border-gray-100 transition-colors ';
        if (row.id === selectedClosedId) return base + 'bg-blue-50';
        return base + 'hover:bg-gray-50';
    };

    const showOpenSkeleton = openLoading;
    const showClosedSkeleton = closedLoading && !hasClosedSnapshot;

    return (
        <div className="flex h-full flex-col gap-5 pt-1 pb-4 ps-5 pe-10">

            <ImprovementPropositionModal
                isOpen={modalOpen}
                onClose={() => setModalOpen(false)}
                initialData={editItem}
                onSaved={() => {
                    setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                    setRefreshKey((k) => k + 1);
                }}
            />

            {/* Open items list (NEW, ONGOING, POSTPONED) */}
            <section className="mt-2">
                <div className="px-4 text-xs text-gray-500 text-center mb-2">Öppna förbättringsförslag</div>
                <div className="max-h-60 min-h-50 overflow-auto mt-5">
                    <table
                        className="table-fixed w-full border-collapse text-xs"
                        style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}
                    >
                        <colgroup>
                            {openColumns.map((col) => (
                                <col key={col.key} {...(col.width ? { style: { width: col.width } } : {})} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr className="border-b border-gray-200 text-tiny text-gray-500">
                                {openColumns.map((col) => (
                                    <th key={col.key} className="px-2 py-1 text-left font-medium">{col.label}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {showOpenSkeleton ? (
                                Array.from({ length: 5 }).map((_, i) => (
                                    <tr key={i}>
                                        <td colSpan={openColumns.length} className="px-2 py-1">
                                            <Skeleton height={16} />
                                        </td>
                                    </tr>
                                ))
                            ) : openItems.length === 0 ? (
                                <tr>
                                    <td colSpan={openColumns.length} className="px-4 py-6 text-center text-gray-400">
                                        Inga öppna förbättringsförslag.
                                    </td>
                                </tr>
                            ) : (
                                openItems.map((item) => (
                                    <tr
                                        key={item.id}
                                        onClick={() => setSelectedOpenId((prev) => (prev === item.id ? null : item.id))}
                                        className={getOpenRowClass(item)}
                                    >
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(e) => {
                                                    e.stopPropagation();
                                                    handleOpenProposalModal(item);
                                                }}
                                                className="text-blue-600 hover:text-blue-800 hover:underline"
                                            >
                                                {item.id}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">{item.createdByName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{formatDate(item.createdTimestamp)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{item.responsible}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getAreaLabel(item.areaCode)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getStatusLabel(item.statusCode)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.description}>{item.description}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.proposedMeasure}>{item.proposedMeasure}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.followUp}>{item.followUp}</td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>

            {/* Closed items list (FINISHED) with search, area switches, pagination */}
            <section className="flex min-h-0 flex-1 flex-col mt-6">

                {/* Controls row */}
                <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-1 flex-wrap">
                    <button
                        type="button"
                        onClick={() => { setEditItem(null); setModalOpen(true); }}
                        className="w-40 shadow-md/30 text-xs text-white bg-lime-600 hover:bg-lime-700 px-4 py-[5px]"
                    >
                        Skapa nytt förslag
                    </button>

                    <div className="relative w-44 ml-20">
                        <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                        <input
                            type="text"
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            placeholder="Sök"
                            className="w-full text-xs border border-gray-300 rounded-sm pl-7 pr-2 py-1 focus:outline-none bg-white"
                        />
                    </div>

                    {/* Area code switches */}
                    <div className="flex items-center bg-gray-700 rounded-full px-1 py-1 gap-0.5 ml-6">
                        {AREA_CODE_OPTIONS.map((opt) => (
                            <button
                                key={opt.value ?? 'all'}
                                type="button"
                                onClick={() => handleAreaCodeChange(opt.value)}
                                className={`px-3 py-1 text-xs rounded-full transition-colors font-medium ${
                                    selectedAreaCode === opt.value
                                        ? 'bg-amber-400 text-white'
                                        : 'text-gray-300 hover:text-white'
                                }`}
                            >
                                {opt.label}
                            </button>
                        ))}
                    </div>

                    {/* Pagination info + controls */}
                    <div className="ml-auto flex items-center gap-4 text-xs text-gray-600">
                        <span>Rader <strong>{pagination.totalCount}</strong></span>
                        <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber - 1)}
                                disabled={closedLoading || !pagination.hasPreviousPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber + 1)}
                                disabled={closedLoading || !pagination.hasNextPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                        </div>
                    </div>
                </div>

                {/* Closed list heading */}
                {/* <div className="text-xs text-gray-500 text-center mt-3 mb-1">Avslutade förbättringsförslag</div> */}

                <div className="border-t border-gray-300 py-1 mt-4 min-h-0 flex-1 overflow-auto">
                    <table
                        className="table-fixed w-full border-collapse text-xs"
                        style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}
                    >
                        <colgroup>
                            {closedColumns.map((col) => (
                                <col key={col.key} {...(col.width ? { style: { width: col.width } } : {})} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr>
                                {closedColumns.map((col) => (
                                    <th
                                        key={col.key}
                                        onClick={col.sortable ? () => handleSort(col.key) : undefined}
                                        className={`px-2 py-2 text-[10px] font-medium text-gray-500 text-left ${col.sortable ? 'cursor-pointer' : ''}`}
                                    >
                                        <span className="inline-flex items-center gap-1">
                                            {col.label}
                                            {col.sortable ? (
                                                sortConfig.key === col.key ? (
                                                    sortConfig.direction === 'asc'
                                                        ? <ChevronUp className="h-3 w-3" />
                                                        : <ChevronDown className="h-3 w-3" />
                                                ) : (
                                                    <ChevronUp className="h-3 w-3 opacity-0" />
                                                )
                                            ) : null}
                                        </span>
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className={`${!closedLoading && closedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                            {showClosedSkeleton ? (
                                Array.from({ length: 12 }).map((_, i) => (
                                    <tr key={i}>
                                        <td colSpan={closedColumns.length} className="px-2 py-1">
                                            <Skeleton height={16} />
                                        </td>
                                    </tr>
                                ))
                            ) : closedRows.length === 0 ? (
                                <tr>
                                    <td colSpan={closedColumns.length} className="px-4 py-8 text-center text-gray-400">
                                        Inga avslutade förbättringsförslag hittades.
                                    </td>
                                </tr>
                            ) : (
                                closedRows.map((item) => (
                                    <tr
                                        key={item.id}
                                        onClick={() => setSelectedClosedId((prev) => (prev === item.id ? null : item.id))}
                                        className={getClosedRowClass(item)}
                                    >
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(e) => {
                                                    e.stopPropagation();
                                                    handleOpenProposalModal(item);
                                                }}
                                                className="text-blue-600 hover:text-blue-800 hover:underline"
                                            >
                                                {item.id}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">{item.createdByName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{formatDate(item.createdTimestamp)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{item.responsible}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getAreaLabel(item.areaCode)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getStatusLabel(item.statusCode)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.description}>{item.description}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.proposedMeasure}>{item.proposedMeasure}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={item.followUp}>{item.followUp}</td>
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

export default ImprovementPropositionsOverview;
