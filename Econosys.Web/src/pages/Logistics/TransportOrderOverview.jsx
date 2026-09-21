import React, { useEffect, useMemo, useRef, useState } from 'react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import { Link } from 'react-router-dom';

import apiClient from '../../config/apiClient';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import ReportOrderInfoModal from '../../modals/ReportOrderInfoModal';
import { getSharedRequest } from '../../helpers/sharedRequest';
import bg from '../../assets/content.png';
import './TransportOrderOverview.css';

const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };
const HEADER_BACKGROUND_STYLE = { backgroundImage: `url(${bg})` };

const transportOrderOverviewCache = {
    ui: null,
    data: null,
};

const topColumns = [
    { key: 'customerOrderNr', label: 'Nr', width: '5%', align: 'left', filterable: true },
    { key: 'supplierName', label: 'Leverantör', width: '10%', align: 'left', filterable: true },
    { key: 'supplierCity', label: 'Ort', width: '7%', align: 'left' },
    { key: 'customerName', label: 'Kund', width: '9%', align: 'left', filterable: true },
    { key: 'customerCity', label: 'Ort', width: '11%', align: 'left' },
    { key: 'productName', label: 'Produkt', width: '16%', align: 'left', filterable: true },
    { key: 'deliveryTime', label: 'Lev.tid', width: '7%', align: 'left', filterable: true },
    { key: 'confirmedDeliveryTime', label: 'Bekräftad', width: '7%', align: 'left', filterable: true },
    { key: 'editionOrdered', label: 'Best.', width: '4%', align: 'right' },
    { key: 'editionProduced', label: 'Prod.', width: '4%', align: 'right' },
    { key: 'editionCustomerOrder', label: 'OE', width: '4%', align: 'right' },
    { key: 'rest', label: 'Rest', width: '4%', align: 'right' },
    { key: 'pallets', label: 'PALL', width: '4%', align: 'right' },
    { key: 'palletFormat', label: 'Pallformat', width: '10%', align: 'left' },
    { key: 'rapp', label: 'Rapp.', width: '4%', align: 'left' },
    { key: 'planera', label: 'Plamnera', width: '4%', align: 'left' },
];

const planningColumns = [
    { key: 'transportOrderNr', label: 'Nr', width: '10%' },
    { key: 'transporterName', label: 'Transportör', width: '24%' },
    { key: 'loadingCities', label: 'Lastas', width: '29%' },
    { key: 'unloadingCities', label: 'Lossas', width: '29%' },
    { key: 'filledInfo', label: 'Fylld', width: '7%' },
];

const activeColumns = [
    { key: 'transportOrderNr', label: 'Nr', width: '10%' },
    { key: 'transporterName', label: 'Transportör', width: '24%' },
    { key: 'loadingCities', label: 'Lastas', width: '29%' },
    { key: 'unloadingCities', label: 'Lossas', width: '29%' },
    { key: 'markUnloaded', label: 'Lossad', width: '7%' },
];

const reportBackColumns = [
    { key: 'transportOrderNr', label: 'Nr', width: '10%' },
    { key: 'transporterName', label: 'Transportör', width: '65%' },
    { key: 'hasNotAttestedTransportInvoices', label: 'Att.', width: '10%' },
    { key: 'reportCost', label: 'Kostn.', width: '15%' },
];

function formatNumber(value, digits = 0) {
    if (value === null || value === undefined || Number.isNaN(Number(value))) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
    }).format(Number(value));
}

function normalizeFilterValue(value) {
    return String(value ?? '').trim().toLowerCase();
}

function parseDateInputToken(token, now = new Date()) {
    const normalized = String(token ?? '').trim();
    if (!normalized) return null;

    const isoMatch = normalized.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
    if (isoMatch) {
        const year = Number(isoMatch[1]);
        const month = Number(isoMatch[2]);
        const day = Number(isoMatch[3]);
        const date = new Date(year, month - 1, day);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    const monthDayMatch = normalized.match(/^(\d{1,2})[\/-](\d{1,2})$/);
    if (monthDayMatch) {
        const month = Number(monthDayMatch[1]);
        const day = Number(monthDayMatch[2]);
        const date = new Date(now.getFullYear(), month - 1, day);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    const dayMatch = normalized.match(/^(\d{1,2})$/);
    if (dayMatch) {
        const day = Number(dayMatch[1]);
        const date = new Date(now.getFullYear(), now.getMonth(), day);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    return null;
}

function parseDateFromCellValue(value) {
    const source = String(value ?? '').trim();
    if (!source) return null;

    const isoMatch = source.match(/(\d{4})-(\d{2})-(\d{2})/);
    if (isoMatch) {
        const date = new Date(Number(isoMatch[1]), Number(isoMatch[2]) - 1, Number(isoMatch[3]));
        return Number.isNaN(date.getTime()) ? null : date;
    }

    const svMatch = source.match(/(\d{1,2})[\/-](\d{1,2})[\/-](\d{4})/);
    if (svMatch) {
        const date = new Date(Number(svMatch[3]), Number(svMatch[2]) - 1, Number(svMatch[1]));
        return Number.isNaN(date.getTime()) ? null : date;
    }

    return null;
}

function isDateWithinRange(date, start, end) {
    if (!date) return false;
    if (start && date < start) return false;
    if (end && date > end) return false;
    return true;
}

function matchesSmartDeliveryTimeFilter(cellValue, filterValue) {
    const normalizedFilter = String(filterValue ?? '').trim();
    if (!normalizedFilter) return true;

    const date = parseDateFromCellValue(cellValue);
    if (!date) {
        return normalizeFilterValue(cellValue).includes(normalizeFilterValue(normalizedFilter));
    }

    const [rawStart, rawEnd] = normalizedFilter.split('+');

    if (normalizedFilter.includes('+')) {
        const start = parseDateInputToken(rawStart);
        const end = parseDateInputToken(rawEnd);
        if (!start && !end) return false;
        return isDateWithinRange(date, start, end);
    }

    const exactDate = parseDateInputToken(normalizedFilter);
    if (!exactDate) {
        return normalizeFilterValue(cellValue).includes(normalizeFilterValue(normalizedFilter));
    }

    return date.toDateString() === exactDate.toDateString();
}

const SmallStyledCheckbox = ({ checked }) => {
    const indicatorStyle = checked
        ? {
            backgroundColor: '#14b8a6',
            borderColor: '#14b8a6',
            color: '#ffffff',
        }
        : {
            backgroundColor: '#ffffff',
            borderColor: '#aaaaaa',
            color: '#4b5563',
        };

    return (
        <span className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-[#e5e7eb] bg-white p-[1px]">
            <span className="inline-flex h-full w-full items-center justify-center rounded-full border transition-all" style={indicatorStyle}>
                <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-2.5 w-2.5 transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}>
                    <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </span>
        </span>
    );
};

const MultiSelectCheckboxFilter = ({ options, selectedValues, onChange }) => {
    const [open, setOpen] = useState(false);
    const rootRef = useRef(null);

    useEffect(() => {
        const handleClickOutside = (event) => {
            if (rootRef.current && !rootRef.current.contains(event.target)) {
                setOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => {
            document.removeEventListener('mousedown', handleClickOutside);
        };
    }, []);

    const allSelected = options.length > 0 && selectedValues.length === options.length;

    const label = allSelected
        ? 'Välj alla'
        : selectedValues.length > 0
            ? `${selectedValues.length} valda`
            : '';

    const toggleAll = () => {
        onChange(allSelected ? [] : options);
    };

    const toggleOption = (option) => {
        if (selectedValues.includes(option)) {
            onChange(selectedValues.filter((value) => value !== option));
            return;
        }

        onChange([...selectedValues, option]);
    };

    return (
        <div className="relative" ref={rootRef}>
            <button
                type="button"
                onClick={() => setOpen((prev) => !prev)}
                className="flex h-5 w-full items-center justify-between rounded-sm border border-gray-300 bg-white px-1.5 text-left text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none"
                title={label || 'Välj'}
            >
                <span className={`truncate ${label ? 'text-gray-700' : 'text-gray-400'}`}>{label || 'Välj'}</span>
                <span className="text-tiny text-gray-500">▾</span>
            </button>

            {open ? (
                <div className="absolute left-0 top-[26px] z-50 max-h-72 w-[420px] overflow-y-auto rounded-sm border border-gray-300 bg-white p-1.5 shadow-lg">
                    <label
                        className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-1 text-tiny font-normal text-gray-700 hover:bg-gray-50"
                        onClick={toggleAll}
                    >
                        <SmallStyledCheckbox checked={allSelected} />
                        <span>Välj alla</span>
                    </label>

                    {options.map((option) => (
                        <label
                            key={option}
                            className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-0.5 text-tiny font-normal text-gray-700 hover:bg-gray-50"
                            onClick={() => toggleOption(option)}
                        >
                            <SmallStyledCheckbox checked={selectedValues.includes(option)} />
                            <span className="truncate">{option}</span>
                        </label>
                    ))}
                </div>
            ) : null}
        </div>
    );
};

const TransportOrderOverview = () => {
    const cachedUi = transportOrderOverviewCache.ui;
    const cachedData = transportOrderOverviewCache.data;
    const topScrollRef = useRef(null);

    const [rows, setRows] = useState(() => cachedData?.rows ?? []);
    const [loading, setLoading] = useState(() => !cachedData?.hasSnapshot);
    const [hasSnapshot, setHasSnapshot] = useState(() => Boolean(cachedData?.hasSnapshot));
    const [planningRows, setPlanningRows] = useState(() => cachedData?.planningRows ?? []);
    const [planningLoading, setPlanningLoading] = useState(() => !cachedData?.hasPlanningSnapshot);
    const [hasPlanningSnapshot, setHasPlanningSnapshot] = useState(() => Boolean(cachedData?.hasPlanningSnapshot));
    const [activeRows, setActiveRows] = useState(() => cachedData?.activeRows ?? []);
    const [activeLoading, setActiveLoading] = useState(() => !cachedData?.hasActiveSnapshot);
    const [hasActiveSnapshot, setHasActiveSnapshot] = useState(() => Boolean(cachedData?.hasActiveSnapshot));
    const [reportBackRows, setReportBackRows] = useState(() => cachedData?.reportBackRows ?? []);
    const [reportBackLoading, setReportBackLoading] = useState(() => !cachedData?.hasReportBackSnapshot);
    const [hasReportBackSnapshot, setHasReportBackSnapshot] = useState(() => Boolean(cachedData?.hasReportBackSnapshot));
    const [selectedCustomerOrderId, setSelectedCustomerOrderId] = useState(() => cachedUi?.selectedCustomerOrderId ?? null);
    const [selectedPlanningTransportOrderId, setSelectedPlanningTransportOrderId] = useState(() => cachedUi?.selectedPlanningTransportOrderId ?? null);
    const [selectedActiveTransportOrderId, setSelectedActiveTransportOrderId] = useState(() => cachedUi?.selectedActiveTransportOrderId ?? null);
    const [selectedReportBackTransportOrderId, setSelectedReportBackTransportOrderId] = useState(() => cachedUi?.selectedReportBackTransportOrderId ?? null);

    const [includePlanned, setIncludePlanned] = useState(() => cachedUi?.includePlanned ?? true);
    const [includeNotPlanned, setIncludeNotPlanned] = useState(() => cachedUi?.includeNotPlanned ?? true);
    const [supplierFilter, setSupplierFilter] = useState(() => cachedUi?.supplierFilter ?? []);
    const [customerFilter, setCustomerFilter] = useState(() => cachedUi?.customerFilter ?? []);
    const [deliveryTimeFilter, setDeliveryTimeFilter] = useState(() => cachedUi?.deliveryTimeFilter ?? '');
    const [confirmedDeliveryTimeFilter, setConfirmedDeliveryTimeFilter] = useState(() => cachedUi?.confirmedDeliveryTimeFilter ?? '');
    const [activeDateHelpColumn, setActiveDateHelpColumn] = useState(null);
    const [reportOrderInfo, setReportOrderInfo] = useState(null);
    const [reportOrderInfoLoading, setReportOrderInfoLoading] = useState(false);
    const [reportOrderInfoSaving, setReportOrderInfoSaving] = useState(false);
    const [reportOrderInfoError, setReportOrderInfoError] = useState('');
    const [topTableRefreshToken, setTopTableRefreshToken] = useState(0);
    const [columnFilters, setColumnFilters] = useState(() => {
        if (cachedUi?.columnFilters && typeof cachedUi.columnFilters === 'object' && !Array.isArray(cachedUi.columnFilters)) {
            return { ...cachedUi.columnFilters };
        }

        return Object.fromEntries(
            topColumns
                .filter((column) => column.filterable)
                .map((column) => [column.key, ''])
        );
    });

    const supplierOptions = useMemo(() => {
        const uniqueNames = new Set(
            rows
                .map((row) => String(row?.supplierName ?? '').trim())
                .filter((value) => value.length > 0)
        );

        return Array.from(uniqueNames).sort((a, b) => a.localeCompare(b, 'sv-SE'));
    }, [rows]);

    const customerOptions = useMemo(() => {
        const uniqueNames = new Set(
            rows
                .map((row) => String(row?.customerName ?? '').trim())
                .filter((value) => value.length > 0)
        );

        return Array.from(uniqueNames).sort((a, b) => a.localeCompare(b, 'sv-SE'));
    }, [rows]);

    const supplierFilterInitializedRef = useRef(Array.isArray(cachedUi?.supplierFilter));
    const customerFilterInitializedRef = useRef(Array.isArray(cachedUi?.customerFilter));

    useEffect(() => {
        if (!topScrollRef.current) {
            return;
        }

        topScrollRef.current.scrollTop = cachedUi?.topScrollTop ?? 0;
    }, []);

    useEffect(() => {
        transportOrderOverviewCache.ui = {
            includePlanned,
            includeNotPlanned,
            supplierFilter: [...supplierFilter],
            customerFilter: [...customerFilter],
            deliveryTimeFilter,
            confirmedDeliveryTimeFilter,
            columnFilters: { ...columnFilters },
            selectedCustomerOrderId,
            selectedPlanningTransportOrderId,
            selectedActiveTransportOrderId,
            selectedReportBackTransportOrderId,
            topScrollTop: topScrollRef.current?.scrollTop ?? transportOrderOverviewCache.ui?.topScrollTop ?? 0,
        };
    }, [
        includePlanned,
        includeNotPlanned,
        supplierFilter,
        customerFilter,
        deliveryTimeFilter,
        confirmedDeliveryTimeFilter,
        columnFilters,
        selectedCustomerOrderId,
        selectedPlanningTransportOrderId,
        selectedActiveTransportOrderId,
        selectedReportBackTransportOrderId,
    ]);

    useEffect(() => {
        transportOrderOverviewCache.data = {
            rows,
            hasSnapshot,
            planningRows,
            hasPlanningSnapshot,
            activeRows,
            hasActiveSnapshot,
            reportBackRows,
            hasReportBackSnapshot,
        };
    }, [
        rows,
        hasSnapshot,
        planningRows,
        hasPlanningSnapshot,
        activeRows,
        hasActiveSnapshot,
        reportBackRows,
        hasReportBackSnapshot,
    ]);

    useEffect(() => {
        if (!supplierFilterInitializedRef.current) {
            setSupplierFilter(supplierOptions);
            supplierFilterInitializedRef.current = true;
            return;
        }

        setSupplierFilter((previous) => previous.filter((value) => supplierOptions.includes(value)));
    }, [supplierOptions]);

    useEffect(() => {
        if (!customerFilterInitializedRef.current) {
            setCustomerFilter(customerOptions);
            customerFilterInitializedRef.current = true;
            return;
        }

        setCustomerFilter((previous) => previous.filter((value) => customerOptions.includes(value)));
    }, [customerOptions]);

    const handleColumnFilterChange = (columnKey, value) => {
        setColumnFilters((previousFilters) => ({
            ...previousFilters,
            [columnKey]: value,
        }));
    };

    useEffect(() => {
        let isActive = true;

        const fetchRows = async () => {
            if (!hasSnapshot) {
                setLoading(true);
            }

            const requestKey = `logistics:transportorderoverview:${includePlanned}:${includeNotPlanned}:${topTableRefreshToken}`;

            try {
                const response = await getSharedRequest(requestKey, () => apiClient.get('/logistics/transportorderoverview/top-table', {
                    params: {
                        includePlanned,
                        includeNotPlanned,
                    },
                }));

                if (!isActive) return;

                setRows(response?.data ?? []);
            } catch (error) {
                console.error('Failed to load transport order overview top table:', error);
                if (!isActive) return;
                if (!hasSnapshot) {
                    setRows([]);
                }
            } finally {
                if (isActive) {
                    setHasSnapshot(true);
                    setLoading(false);
                }
            }
        };

        const timer = setTimeout(fetchRows, 250);
        return () => {
            isActive = false;
            clearTimeout(timer);
        };
    }, [includePlanned, includeNotPlanned, topTableRefreshToken]);

    useEffect(() => {
        let isActive = true;

        const fetchPlanningRows = async () => {
            if (!hasPlanningSnapshot) {
                setPlanningLoading(true);
            }

            try {
                const response = await getSharedRequest(
                    'logistics:transportorderoverview:planning-table',
                    () => apiClient.get('/logistics/transportorderoverview/planning-table')
                );

                if (!isActive) return;
                setPlanningRows(response?.data ?? []);
            } catch (error) {
                console.error('Failed to load transport order planning table:', error);
                if (!isActive) return;
                if (!hasPlanningSnapshot) {
                    setPlanningRows([]);
                }
            } finally {
                if (isActive) {
                    setHasPlanningSnapshot(true);
                    setPlanningLoading(false);
                }
            }
        };

        fetchPlanningRows();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const fetchActiveRows = async () => {
            if (!hasActiveSnapshot) {
                setActiveLoading(true);
            }

            try {
                const response = await getSharedRequest(
                    'logistics:transportorderoverview:active-table',
                    () => apiClient.get('/logistics/transportorderoverview/active-table')
                );

                if (!isActive) return;
                setActiveRows(response?.data ?? []);
            } catch (error) {
                console.error('Failed to load active transport order table:', error);
                if (!isActive) return;
                if (!hasActiveSnapshot) {
                    setActiveRows([]);
                }
            } finally {
                if (isActive) {
                    setHasActiveSnapshot(true);
                    setActiveLoading(false);
                }
            }
        };

        fetchActiveRows();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const fetchReportBackRows = async () => {
            if (!hasReportBackSnapshot) {
                setReportBackLoading(true);
            }

            try {
                const response = await getSharedRequest(
                    'logistics:transportorderoverview:report-back-table',
                    () => apiClient.get('/logistics/transportorderoverview/report-back-table')
                );

                if (!isActive) return;
                setReportBackRows(response?.data ?? []);
            } catch (error) {
                console.error('Failed to load transport order report-back table:', error);
                if (!isActive) return;
                if (!hasReportBackSnapshot) {
                    setReportBackRows([]);
                }
            } finally {
                if (isActive) {
                    setHasReportBackSnapshot(true);
                    setReportBackLoading(false);
                }
            }
        };

        fetchReportBackRows();

        return () => {
            isActive = false;
        };
    }, []);

    const showSkeleton = loading && !hasSnapshot;

    const filteredRows = useMemo(() => {
        const supplierFilterSet = new Set(supplierFilter);
        const customerFilterSet = new Set(customerFilter);

        return rows.filter((row) => {
            if (supplierFilterSet.size > 0 && !supplierFilterSet.has(String(row?.supplierName ?? '').trim())) {
                return false;
            }

            if (customerFilterSet.size > 0 && !customerFilterSet.has(String(row?.customerName ?? '').trim())) {
                return false;
            }

            if (!matchesSmartDeliveryTimeFilter(row?.deliveryTime, deliveryTimeFilter)) {
                return false;
            }

            if (!matchesSmartDeliveryTimeFilter(row?.confirmedDeliveryTime, confirmedDeliveryTimeFilter)) {
                return false;
            }

            return Object.entries(columnFilters).every(([columnKey, filterValue]) => {
                if (columnKey === 'supplierName' || columnKey === 'customerName' || columnKey === 'deliveryTime' || columnKey === 'confirmedDeliveryTime') {
                    return true;
                }

                const normalizedColumnFilter = normalizeFilterValue(filterValue);
                if (!normalizedColumnFilter) {
                    return true;
                }

                return normalizeFilterValue(row?.[columnKey]).includes(normalizedColumnFilter);
            });
        });
    }, [rows, columnFilters, supplierFilter, customerFilter, deliveryTimeFilter, confirmedDeliveryTimeFilter]);

    useEffect(() => {
        if (selectedCustomerOrderId === null) return;

        const selectedStillVisible = filteredRows.some((row) => row.customerOrderId === selectedCustomerOrderId);
        if (!selectedStillVisible) {
            setSelectedCustomerOrderId(null);
        }
    }, [filteredRows, selectedCustomerOrderId]);

    useEffect(() => {
        if (selectedPlanningTransportOrderId === null) return;

        const selectedStillVisible = planningRows.some((row) => row.id === selectedPlanningTransportOrderId);
        if (!selectedStillVisible) {
            setSelectedPlanningTransportOrderId(null);
        }
    }, [planningRows, selectedPlanningTransportOrderId]);

    useEffect(() => {
        if (selectedActiveTransportOrderId === null) return;

        const selectedStillVisible = activeRows.some((row) => row.id === selectedActiveTransportOrderId);
        if (!selectedStillVisible) {
            setSelectedActiveTransportOrderId(null);
        }
    }, [activeRows, selectedActiveTransportOrderId]);

    useEffect(() => {
        if (selectedReportBackTransportOrderId === null) return;

        const selectedStillVisible = reportBackRows.some((row) => row.id === selectedReportBackTransportOrderId);
        if (!selectedStillVisible) {
            setSelectedReportBackTransportOrderId(null);
        }
    }, [reportBackRows, selectedReportBackTransportOrderId]);

    const summaryText = useMemo(() => `Rader ${filteredRows.length}`, [filteredRows.length]);

    const openReportOrderInfo = async (customerOrderId) => {
        setReportOrderInfoError('');
        setReportOrderInfoLoading(true);
        setReportOrderInfo({ customerOrderId, orderedEdition: null, producedEdition: null, isCompleted: false });

        try {
            const response = await apiClient.get(`/logistics/transportorderoverview/order-info/${customerOrderId}`);
            setReportOrderInfo({
                customerOrderId,
                orderedEdition: response?.data?.orderedEdition ?? null,
                producedEdition: response?.data?.producedEdition ?? null,
                isCompleted: Boolean(response?.data?.isCompleted),
            });
        } catch (error) {
            console.error('Failed to load order info:', error);
            setReportOrderInfoError('Kunde inte hämta orderinformation.');
        } finally {
            setReportOrderInfoLoading(false);
        }
    };

    const handleSaveReportOrderInfo = async ({ producedEdition, isCompleted }) => {
        if (!reportOrderInfo) return;

        setReportOrderInfoSaving(true);
        setReportOrderInfoError('');

        try {
            await apiClient.put(`/logistics/transportorderoverview/order-info/${reportOrderInfo.customerOrderId}`, {
                producedEdition,
                isCompleted,
            });

            setReportOrderInfo(null);
            setTopTableRefreshToken((previous) => previous + 1);
        } catch (error) {
            console.error('Failed to save order info:', error);
            setReportOrderInfoError('Kunde inte spara orderinformation.');
        } finally {
            setReportOrderInfoSaving(false);
        }
    };

    const showPlanningSkeleton = planningLoading && !hasPlanningSnapshot;
    const showActiveSkeleton = activeLoading && !hasActiveSnapshot;
    const showReportBackSkeleton = reportBackLoading && !hasReportBackSnapshot;

    return (
        <div className="grid grid-rows-[520px_auto] gap-6 pt-1 pb-4 ps-5 pe-10">
            <section className="flex min-h-0 flex-col">
                <div className="mb-1 flex flex-wrap justify-between items-center gap-4 text-xs text-gray-700">
                    <div className="w-100 flex items-center gap-4">
                        <LabeledCheckbox
                            label="Planerade"
                            name="includePlanned"
                            checked={includePlanned}
                            onChange={setIncludePlanned}
                            disabled={loading}
                            className="shrink-0"
                        />

                        <LabeledCheckbox
                            label="Ej planerade"
                            name="includeNotPlanned"
                            checked={includeNotPlanned}
                            onChange={setIncludeNotPlanned}
                            disabled={loading}
                            className="shrink-0"
                        />
                    </div>

                    <div className="px-4 text-xs text-gray-500 text-center">Ordererkännanden som skall planeras</div>

                    <div className="w-100 flex items-center gap-2">
                        <span className="ml-auto text-xs text-gray-600">{summaryText}</span>
                    </div>
                </div>

                <div
                    ref={topScrollRef}
                    onScroll={(event) => {
                        transportOrderOverviewCache.ui = {
                            ...(transportOrderOverviewCache.ui ?? {}),
                            topScrollTop: event.currentTarget.scrollTop,
                        };
                    }}
                    className="transport-overview-top-scroll min-h-0 flex-1 overflow-auto border-t border-gray-300 pt-0"
                    style={HEADER_BACKGROUND_STYLE}
                >
                    <table className="table-fixed w-full border-separate border-spacing-0 text-xs" style={TABLE_FONT_STYLE}>
                        <colgroup>
                            {topColumns.map((column) => (
                                <col key={column.key} style={{ width: column.width }} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr className="sticky top-[-1px] z-30 border-b border-gray-200 text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                                {topColumns.map((column) => (
                                    <th
                                        key={column.key}
                                        className={`align-top px-1.5 pt-2 pb-1 text-tiny font-medium ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                        style={HEADER_BACKGROUND_STYLE}
                                    >
                                        <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                                        {column.key === 'supplierName' ? (
                                            <MultiSelectCheckboxFilter
                                                options={supplierOptions}
                                                selectedValues={supplierFilter}
                                                onChange={setSupplierFilter}
                                            />
                                        ) : column.key === 'customerName' ? (
                                            <MultiSelectCheckboxFilter
                                                options={customerOptions}
                                                selectedValues={customerFilter}
                                                onChange={setCustomerFilter}
                                            />
                                        ) : column.key === 'deliveryTime' || column.key === 'confirmedDeliveryTime' ? (
                                            <div
                                                className="relative"
                                                onMouseEnter={() => setActiveDateHelpColumn(column.key)}
                                                onMouseLeave={() => setActiveDateHelpColumn(null)}
                                            >
                                                <input
                                                    type="text"
                                                    value={column.key === 'deliveryTime' ? deliveryTimeFilter : confirmedDeliveryTimeFilter}
                                                    onChange={(event) => {
                                                        if (column.key === 'deliveryTime') {
                                                            setDeliveryTimeFilter(event.target.value);
                                                            return;
                                                        }

                                                        setConfirmedDeliveryTimeFilter(event.target.value);
                                                    }}
                                                    placeholder={column.label}
                                                    className="h-5 w-full rounded-sm border border-gray-300 bg-white px-1.5 text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none"
                                                />

                                                {activeDateHelpColumn === column.key ? (
                                                    <div
                                                        className="absolute left-0 top-[28px] z-50 w-[320px] rounded-sm border border-gray-500 bg-[#eceff6] px-4 py-3 text-left text-slate-700 shadow-lg"
                                                    >
                                                        <div className="text-tiny font-normal" style={{ lineHeight: 1.8 }}>
                                                            <div className="pb-[3px]">Intervall anges med '+',</div>
                                                            <div className="pb-[3px]">Datum anges:</div>
                                                            <div className="pb-[3px]">5 =&gt; 5:e innevarande månad</div>
                                                            <div className="pb-[3px]">10-5 =&gt; 5/10 innevarande år</div>
                                                            <div className="pb-[3px]">2020-10-05 om annat år önskas</div>
                                                            <div className="pb-[3px]">Exvis: 5+10-5 =&gt; 5 denna månad till 5/10 eller +5/10 för allt fram till 5/10</div>
                                                        </div>
                                                    </div>
                                                ) : null}
                                            </div>
                                        ) : column.filterable ? (
                                            <input
                                                type="text"
                                                value={columnFilters[column.key] ?? ''}
                                                onChange={(event) => handleColumnFilterChange(column.key, event.target.value)}
                                                placeholder={column.label}
                                                className="h-5 w-full rounded-sm border border-gray-300 bg-white px-1.5 text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none"
                                            />
                                        ) : (
                                            <div className="h-[24px]" aria-hidden="true" />
                                        )}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className={`${!loading && filteredRows.length > 0 ? '' : 'bg-transparent'}`}>
                            {showSkeleton ? (
                                Array.from({ length: 10 }).map((_, index) => (
                                    <tr key={index}>
                                        <td colSpan={topColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                    </tr>
                                ))
                            ) : filteredRows.length === 0 ? (
                                <tr>
                                    <td colSpan={topColumns.length} className="px-4 py-8 text-center text-gray-400">Inga rader att visa.</td>
                                </tr>
                            ) : (
                                filteredRows.map((row) => {
                                    const isSelected = row.customerOrderId === selectedCustomerOrderId;

                                    return (
                                    <tr
                                        key={row.customerOrderId}
                                        role="button"
                                        tabIndex={0}
                                        aria-selected={isSelected}
                                        onClick={() => setSelectedCustomerOrderId(row.customerOrderId)}
                                        onKeyDown={(event) => {
                                            if (event.key === 'Enter' || event.key === ' ') {
                                                event.preventDefault();
                                                setSelectedCustomerOrderId(row.customerOrderId);
                                            }
                                        }}
                                        className={[
                                            'h-6 cursor-pointer',
                                            isSelected ? 'bg-lime-200/80' : '',
                                            !isSelected && row.isPlanned ? 'bg-lime-100/70' : '',
                                            !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/80',
                                            'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600',
                                        ].join(' ')}
                                    >
                                        <td className="truncate px-2 py-1 text-gray-800">{row.customerOrderNr}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.supplierName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800 font-semibold">{row.supplierCity}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.customerName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800 font-semibold">{row.customerCity}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.productName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.deliveryTime}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.confirmedDeliveryTime}</td>
                                        <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.editionOrdered, 0)}</td>
                                        <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.editionProduced, 0)}</td>
                                        <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.editionCustomerOrder, 0)}</td>
                                        <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.rest, 0)}</td>
                                        <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.pallets, 1)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.palletFormat}</td>
                                        <td className="px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(event) => {
                                                    event.stopPropagation();
                                                    openReportOrderInfo(row.customerOrderId);
                                                }}
                                                className="text-slate-600 hover:text-slate-800 hover:underline"
                                            >
                                                Rapp.
                                            </button>
                                        </td>
                                        <td className="px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                className="cursor-not-allowed text-gray-400"
                                                title="Planera kommer i nästa steg"
                                                disabled
                                            >
                                                Planera
                                            </button>
                                        </td>
                                    </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>
            </section>

            <section className="mt-6 grid grid-cols-1 gap-20 lg:grid-cols-3">
                <div className="flex flex-col">
                    <div className="pb-2 text-center text-xs text-gray-600">Transportorder, planering</div>
                    <div className="border-t border-gray-300 pt-1">
                        <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                            <colgroup>
                                {planningColumns.map((column) => (
                                    <col key={column.key} style={{ width: column.width }} />
                                ))}
                            </colgroup>
                            <thead>
                                <tr className="text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                                    {planningColumns.map((column) => (
                                        <th key={column.key} className="align-top px-1.5 pt-2 pb-1 text-left text-tiny font-medium" style={HEADER_BACKGROUND_STYLE}>
                                            <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {showPlanningSkeleton ? (
                                    Array.from({ length: 8 }).map((_, index) => (
                                        <tr key={`planning-skeleton-${index}`}>
                                            <td colSpan={planningColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                        </tr>
                                    ))
                                ) : planningRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={planningColumns.length} className="px-2 py-3 text-center text-gray-400">Inga transportorder i planering.</td>
                                    </tr>
                                ) : (
                                    planningRows.map((row) => {
                                        const isSelected = row.id === selectedPlanningTransportOrderId;

                                        return (
                                            <tr
                                                key={`planning-${row.id}`}
                                                role="button"
                                                tabIndex={0}
                                                aria-selected={isSelected}
                                                onClick={() => setSelectedPlanningTransportOrderId(row.id)}
                                                onKeyDown={(event) => {
                                                    if (event.key === 'Enter' || event.key === ' ') {
                                                        event.preventDefault();
                                                        setSelectedPlanningTransportOrderId(row.id);
                                                    }
                                                }}
                                                className={[
                                                    'h-6 cursor-pointer border-b border-gray-100',
                                                    isSelected ? 'bg-lime-200/80' : '',
                                                    !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/80',
                                                    'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600',
                                                ].join(' ')}
                                            >
                                                <td className="truncate px-2 py-1 text-gray-800">
                                                    <Link
                                                        to={`/logistics/transportorder/${row.id}`}
                                                        onClick={(event) => event.stopPropagation()}
                                                        className="truncate text-slate-700 hover:text-slate-900 hover:underline"
                                                        title={`Öppna transportorder ${row.transportOrderNr}`}
                                                    >
                                                        {row.transportOrderNr}
                                                    </Link>
                                                </td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.transporterName}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.loadingCities}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.unloadingCities}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.filledInfo}</td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>

                <div className="flex flex-col">
                    <div className="pb-2 text-center text-xs text-gray-600">Transportorder, aktiva</div>
                    <div className="border-t border-gray-300 pt-1">
                        <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                            <colgroup>
                                {activeColumns.map((column) => (
                                    <col key={column.key} style={{ width: column.width }} />
                                ))}
                            </colgroup>
                            <thead>
                                <tr className="text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                                    {activeColumns.map((column) => (
                                        <th key={column.key} className="align-top px-1.5 pt-2 pb-1 text-left text-tiny font-medium" style={HEADER_BACKGROUND_STYLE}>
                                            <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {showActiveSkeleton ? (
                                    Array.from({ length: 8 }).map((_, index) => (
                                        <tr key={`active-skeleton-${index}`}>
                                            <td colSpan={activeColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                        </tr>
                                    ))
                                ) : activeRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={activeColumns.length} className="px-2 py-3 text-center text-gray-400">Inga aktiva transportorder.</td>
                                    </tr>
                                ) : (
                                    activeRows.map((row) => {
                                        const isSelected = row.id === selectedActiveTransportOrderId;

                                        return (
                                            <tr
                                                key={`active-${row.id}`}
                                                role="button"
                                                tabIndex={0}
                                                aria-selected={isSelected}
                                                onClick={() => setSelectedActiveTransportOrderId(row.id)}
                                                onKeyDown={(event) => {
                                                    if (event.key === 'Enter' || event.key === ' ') {
                                                        event.preventDefault();
                                                        setSelectedActiveTransportOrderId(row.id);
                                                    }
                                                }}
                                                className={[
                                                    'h-6 cursor-pointer border-b border-gray-100',
                                                    isSelected ? 'bg-lime-200/80' : '',
                                                    !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/80',
                                                    'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600',
                                                ].join(' ')}
                                            >
                                                <td className="truncate px-2 py-1 text-gray-800">
                                                    <Link
                                                        to={`/logistics/transportorder/${row.id}`}
                                                        onClick={(event) => event.stopPropagation()}
                                                        className="truncate text-slate-700 hover:text-slate-900 hover:underline"
                                                        title={`Öppna transportorder ${row.transportOrderNr}`}
                                                    >
                                                        {row.transportOrderNr}
                                                    </Link>
                                                </td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.transporterName}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.loadingCities}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.unloadingCities}</td>
                                                <td className="px-2 py-1 text-gray-700">
                                                    <button type="button" disabled className="cursor-not-allowed text-gray-400" title="Lossad kommer i nästa steg">
                                                        Lossad
                                                    </button>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>

                <div className="flex flex-col">
                    <div className="pb-2 text-center text-xs text-gray-600">Transportorder, att återrapportera</div>
                    <div className="border-t border-gray-300 pt-1">
                        <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                            <colgroup>
                                {reportBackColumns.map((column) => (
                                    <col key={column.key} style={{ width: column.width }} />
                                ))}
                            </colgroup>
                            <thead>
                                <tr className="text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                                    {reportBackColumns.map((column) => (
                                        <th key={column.key} className="align-top px-1.5 pt-2 pb-1 text-left text-tiny font-medium" style={HEADER_BACKGROUND_STYLE}>
                                            <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {showReportBackSkeleton ? (
                                    Array.from({ length: 8 }).map((_, index) => (
                                        <tr key={`report-back-skeleton-${index}`}>
                                            <td colSpan={reportBackColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                        </tr>
                                    ))
                                ) : reportBackRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={reportBackColumns.length} className="px-2 py-3 text-center text-gray-400">Inga transportorder att återrapportera.</td>
                                    </tr>
                                ) : (
                                    reportBackRows.map((row) => {
                                        const isSelected = row.id === selectedReportBackTransportOrderId;

                                        return (
                                            <tr
                                                key={`report-back-${row.id}`}
                                                role="button"
                                                tabIndex={0}
                                                aria-selected={isSelected}
                                                onClick={() => setSelectedReportBackTransportOrderId(row.id)}
                                                onKeyDown={(event) => {
                                                    if (event.key === 'Enter' || event.key === ' ') {
                                                        event.preventDefault();
                                                        setSelectedReportBackTransportOrderId(row.id);
                                                    }
                                                }}
                                                className={[
                                                    'h-6 cursor-pointer border-b border-gray-100',
                                                    isSelected ? 'bg-lime-200/80' : '',
                                                    !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/80',
                                                    'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600',
                                                ].join(' ')}
                                            >
                                                <td className="truncate px-2 py-1 text-gray-800">
                                                    <Link
                                                        to={`/logistics/transportorder/${row.id}`}
                                                        onClick={(event) => event.stopPropagation()}
                                                        className="truncate text-slate-700 hover:text-slate-900 hover:underline"
                                                        title={`Öppna transportorder ${row.transportOrderNr}`}
                                                    >
                                                        {row.transportOrderNr}
                                                    </Link>
                                                </td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.transporterName}</td>
                                                <td className="px-2 py-1 text-gray-800">
                                                    <span className="inline-flex"><SmallStyledCheckbox checked={Boolean(row.hasNotAttestedTransportInvoices)} /></span>
                                                </td>
                                                <td className="px-2 py-1 text-gray-700">
                                                    <button type="button" disabled className="cursor-not-allowed text-gray-400" title="Kostn. kommer i nästa steg">
                                                        Kostn.
                                                    </button>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            </section>

            <ReportOrderInfoModal
                isOpen={reportOrderInfo !== null}
                onClose={() => {
                    if (reportOrderInfoSaving) return;
                    setReportOrderInfo(null);
                    setReportOrderInfoError('');
                }}
                onSave={handleSaveReportOrderInfo}
                orderedEdition={reportOrderInfo?.orderedEdition ?? null}
                producedEdition={reportOrderInfo?.producedEdition ?? null}
                isCompleted={reportOrderInfo?.isCompleted ?? false}
                isLoading={reportOrderInfoLoading}
                isSaving={reportOrderInfoSaving}
                errorMessage={reportOrderInfoError}
            />
        </div>
    );
};

export default TransportOrderOverview;
