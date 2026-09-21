import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, ChevronsUpDown, Search, SlidersHorizontal, X } from 'lucide-react';
import ExcelJS from 'exceljs';
import { Link } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import DateRangePicker from '../../../components/DaterangePicker';
import LabeledInput from '../../../components/LabeledInput';
import LabeledSelect from '../../../components/LabeledSelect';
import apiClient from '../../../config/apiClient';
import { formatDateShort, formatDateTime } from '../../../helpers/dateUtils';
import { getSharedRequest } from '../../../helpers/sharedRequest';

const PAGE_SIZE = 100;

const forceHandlingTimeCountAsOptions = [
    { id: '', name: 'Standard' },
    { id: 'DONTCOUNT', name: 'Räkna ej' },
    { id: 'COUNTASNEW', name: 'Räkna som ny' },
    { id: 'COUNTASREPEATE', name: 'Räkna som repeat' },
];

const columns = [
    { key: 'supplierOrderNr', label: 'Best.nr', align: 'left', width: 'w-[6%]', sortable: true, sortField: 'supplierOrderNr' },
    { key: 'customerOrderNr', label: 'Oe.nr', align: 'left', width: 'w-[6%]', sortable: true, sortField: 'customerOrderNr' },
    { key: 'supplierName', label: 'Leverantor', align: 'left', width: 'w-[20%]', sortable: true, sortField: 'supplierName' },
    { key: 'supplierOrderCreatedDate', label: 'Best skapad', align: 'left', width: 'w-[9%]', sortable: true, sortField: 'supplierOrderCreatedDate' },
    { key: 'customerOrderCreatedDate', label: 'Oe skapad', align: 'left', width: 'w-[11%]', sortable: true, sortField: 'customerOrderCreatedDate' },
    { key: 'supplierOrderSentDateTime', label: 'Beställning skickad', align: 'left', width: 'w-[12%]', sortable: true, sortField: 'supplierOrderSentDateTime' },
    { key: 'customerOrderSentDateTime', label: 'Kundorder skickad', align: 'left', width: 'w-[12%]', sortable: true, sortField: 'customerOrderSentDateTime' },
    { key: 'handlingTimeDays', label: 'Hanteringstid dagar', align: 'right', width: 'w-[9%]', sortable: false },
    { key: 'overrideHandlingTimeDays', label: 'Tvinga dagar', align: 'right', width: 'w-[9%]', sortable: false },
    { key: 'forceHandlingTimeCountAs', label: 'Räknas som', align: 'left', width: 'w-[10%]', sortable: false },
    { key: 'actions', label: '', align: 'center', width: 'w-[3%]', sortable: false },
];

const toDateString = (date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

const toNumberOrNull = (value) => {
    if (value === '' || value === null || value === undefined) {
        return null;
    }

    const parsed = Number.parseInt(String(value), 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const formatHandlingTimeDays = (handlingTimeDays) => {
    if (handlingTimeDays === null || handlingTimeDays === undefined) {
        return '';
    }

    return Number(handlingTimeDays).toFixed(1);
};

const formatForceCountAs = (code) => {
    if (!code) return '';

    const normalizedCode = String(code).toUpperCase();
    const matchingOption = forceHandlingTimeCountAsOptions.find(
        (option) => String(option.id).toUpperCase() === normalizedCode
    );

    return matchingOption?.name ?? code;
};

const formatOverrideHandlingTimeDays = (overrideHandlingTimeDays) => {
    if (overrideHandlingTimeDays == null || overrideHandlingTimeDays === '') {
        return '';
    }

    const parsed = Number.parseFloat(String(overrideHandlingTimeDays));
    return Number.isNaN(parsed) ? '' : parsed.toFixed(1);
};

const buildRequestBody = (filters, pagination, sortConfig) => {
    const sortColumn = columns.find((column) => column.key === sortConfig.key);
    const sortField = sortColumn?.sortField;

    return {
        startDate: toDateString(filters.startDate),
        endDate: toDateString(filters.endDate),
        supplierId: toNumberOrNull(filters.supplierId),
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
        orderBy: sortField
            ? [{
                field: sortField,
                direction: sortConfig.direction,
            }]
            : [],
    };
};

const HandlingTimesData = () => {
    const now = useMemo(() => {
        const date = new Date();
        date.setHours(0, 0, 0, 0);
        return date;
    }, []);
    const yearStart = useMemo(() => new Date(now.getFullYear(), 0, 1), [now]);

    const [supplierOptions, setSupplierOptions] = useState([]);
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(false);
    const [isExporting, setIsExporting] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [filterInput, setFilterInput] = useState('');
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);

    const skeletonTimerRef = useRef(null);
    const didMountRef = useRef(false);

    const [filters, setFilters] = useState({
        startDate: yearStart,
        endDate: now,
        supplierId: '',
    });

    const [pagination, setPagination] = useState({
        pageNumber: 1,
        pageSize: PAGE_SIZE,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
    });

    const [sortConfig, setSortConfig] = useState({
        key: 'supplierOrderCreatedDate',
        direction: 'desc',
    });

    const [modalRow, setModalRow] = useState(null);
    const [modalForm, setModalForm] = useState({
        overrideHandlingTimeDays: null,
        forceHandlingTimeCountAs: '',
    });
    const [modalAnchor, setModalAnchor] = useState({ top: 0, left: 0 });
    const [isModalSaving, setIsModalSaving] = useState(false);
    const [modalError, setModalError] = useState(null);
    const popupRef = useRef(null);

    const handleOpenOverrideModal = (event, row) => {
        event.preventDefault();
        event.stopPropagation();

        const popupWidth = 320;
        const popupHeight = 290;
        const viewportPadding = 8;
        const gap = 8;
        const triggerRect = event.currentTarget.getBoundingClientRect();

        let left = triggerRect.right + gap;
        if (left + popupWidth > window.innerWidth - viewportPadding) {
            left = Math.max(viewportPadding, triggerRect.left - popupWidth - gap);
        }

        let top = triggerRect.top - 6;
        if (top + popupHeight > window.innerHeight - viewportPadding) {
            top = Math.max(viewportPadding, window.innerHeight - popupHeight - viewportPadding);
        }

        setModalAnchor({ top, left });
        setModalRow(row);
        setModalForm({
            overrideHandlingTimeDays: row.overrideHandlingTimeDays ?? null,
            forceHandlingTimeCountAs: row.forceHandlingTimeCountAs ?? '',
        });
        setModalError(null);
    };

    const handleCloseModal = () => {
        setModalRow(null);
        setModalError(null);
    };

    useEffect(() => {
        if (!modalRow) {
            return undefined;
        }

        const handlePointerDown = (event) => {
            if (popupRef.current && !popupRef.current.contains(event.target)) {
                handleCloseModal();
            }
        };

        const handleEscape = (event) => {
            if (event.key === 'Escape') {
                handleCloseModal();
            }
        };

        const handleViewportChange = () => {
            handleCloseModal();
        };

        document.addEventListener('mousedown', handlePointerDown);
        document.addEventListener('keydown', handleEscape);
        window.addEventListener('resize', handleViewportChange);
        window.addEventListener('scroll', handleViewportChange, true);

        return () => {
            document.removeEventListener('mousedown', handlePointerDown);
            document.removeEventListener('keydown', handleEscape);
            window.removeEventListener('resize', handleViewportChange);
            window.removeEventListener('scroll', handleViewportChange, true);
        };
    }, [modalRow]);

    const handleSaveOverride = async (event) => {
        event.preventDefault();
        if (!modalRow) return;

        setIsModalSaving(true);
        setModalError(null);

        try {
            const parsedOverride = modalForm.overrideHandlingTimeDays;

            const payload = {
                overrideHandlingTimeDays: Number.isNaN(parsedOverride) ? null : parsedOverride,
                forceHandlingTimeCountAs: modalForm.forceHandlingTimeCountAs || null,
            };

            await apiClient.put(`/supplierorders/${modalRow.supplierOrderId}/handling-time-override`, payload);

            setRows((prev) =>
                prev.map((row) =>
                    row.supplierOrderId === modalRow.supplierOrderId
                        ? {
                            ...row,
                            overrideHandlingTimeDays: payload.overrideHandlingTimeDays,
                            forceHandlingTimeCountAs: payload.forceHandlingTimeCountAs,
                        }
                        : row
                )
            );

            handleCloseModal();
        } catch (error) {
            console.error('Failed to save handling time override:', error);
            setModalError('Kunde inte spara andringarna.');
        } finally {
            setIsModalSaving(false);
        }
    };

    useEffect(() => {
        let isActive = true;

        const loadSuppliers = async () => {
            try {
                const response = await getSharedRequest(
                    'reporting:handling-times:supplier-options',
                    () => apiClient.get('/reporting/handling-times/suppliers')
                );

                if (!isActive) return;
                setSupplierOptions(response?.data ?? []);
            } catch (error) {
                console.error('Failed to load handling times supplier options:', error);
                if (!isActive) return;
                setSupplierOptions([]);
            }
        };

        loadSuppliers();

        return () => {
            isActive = false;
        };
    }, []);

    const loadRows = async (isActive = true) => {
        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const requestBody = buildRequestBody(filters, pagination, sortConfig);
            const requestKey = `reporting:handling-times:data:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(
                requestKey,
                () => apiClient.post('/reporting/handling-times/data', requestBody)
            );

            if (!isActive) return;

            const data = response?.data ?? {};
            setRows(data?.items ?? []);
            setPagination((prev) => ({
                ...prev,
                pageNumber: data?.pageNumber ?? prev.pageNumber,
                pageSize: data?.pageSize ?? prev.pageSize,
                totalCount: data?.totalCount ?? 0,
                totalPages: data?.totalPages ?? 0,
                hasPreviousPage: Boolean(data?.hasPreviousPage),
                hasNextPage: Boolean(data?.hasNextPage),
            }));
        } catch (error) {
            console.error('Failed to load handling times data:', error);
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
            if (!isActive) return;
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
            setInitialLoadCompleted(true);
        }
    };

    useEffect(() => {
        let isActive = true;

        const timer = setTimeout(async () => {
            await loadRows(isActive);
            if (!didMountRef.current) {
                didMountRef.current = true;
            }
        }, didMountRef.current ? 250 : 0);

        return () => {
            isActive = false;
            clearTimeout(timer);
            clearTimeout(skeletonTimerRef.current);
        };
    }, [filters, pagination.pageNumber, sortConfig]);

    const filteredSupplierOptions = useMemo(() => {
        const term = filterInput.trim().toLowerCase();
        if (!term) return supplierOptions;

        return supplierOptions.filter((supplier) =>
            String(supplier?.name ?? '').toLowerCase().includes(term)
        );
    }, [supplierOptions, filterInput]);

    const onPageChange = (nextPage) => {
        if (nextPage < 1 || (pagination.totalPages > 0 && nextPage > pagination.totalPages)) {
            return;
        }

        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const handleSort = (key) => {
        const column = columns.find((item) => item.key === key);
        if (!column?.sortable) return;

        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const renderSortIcon = (column) => {
        if (!column.sortable) return null;
        if (sortConfig.key !== column.key) return <ChevronsUpDown className="h-3.5 w-3.5 text-gray-300" />;

        return sortConfig.direction === 'asc'
            ? <ChevronUp className="h-3.5 w-3.5 text-gray-500" />
            : <ChevronDown className="h-3.5 w-3.5 text-gray-500" />;
    };

    const handleExportExcel = async () => {
        if (loading || isExporting || pagination.totalCount === 0) {
            return;
        }

        setIsExporting(true);

        try {
            const workbook = new ExcelJS.Workbook();
            const worksheet = workbook.addWorksheet('HandlingTimesData');

            const exportColumns = columns.filter((column) => column.key !== 'actions');
            worksheet.addRow(exportColumns.map((column) => column.label));
            worksheet.getRow(1).font = { bold: true, size: 9 };

            let currentPage = 1;
            let totalPages = 1;

            while (currentPage <= totalPages) {
                const requestBody = buildRequestBody(
                    filters,
                    { ...pagination, pageNumber: currentPage, pageSize: 200 },
                    sortConfig
                );

                const response = await apiClient.post('/reporting/handling-times/data', requestBody);
                const data = response?.data ?? {};
                const items = data?.items ?? [];

                totalPages = Number(data?.totalPages) || 1;

                items.forEach((row) => {
                    worksheet.addRow([
                        row.supplierOrderNr || row.supplierOrderId || '',
                        row.customerOrderNr || row.customerOrderId || '',
                        row.supplierName ?? '',
                        formatDateShort(row.supplierOrderCreatedDate),
                        formatDateShort(row.customerOrderCreatedDate),
                        formatDateTime(row.supplierOrderSentDateTime),
                        formatDateTime(row.customerOrderSentDateTime),
                        formatHandlingTimeDays(row.handlingTimeDays),
                        formatOverrideHandlingTimeDays(row.overrideHandlingTimeDays),
                        formatForceCountAs(row.forceHandlingTimeCountAs),
                    ]);
                });

                currentPage += 1;
            }

            for (let col = 1; col <= exportColumns.length; col += 1) {
                worksheet.getColumn(col).width = 20;
            }

            const buffer = await workbook.xlsx.writeBuffer();
            const blob = new Blob([buffer], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            });

            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `handlingstider-data-${toDateString(new Date())}.xlsx`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export handling times data:', error);
        } finally {
            setIsExporting(false);
        }
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className={`relative z-20 flex items-center gap-4 overflow-visible whitespace-nowrap pb-2 mt-2 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <DateRangePicker
                    placeholder="Valj period"
                    presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                    initialPresetKey="year-to-date"
                    onApply={({ startDate, endDate }) => {
                        setFilters((prev) => ({ ...prev, startDate, endDate }));
                        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                    }}
                    triggerRadius="full"
                    triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                    openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                    closedTriggerClassName="border-lime-600 hover:border-lime-700"
                    widthClassName="w-60"
                />

                {/* <div className="relative w-40 shrink-0">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={filterInput}
                        onChange={(event) => setFilterInput(event.target.value ?? '')}
                        placeholder="Sok leverantor"
                        className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                    />
                </div> */}

                <div className="relative w-64 shrink-0 mr-10">
                    <select
                        value={filters.supplierId}
                        onChange={(event) => {
                            setFilters((prev) => ({ ...prev, supplierId: event.target.value }));
                            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                        }}
                        className="h-7 w-full rounded-full border border-lime-600 bg-white px-3 text-xs text-gray-700 outline-none transition focus:border-lime-700"
                    >
                        <option value="">Alla leverantorer</option>
                        {filteredSupplierOptions.map((supplier) => (
                            <option key={supplier.id} value={String(supplier.id)}>
                                {supplier.name}
                            </option>
                        ))}
                    </select>
                </div>

                <div className="text-xs text-center">
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (loading || isExporting || pagination.totalCount === 0) return;
                            handleExportExcel();
                        }}
                        aria-disabled={loading || isExporting || pagination.totalCount === 0}
                        className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || isExporting || pagination.totalCount === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        {isExporting ? 'Exporterar EXCEL...' : 'Exportera till EXCEL'}
                    </a>
                </div>

                <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    {initialLoadCompleted && (
                        <>
                            <span>Rader <strong>{pagination.totalCount}</strong></span>
                            <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                            <div className="flex gap-1">
                                <button
                                    type="button"
                                    onClick={() => onPageChange(pagination.pageNumber - 1)}
                                    disabled={loading || !pagination.hasPreviousPage}
                                    className="disabled:cursor-not-allowed disabled:opacity-40"
                                >
                                    <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                                </button>
                                <button
                                    type="button"
                                    onClick={() => onPageChange(pagination.pageNumber + 1)}
                                    disabled={loading || !pagination.hasNextPage}
                                    className="disabled:cursor-not-allowed disabled:opacity-40"
                                >
                                    <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                                </button>
                            </div>
                        </>
                    )}
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} style={{ width: column.width }} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 ${column.align === 'right' ? 'text-right' : column.align === 'center' ? 'text-center' : 'text-left'}`}
                                >
                                    {column.sortable ? (
                                        <button
                                            type="button"
                                            onClick={() => handleSort(column.key)}
                                            className={`inline-flex items-center gap-1 ${column.align === 'right' ? 'ml-auto' : ''} hover:text-gray-600`}
                                        >
                                            <span>{column.label}</span>
                                            {renderSortIcon(column)}
                                        </button>
                                    ) : (
                                        column.label
                                    )}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index}>
                                    {columns.map((column) => (
                                        <td key={column.key} className="px-2 py-1">
                                            <Skeleton height={16} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 && !loading && initialLoadCompleted ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400 text-xs">
                                    Inga leverantorsorder hittades for valt urval.
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={`${row.supplierOrderId}-${row.customerOrderId ?? 'none'}`} className="h-6 border-b border-gray-100 hover:bg-lime-200/70">
                                    <td className="truncate px-2 py-0 text-gray-800">
                                        <Link
                                            to={`/order/supplierorders/${row.supplierOrderId}`}
                                            className="text-slate-700 hover:text-slate-900 hover:underline"
                                        >
                                            {row.supplierOrderNr || row.supplierOrderId}
                                        </Link>
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800">
                                        {row.customerOrderId ? (
                                            <Link
                                                to={`/order/customerorders/${row.customerOrderId}`}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.customerOrderNr || row.customerOrderId}
                                            </Link>
                                        ) : ''}
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.supplierName}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDateShort(row.supplierOrderCreatedDate)}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDateShort(row.customerOrderCreatedDate)}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDateTime(row.customerOrderSentDateTime)}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDateTime(row.supplierOrderSentDateTime)}</td>
                                    <td className="truncate px-2 py-0 text-right text-gray-800">
                                        {formatHandlingTimeDays(row.handlingTimeDays)}
                                    </td>
                                    <td className="truncate px-2 py-0 text-right text-gray-800">
                                        {formatOverrideHandlingTimeDays(row.overrideHandlingTimeDays)}
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800 font-mono text-[11px]">
                                        {formatForceCountAs(row.forceHandlingTimeCountAs)}
                                    </td>
                                    <td className="px-2 py-0 text-center">
                                        <button
                                            type="button"
                                            onClick={(event) => handleOpenOverrideModal(event, row)}
                                            className="inline-flex items-center justify-center text-gray-400 hover:text-slate-700 transition"
                                            title="Andra hanteringstid & kod"
                                        >
                                            <SlidersHorizontal className="h-3.5 w-3.5" />
                                        </button>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {modalRow && (
                <div
                    ref={popupRef}
                    className="fixed z-50 w-80 rounded-lg border border-stone-300 bg-stone-100 p-3 shadow-xl"
                    style={{ top: `${modalAnchor.top}px`, left: `${modalAnchor.left}px` }}
                >
                    {/* <div className="absolute -right-1.5 top-4 h-3 w-3 rotate-135 border-b border-l border-gray-200 bg-white" /> */}

                    <div className="flex items-center justify-between py-2">
                        <h3 className="truncate pr-2 text-xs font-semibold text-gray-800 mx-auto">
                            Hanteringstid {modalRow.supplierOrderNr || modalRow.supplierOrderId}
                        </h3>
                        {/* <button
                            type="button"
                            onClick={handleCloseModal}
                            className="text-gray-400 hover:text-gray-600"
                        >
                            <X className="h-3.5 w-3.5" />
                        </button> */}
                    </div>

                    <form onSubmit={handleSaveOverride} className="space-y-3 p-3">
                        <LabeledSelect
                            label="Tvinga räknas som"
                            labelWidth="w-28"
                            inputWidth="w-full"
                            margintop="0"
                            name="forceHandlingTimeCountAs"
                            value={modalForm.forceHandlingTimeCountAs}
                            items={forceHandlingTimeCountAsOptions}
                            onChange={(value) => setModalForm((prev) => ({ ...prev, forceHandlingTimeCountAs: value }))}
                        />

                        <LabeledInput
                            label="Tvinga dagar"
                            labelWidth="w-28"
                            inputWidth="w-full"
                            margintop="0"
                            name="overrideHandlingTimeDays"
                            type="number"
                            value={modalForm.overrideHandlingTimeDays}
                            onChange={(value) => setModalForm((prev) => ({ ...prev, overrideHandlingTimeDays: value }))}
                            step="0.1"
                            placeholder="t.ex. 2,0"
                        />

                        {modalError && (
                            <p className="text-xs text-red-600">{modalError}</p>
                        )}

                        <div className="flex justify-end gap-2 border-t border-gray-100 pt-2">
                            <button
                                type="button"
                                onClick={handleCloseModal}
                                className="rounded border border-gray-300 px-2.5 py-1 text-xs text-gray-600 hover:text-gray-800"
                            >
                                Avbryt
                            </button>
                            <button
                                type="submit"
                                disabled={isModalSaving}
                                className="rounded bg-lime-700 px-3 py-1 text-xs text-white hover:bg-lime-800 disabled:opacity-50"
                            >
                                {isModalSaving ? 'Sparar...' : 'Spara'}
                            </button>
                        </div>
                    </form>
                </div>
            )}
        </div>
    );
};

export default HandlingTimesData;
