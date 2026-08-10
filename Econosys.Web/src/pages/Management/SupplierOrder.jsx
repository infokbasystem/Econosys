import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import { DayPicker } from 'react-day-picker';
import 'react-day-picker/style.css';
import { sv } from 'date-fns/locale';

import { usePdf } from '../../contexts/PdfContext';
import ConfirmationModal from '../../components/ConfirmationModal';
import OrderNavigationTree from '../../components/OrderNavigationTree';
import OrderCost from '../../components/OrderCost';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledTextArea from '../../components/LabeledTextArea';
import apiClient from '../../config/apiClient';
import { formatDateTime, toSwedishDateInputValue } from '../../helpers/dateUtils';
import { getFileNameFromContentDisposition } from '../../helpers/fileUtils';
import { formatNumber, parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const toArray = (value) => (Array.isArray(value) ? value : []);
const toInputValue = (value) => (value == null ? '' : String(value));
const parseGoodsMarking = (value) => new Set(
    String(value || '')
        .split(/[;,|]/)
        .map((part) => part.trim().toLowerCase())
        .filter(Boolean),
);
const stringifyGoodsMarking = (flags) => Array.from(flags).join(';');
const toDateSwedishIso = (date) => {
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) return null;
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}T00:00:00`;
};
const toWeekInputValue = (value) => {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';

    const utcDate = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
    utcDate.setUTCDate(utcDate.getUTCDate() + 4 - (utcDate.getUTCDay() || 7));
    const weekYear = utcDate.getUTCFullYear();
    const yearStart = new Date(Date.UTC(weekYear, 0, 1));
    const week = Math.ceil((((utcDate - yearStart) / 86400000) + 1) / 7);
    return `${weekYear}-W${String(week).padStart(2, '0')}`;
};
const toWeekLabelValue = (value) => {
    const weekInput = toWeekInputValue(value);
    const match = weekInput.match(/^(\d{4})-W(\d{2})$/);
    return match ? `v. ${match[2]} ${match[1]}` : '';
};
const fromWeekInputToSwedishIso = (value) => {
    if (!value) return null;
    const match = String(value).match(/^(\d{4})-W(\d{2})$/);
    if (!match) return null;

    const year = Number(match[1]);
    const week = Number(match[2]);
    if (!Number.isInteger(year) || !Number.isInteger(week) || week < 1 || week > 53) return null;

    const jan4 = new Date(Date.UTC(year, 0, 4));
    const dayOfWeek = jan4.getUTCDay() || 7;
    const monday = new Date(jan4);
    monday.setUTCDate(jan4.getUTCDate() - dayOfWeek + 1 + (week - 1) * 7);

    const yyyy = monday.getUTCFullYear();
    const mm = String(monday.getUTCMonth() + 1).padStart(2, '0');
    const dd = String(monday.getUTCDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}T00:00:00`;
};
const parseNullableDecimal = (value) => {
    if (value == null || value === '') return null;
    const normalized = String(value).trim().replace(',', '.');
    if (!normalized) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};
const formatCompactNumber = (value, maxFractionDigits = 2) => {
    if (value == null || value === '') return '';
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: 0,
        maximumFractionDigits: maxFractionDigits,
    }).format(parsed);
};
const makeTempId = () => `temp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const mapApiOrderCostToEditable = (row) => ({
    localId: row?.id ? String(row.id) : makeTempId(),
    id: row?.id ?? null,
    quotationId: row?.quotationId ?? null,
    customerOrderId: row?.customerOrderId ?? null,
    supplierOrderId: row?.supplierOrderId ?? null,
    costId: row?.costId ?? null,
    doDebit: Boolean(row?.doDebit),
    nrOf: toInputValue(row?.nrOf),
    inPrice: toInputValue(row?.inPrice),
    inPriceCurrencyId: parseNullableInt(row?.inPriceCurrencyId),
    inPriceAttested: toInputValue(row?.inPriceAttested),
    outPrice: toInputValue(row?.outPrice),
    markup: row?.markup ?? null,
    note: row?.note ?? '',
    supplierName: row?.supplierName ?? '',
    invoiceId: row?.invoiceId ?? null,
    invoiceRowId: row?.invoiceRowId ?? null,
    doPrintOnQuotation: Boolean(row?.doPrintOnQuotation),
    doPrintOnCustomerOrder: Boolean(row?.doPrintOnCustomerOrder),
    doPrintOnSupplierOrder: Boolean(row?.doPrintOnSupplierOrder),
    doInvoiceSeparately: Boolean(row?.doInvoiceSeparately),
    doInvoiceSeparatelyImmediately: Boolean(row?.doInvoiceSeparatelyImmediately),
    isCostInvoicedSeparately: Boolean(row?.isCostInvoicedSeparately),
});

const buildOrderCostPayload = (row) => ({
    id: row.id ?? null,
    customerOrderId: row.customerOrderId ?? null,
    supplierOrderId: row.supplierOrderId ?? null,
    costId: parseNullableInt(row.costId),
    doDebit: Boolean(row.doDebit),
    nrOf: parseNullableDecimal(row.nrOf),
    inPrice: parseNullableDecimal(row.inPrice),
    inPriceAttested: parseNullableDecimal(row.inPriceAttested),
    outPrice: parseNullableDecimal(row.outPrice),
    note: row.note || null,
    supplierName: row.supplierName || null,
    doPrintOnQuotation: Boolean(row.doPrintOnQuotation),
    doPrintOnCustomerOrder: Boolean(row.doPrintOnCustomerOrder),
    doPrintOnSupplierOrder: Boolean(row.doPrintOnSupplierOrder),
    doInvoiceSeparately: Boolean(row.doInvoiceSeparately),
    doInvoiceSeparatelyImmediately: Boolean(row.doInvoiceSeparatelyImmediately),
    isCostInvoicedSeparately: Boolean(row.isCostInvoicedSeparately),
});

const createNewSupplierOrderModel = () => ({
    id: 0,
    supplierOrderNr: '',
    basedOnSupplierOrderId: null,
    supplierId: null,
    supplierFactoryId: null,
    inventoryId: null,
    supplierName: '',
    customerId: null,
    customerName: '',
    customerOrderNr: '',
    deliveryAddressName: '',
    deliveryAddress: '',
    deliveryPostalNr: '',
    deliveryPostalAddress: '',
    deliveryCountry: '',
    date: null,
    timeOfDelivery: '',
    yourReference: '',
    ourReference: '',
    termsOfDelivery: '',
    termsOfPayment: '',
    message: '',
    goodsMarking: '',
    product: '',
    material: '',
    format: '',
    color: '',
    construction: '',
    hideCustomerInfoOnPrint: false,
    hideCustomerNameOnPrint: false,
    hideProductNameOnPrint: false,
    isFSC: false,
    confirmed: false,
    econopackTransportResponsible: false,
    deliveryDate: null,
    deliveryDateWeekMode: false,
    confirmedDeliveryDate: null,
    confirmedDeliveryDateWeekMode: false,
    packagingType: '',
    palletFormatId: null,
    eurPallet: false,
    orderCosts: [],
    created: null,
    edited: null,
    createdByUserName: null,
    editedByUserName: null,
});

const renderMetaRow = (label, value, userName) => {
    if (!value) return null;

    return (
        <div className="grid grid-cols-21 gap-1 mx-2">
            <div className="col-span-5"><span className="font-medium">{label}</span></div>
            <div className="col-span-8">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">{userName && `av ${userName}`}</div>
        </div>
    );
};

const renderReadOnlyInfoRow = (label, value) => (
    <div>
        <div className="text-tiny tracking-wide text-gray-400">{label}</div>
        <div className="mt-1 text-xs text-gray-800 break-words">{value || 'Saknas'}</div>
    </div>
);

const SupplierOrder = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewSupplierOrder = id === 'new' || id == null;

    const {
        openPdfPreview,
        markStale,
        clearStale,
        showPdfPanel,
        closePdfPreview,
    } = usePdf();

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedWarningReason, setUnsavedWarningReason] = useState('navigate');

    const [supplierOrder, setSupplierOrder] = useState(null);
    const [originalSupplierOrder, setOriginalSupplierOrder] = useState(null);
    const [selectedCustomerDetails, setSelectedCustomerDetails] = useState(null);
    const [, setSelectedDeliveryAddressOption] = useState('');
    const [legacyUserOptions, setLegacyUserOptions] = useState([]);
    const [supplierFactoryOptions, setSupplierFactoryOptions] = useState([]);
    const [inventoryOptions, setInventoryOptions] = useState([]);
    const [orderCostOptions, setOrderCostOptions] = useState([]);
    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [palletFormatOptions, setPalletFormatOptions] = useState([]);
    const [showDeliveryDatePicker, setShowDeliveryDatePicker] = useState(false);
    const [showConfirmedDeliveryDatePicker, setShowConfirmedDeliveryDatePicker] = useState(false);

    const skipUnsavedCheckRef = React.useRef(false);
    const deliveryDatePickerRef = useRef(null);
    const confirmedDeliveryDatePickerRef = useRef(null);

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/order/supplierorders');
    };

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!supplierOrder || !originalSupplierOrder) return false;
        return JSON.stringify(supplierOrder) !== JSON.stringify(originalSupplierOrder);
    }, [supplierOrder, originalSupplierOrder]);

    const blocker = useBlocker(hasUnsavedChanges);

    useEffect(() => {
        return () => {
            closePdfPreview?.();
        };
    }, [closePdfPreview]);

    useEffect(() => {
        const handleBeforeUnload = (event) => {
            if (!hasUnsavedChanges()) return;
            event.preventDefault();
            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setUnsavedWarningReason('navigate');
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [id]);

    const getPdf = useCallback(async ({ ignoreUnsaved = false } = {}) => {
        if (!supplierOrder?.id) return null;

        if (!ignoreUnsaved && hasUnsavedChanges()) {
            setUnsavedWarningReason('print');
            setShowUnsavedWarning(true);
            return null;
        }

        openPdfPreview('');

        try {
            const response = await apiClient.get(`/pdf/supplierorder/${supplierOrder.id}`, {
                responseType: 'blob',
            });

            const contentDisposition = response.headers['content-disposition'];
            const fileName = getFileNameFromContentDisposition(contentDisposition) || `supplierorder_${supplierOrder.id}.pdf`;
            const blob = response.data;
            const url = URL.createObjectURL(blob);
            openPdfPreview(url, fileName);
            return { url, fileName };
        } catch (error) {
            console.error('Failed to get supplier order PDF:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte ladda beställnings-PDF.' },
            ]);
            return null;
        }
    }, [hasUnsavedChanges, openPdfPreview, supplierOrder?.id]);

    const updateSupplierOrder = useCallback((patch) => {
        markStale();
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setSupplierOrder((prev) => ({ ...prev, ...patch }));
        if (showPdfPanel) {
            closePdfPreview?.();
        }
    }, [closePdfPreview, markStale, showPdfPanel]);

    const handleCustomerDeliveryAddressChange = useCallback((value) => {
        const selectedAddress = selectedCustomerDetails?.deliveryAddresses?.find(
            (item, index) => String(item?.id ?? `virtual-${index}`) === String(value),
        );

        setSelectedDeliveryAddressOption(String(value ?? ''));

        updateSupplierOrder({
            deliveryAddressName: selectedAddress?.name ?? '',
            deliveryAddress: selectedAddress?.address ?? '',
            deliveryPostalNr: selectedAddress?.postalNr ?? '',
            deliveryPostalAddress: selectedAddress?.postalAddress ?? '',
            deliveryCountry: selectedAddress?.country ?? '',
        });
    }, [selectedCustomerDetails, updateSupplierOrder]);

    const buildPayload = (source) => ({
        id: parseNullableInt(source?.id) ?? 0,
        supplierOrderNr: source?.supplierOrderNr || null,
        basedOnSupplierOrderId: parseNullableInt(source?.basedOnSupplierOrderId),
        supplierId: parseNullableInt(source?.supplierId),
        supplierFactoryId: parseNullableInt(source?.supplierFactoryId),
        inventoryId: parseNullableInt(source?.inventoryId),
        supplierName: source?.supplierName || null,
        customerId: parseNullableInt(source?.customerId),
        customerOrderNr: source?.customerOrderNr || null,
        deliveryAddressName: source?.deliveryAddressName || null,
        deliveryAddress: source?.deliveryAddress || null,
        deliveryPostalNr: source?.deliveryPostalNr || null,
        deliveryPostalAddress: source?.deliveryPostalAddress || null,
        deliveryCountry: source?.deliveryCountry || null,
        date: source?.date || null,
        timeOfDelivery: source?.timeOfDelivery || null,
        yourReference: source?.yourReference || null,
        ourReference: source?.ourReference || null,
        termsOfDelivery: source?.termsOfDelivery || null,
        termsOfPayment: source?.termsOfPayment || null,
        message: source?.message || null,
        product: source?.product || null,
        material: source?.material || null,
        format: source?.format || null,
        color: source?.color || null,
        construction: source?.construction || null,
        goodsMarking: source?.goodsMarking || null,
        hideCustomerInfoOnPrint: Boolean(source?.hideCustomerInfoOnPrint),
        hideCustomerNameOnPrint: Boolean(source?.hideCustomerNameOnPrint),
        hideProductNameOnPrint: Boolean(source?.hideProductNameOnPrint),
        isFSC: Boolean(source?.isFSC),
        confirmed: Boolean(source?.confirmed),
        econopackTransportResponsible: Boolean(source?.econopackTransportResponsible),
        deliveryDate: source?.deliveryDate || null,
        deliveryDateWeekMode: Boolean(source?.deliveryDateWeekMode),
        confirmedDeliveryDate: source?.confirmedDeliveryDate || null,
        confirmedDeliveryDateWeekMode: Boolean(source?.confirmedDeliveryDateWeekMode),
        packagingType: source?.packagingType || null,
        palletFormatId: parseNullableInt(source?.palletFormatId),
        eurPallet: Boolean(source?.eurPallet),
        producedEdition: parseNullableInt(source?.producedEdition),
        orderCosts: toArray(source?.orderCosts)
            .filter((row) => row.costId != null)
            .map(buildOrderCostPayload),
    });

    const handleSave = async () => {
        if (!supplierOrder) return;

        try {
            const payload = buildPayload(supplierOrder);
            const supplierOrderId = parseNullableInt(supplierOrder?.id);
            const isCreatingNew = !(supplierOrderId && supplierOrderId > 0);

            const response = supplierOrderId && supplierOrderId > 0
                ? await apiClient.put(`/supplierorders/${supplierOrderId}`, payload)
                : await apiClient.post('/supplierorders', payload);

            const saved = response?.data ?? null;
            const savedWithEditable = {
                ...saved,
                orderCosts: toArray(saved?.orderCosts).map(mapApiOrderCostToEditable),
            };

            setSupplierOrder(savedWithEditable);
            setOriginalSupplierOrder(structuredClone(savedWithEditable));
            clearStale();
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'success'),
                { type: 'success', text: 'Beställning sparades.' },
            ]);

            if (isCreatingNew && saved?.id) {
                skipUnsavedCheckRef.current = true;
                navigate(`/order/supplierorders/${saved.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save supplier order:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte spara beställning.' },
            ]);
        }
    };

    const handleDelete = async () => {
        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/supplierorders/${supplierOrder.id}`);
            navigate('/order/supplierorders');
        } catch (error) {
            console.error('Failed to delete supplier order:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte radera beställning.' },
            ]);
        }
    };

    const handleOrderCostChange = useCallback((newRows) => {
        updateSupplierOrder({ orderCosts: newRows });
    }, [updateSupplierOrder]);

    const handlePalletRowChange = useCallback((patch) => {
        updateSupplierOrder(patch);
    }, [updateSupplierOrder]);

    const handleDeliveryDateSelect = useCallback((selectedDate) => {
        if (!(selectedDate instanceof Date) || Number.isNaN(selectedDate.getTime())) {
            return;
        }

        updateSupplierOrder({
            deliveryDate: toDateSwedishIso(selectedDate),
            deliveryDateWeekMode: false,
        });
        setShowDeliveryDatePicker(false);
    }, [updateSupplierOrder]);

    const handleDeliveryWeekSelect = useCallback((dates) => {
        const firstDate = Array.isArray(dates) && dates.length > 0 ? dates[0] : null;
        if (!(firstDate instanceof Date) || Number.isNaN(firstDate.getTime())) {
            return;
        }

        updateSupplierOrder({
            deliveryDate: toDateSwedishIso(firstDate),
            deliveryDateWeekMode: true,
        });
        setShowDeliveryDatePicker(false);
    }, [updateSupplierOrder]);

    const handleConfirmedDeliveryDateSelect = useCallback((selectedDate) => {
        if (!(selectedDate instanceof Date) || Number.isNaN(selectedDate.getTime())) {
            return;
        }

        const nextConfirmedDeliveryDate = supplierOrder?.confirmedDeliveryDateWeekMode
            ? fromWeekInputToSwedishIso(toWeekInputValue(selectedDate))
            : toDateSwedishIso(selectedDate);

        updateSupplierOrder({
            confirmedDeliveryDate: nextConfirmedDeliveryDate,
        });
        setShowConfirmedDeliveryDatePicker(false);
    }, [supplierOrder?.confirmedDeliveryDateWeekMode, updateSupplierOrder]);

    const handleGoodsMarkingToggle = useCallback((flag, checked, patch = {}) => {
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setSupplierOrder((prev) => {
            const nextFlags = parseGoodsMarking(prev?.goodsMarking);
            if (checked) {
                nextFlags.add(flag);
            } else {
                nextFlags.delete(flag);
            }

            const next = {
                ...prev,
                ...patch,
                goodsMarking: stringifyGoodsMarking(nextFlags),
            };

            markStale();
            if (showPdfPanel) {
                closePdfPreview?.();
            }

            return next;
        });
    }, [closePdfPreview, markStale, showPdfPanel]);

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        if (unsavedWarningReason === 'print') {
            void getPdf({ ignoreUnsaved: true });
            return;
        }

        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        setUnsavedWarningReason('navigate');
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    useEffect(() => {
        let isActive = true;

        const loadSupplierOrder = async () => {
            setLoading(true);

            try {
                if (isNewSupplierOrder) {
                    const empty = createNewSupplierOrderModel();
                    if (!isActive) return;
                    setSupplierOrder(empty);
                    setOriginalSupplierOrder(structuredClone(empty));
                } else {
                    const requestKey = `supplierorders:${id}`;
                    const response = await getSharedRequest(requestKey, () => apiClient.get(`/supplierorders/${id}`));
                    if (!isActive) return;
                    const data = response?.data ?? null;
                    const dataWithEditable = {
                        ...data,
                        orderCosts: toArray(data?.orderCosts).map(mapApiOrderCostToEditable),
                    };
                    setSupplierOrder(dataWithEditable);
                    setOriginalSupplierOrder(structuredClone(dataWithEditable));
                }
            } catch (error) {
                console.error('Failed to load supplier order:', error);
                if (!isActive) return;
                setSupplierOrder(null);
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        loadSupplierOrder();

        return () => {
            isActive = false;
        };
    }, [id, isNewSupplierOrder]);

    useEffect(() => {
        let isActive = true;

        const toLegacyUserOptions = (items) => (Array.isArray(items)
            ? items
                .map((item) => ({
                    id: item?.id ?? item?.name ?? '',
                    name: item?.name ?? item?.id ?? '',
                }))
                .filter((item) => item.id && item.name)
            : []);

        const loadFormOptions = async () => {
            try {
                const supplierOrderId = parseNullableInt(id);

                if (!isNewSupplierOrder && supplierOrderId && supplierOrderId > 0) {
                    const requestKey = `supplierorders:${supplierOrderId}:form-options`;
                    const response = await getSharedRequest(requestKey, () => apiClient.get(`/supplierorders/${supplierOrderId}/form-options`));
                    const payload = response?.data ?? {};

                    if (!isActive) return;

                    setLegacyUserOptions(toLegacyUserOptions(payload?.users));
                    setSupplierFactoryOptions(toArray(payload?.supplierFactories));
                    setInventoryOptions(toArray(payload?.inventories));
                    setOrderCostOptions(toArray(payload?.costs));
                    setCurrencyOptions(toArray(payload?.currencies));
                    setPalletFormatOptions(toArray(payload?.palletFormats));
                    return;
                }

                const requestKey = 'supplierorders:new:form-options';
                const [usersRes, costsRes, currenciesRes, inventoriesRes] = await getSharedRequest(requestKey, () => Promise.all([
                    apiClient.get('/legacyusers/active-options'),
                    apiClient.post('/costs/search', {}),
                    apiClient.post('/currencies/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }),
                    apiClient.post('/inventories/search', {
                        filter: {
                            conditions: [
                                {
                                    field: 'isinventory',
                                    operator: 'eq',
                                    value: true,
                                },
                            ],
                        },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                        pagination: { pageNumber: 1, pageSize: 1000 },
                    }),
                ]));

                if (!isActive) return;

                setLegacyUserOptions(toLegacyUserOptions(usersRes?.data));
                setSupplierFactoryOptions([]);
                setInventoryOptions(toArray(inventoriesRes?.data?.items));
                setOrderCostOptions(toArray(costsRes?.data));
                setCurrencyOptions(Array.isArray(currenciesRes?.data?.items) ? currenciesRes.data.items : []);
                setPalletFormatOptions([]);
            } catch (error) {
                console.error('Failed to load supplier order form options:', error);
                if (!isActive) return;
                setLegacyUserOptions([]);
                setSupplierFactoryOptions([]);
                setInventoryOptions([]);
                setOrderCostOptions([]);
                setCurrencyOptions([]);
                setPalletFormatOptions([]);
            }
        };

        loadFormOptions();

        return () => {
            isActive = false;
        };
    }, [id, isNewSupplierOrder]);

    useEffect(() => {
        const customerId = parseNullableInt(supplierOrder?.customerId);
        if (!customerId || customerId <= 0) {
            setSelectedCustomerDetails(null);
            return;
        }

        let isActive = true;

        const loadCustomerDetails = async () => {
            try {
                const requestKey = `customers:${customerId}`;
                const response = await getSharedRequest(requestKey, () => apiClient.get(`/customers/${customerId}`));
                if (!isActive) return;
                setSelectedCustomerDetails(response?.data ?? null);
            } catch (error) {
                console.error('Failed to load customer details:', error);
                if (!isActive) return;
                setSelectedCustomerDetails(null);
            }
        };

        loadCustomerDetails();

        return () => {
            isActive = false;
        };
    }, [supplierOrder?.customerId]);

    useEffect(() => {
        setSelectedDeliveryAddressOption('');
    }, [supplierOrder?.customerId]);

    useEffect(() => {
        if (!showDeliveryDatePicker && !showConfirmedDeliveryDatePicker) return undefined;

        const handleClickOutside = (event) => {
            if (deliveryDatePickerRef.current && !deliveryDatePickerRef.current.contains(event.target)) {
                setShowDeliveryDatePicker(false);
            }

            if (confirmedDeliveryDatePickerRef.current && !confirmedDeliveryDatePickerRef.current.contains(event.target)) {
                setShowConfirmedDeliveryDatePicker(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, [showConfirmedDeliveryDatePicker, showDeliveryDatePicker]);

    if (loading) {
        return (
            <div className="pl-10 space-y-4 pt-1 ml-80 mr-60">
                <Skeleton height={30} width={420} className="mb-5" />
                <Skeleton height={220} />
                <Skeleton height={180} />
                <Skeleton height={160} />
            </div>
        );
    }

    if (!supplierOrder) {
        return (
            <div className="pt-2 text-sm text-red-700">
                Kunde inte ladda beställning.
                <button
                    type="button"
                    onClick={() => navigate('/order/supplierorders')}
                    className="ml-4 rounded-sm border border-gray-300 bg-white px-3 py-1 text-xs text-gray-700 hover:bg-gray-100"
                >
                    Tillbaka
                </button>
            </div>
        );
    }

    const customerContactPersonItems = Array.isArray(selectedCustomerDetails?.contactPersons)
        ? selectedCustomerDetails.contactPersons
            .map((item) => {
                const name = item?.name || item?.customerContactPersonName || item?.contactPerson || '';
                return { id: name, name };
            })
            .filter((item) => item.name)
        : [];

    const customerDeliveryAddressItems = Array.isArray(selectedCustomerDetails?.deliveryAddresses)
        ? selectedCustomerDetails.deliveryAddresses.map((item, index) => {
            const addressSummary = [item?.address, item?.postalNr, item?.postalAddress, item?.country]
                .filter((part) => typeof part === 'string' && part.trim().length > 0)
                .join(', ');

            return {
                id: item?.id ?? `virtual-${index}`,
                name: item?.name || addressSummary || `Leveransadress ${index + 1}`,
            };
        })
        : [];

    const goodsMarkingFlags = parseGoodsMarking(supplierOrder?.goodsMarking);
    const showProductInGoodsMarking = goodsMarkingFlags.has('produkt') || !supplierOrder?.hideProductNameOnPrint;
    const showCustomerOrderNrInGoodsMarking = goodsMarkingFlags.has('kundordernr') || Boolean(supplierOrder?.customerOrderNr);
    const showCustomerNameInGoodsMarking = goodsMarkingFlags.has('kundnamn') || !supplierOrder?.hideCustomerNameOnPrint;
    const deliveryDatePickerSelectedDate = supplierOrder?.deliveryDate ? new Date(supplierOrder.deliveryDate) : undefined;
    const deliveryDateDisplayValue = supplierOrder?.deliveryDateWeekMode
        ? toWeekLabelValue(supplierOrder?.deliveryDate)
        : toSwedishDateInputValue(supplierOrder?.deliveryDate);
    const confirmedDeliveryDatePickerSelectedDate = supplierOrder?.confirmedDeliveryDate ? new Date(supplierOrder.confirmedDeliveryDate) : undefined;
    const confirmedDeliveryDateDisplayValue = supplierOrder?.confirmedDeliveryDateWeekMode
        ? toWeekLabelValue(supplierOrder?.confirmedDeliveryDate)
        : toSwedishDateInputValue(supplierOrder?.confirmedDeliveryDate);
    const unitLabel = supplierOrder?.unitName ?? (supplierOrder?.unitId ? `ID ${supplierOrder.unitId}` : '');
    const purchaseCurrencyLabel = supplierOrder?.purchaseCurrencyName ?? (supplierOrder?.purchaseCurrencyId ? `ID ${supplierOrder.purchaseCurrencyId}` : '');
    const editionLabel = formatNumber(supplierOrder?.edition, 0);
    const producedEditionLabel = formatNumber(supplierOrder?.producedEdition, 0);
    const purchasePriceLabel = formatCompactNumber(supplierOrder?.purchasePrice, 2);
    const purchaseCurrencyRateLabel = formatCompactNumber(supplierOrder?.purchaseCurrencyRate, 4);

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA BESTÄLLNING"
                message={`Är du säker på att du vill radera beställning ${supplierOrder.id}? Åtgärden kan inte ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade ändringar"
                message="Det finns osparade ändringar, vill du ändå fortsätta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-90 text-sm pt-2 pb-2 text-gray-700">
                {supplierOrder?.id ? (
                    <>Beställning <span className="ml-2 text-red-500">{supplierOrder.id}</span></>
                ) : (
                    'Ny beställning'
                )}
            </h2>

            <div className="flex h-full items-stretch">
                <div className="flex flex-col w-80 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderMetaRow('Skapad:', supplierOrder?.created, supplierOrder?.createdByUserName)}
                            {renderMetaRow('Redigerad:', supplierOrder?.edited, supplierOrder?.editedByUserName)}
                        </div>
                    </div>

                    <hr className="mt-5 border-gray-300 dark:border-white" />
                    <h2 className="text-sm text-center text-gray-700 mt-5">Meddelanden</h2>
                    {messages.length === 0 ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            {[...messages].map((message, index) => (
                                <li
                                    key={index}
                                    className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error'
                                        ? 'bg-red-100 text-red-700'
                                        : message.type === 'warning'
                                            ? 'bg-yellow-100 text-yellow-800'
                                            : 'bg-green-100 text-green-700'
                                        }`}
                                >
                                    {message.text}
                                </li>
                            ))}
                        </ul>
                    )}

                    <OrderNavigationTree
                        entityType="supplierOrder"
                        entityId={Number.isInteger(supplierOrder?.id) && supplierOrder.id > 0 ? supplierOrder.id : null}
                    />
                </div>

                <div className="flex-grow ps-4 pe-10 py-2 max-w-350">
                    <div className="flex justify-between w-full mb-5">
                        <div className="flex items-center space-x-4">
                            <button
                                type="button"
                                onClick={handleBackClick}
                                className="shadow-md/30 text-xs text-white bg-gray-500 hover:bg-gray-700 px-5 p-[5px]"
                            >
                                Tillbaka
                            </button>
                            <button
                                type="button"
                                onClick={handleSave}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px]"
                            >
                                Spara
                            </button>
                            {supplierOrder?.id !== 0 && (
                                <button
                                    type="button"
                                    onClick={() => void getPdf()}
                                    className="shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-[5px]"
                                >
                                    Skriv ut
                                </button>
                            )}
                        </div>
                        <div className="flex items-center space-x-4">
                            {supplierOrder?.id !== 0 && (
                                <button
                                    type="button"
                                    onClick={() => setShowDeleteConfirm(true)}
                                    className="shadow-md/30 text-xs text-white bg-red-700 hover:bg-red-800 px-5 p-[5px]"
                                >
                                    Radera
                                </button>
                            )}
                        </div>
                    </div>

                    <div className="ml-[-5px] w-200 mb-4 rounded-sm border border-gray-200 bg-gradient-to-br from-slate-50 via-white to-slate-100 px-5 py-4 shadow-sm">
                        <div className="grid grid-cols-[3fr_1fr]">
                            <div className="mt-0 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                                {renderReadOnlyInfoRow('Produkt', supplierOrder?.product)}
                                {renderReadOnlyInfoRow('Material', supplierOrder?.material)}
                                {renderReadOnlyInfoRow('Format', supplierOrder?.format)}
                                {renderReadOnlyInfoRow('Färg', supplierOrder?.color)}
                                {renderReadOnlyInfoRow('Konstruktion', supplierOrder?.construction)}
                                {renderReadOnlyInfoRow('FSC', supplierOrder?.isFSC ? 'Ja' : 'Nej')}
                            </div>
                            <div>
                                {renderReadOnlyInfoRow('Packning', supplierOrder?.packagingType || 'Saknas')}
                            </div>
                        </div>
                    </div>

                    <div className="mt-10 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-25">
                        <span>
                            <div className="grid grid-cols-[380px_380px_300px] gap-x-20 gap-y-8">
                                <span>
                                    <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">{supplierOrder?.customerName || ''}</p>
                                    <LabeledReactSelect
                                        name="customerDeliveryAddressId"
                                        label="Lev. adress"
                                        value={supplierOrder?.deliveryAddressName || ''}
                                        items={customerDeliveryAddressItems}
                                        onChange={handleCustomerDeliveryAddressChange}
                                        labelWidth="w-20"
                                        margintop="2"
                                        allowRawValueLabel
                                    />
                                    <LabeledInput
                                        label=""
                                        value={supplierOrder?.deliveryAddressName || ''}
                                        onChange={(value) => updateSupplierOrder({ deliveryAddressName: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label=""
                                        value={supplierOrder?.deliveryAddress || ''}
                                        onChange={(value) => updateSupplierOrder({ deliveryAddress: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <div className="flex space-x-1 w-full mt-[0px]">
                                        <LabeledInput
                                            name="deliveryPostalNr"
                                            label=""
                                            value={supplierOrder?.deliveryPostalNr || ''}
                                            onChange={(value) => updateSupplierOrder({ deliveryPostalNr: value })}
                                            labelWidth="w-20"
                                            inputWidth="w-25"
                                            margintop="0"
                                            placeholder=""
                                        />
                                        <div className="w-full">
                                            <input
                                                name="deliveryPostalAddress"
                                                value={supplierOrder?.deliveryPostalAddress || ''}
                                                onChange={(event) => updateSupplierOrder({ deliveryPostalAddress: event.target.value })}
                                                className="text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                                            />
                                        </div>
                                    </div>
                                    <LabeledInput
                                        label=""
                                        value={supplierOrder?.deliveryCountry || ''}
                                        onChange={(value) => updateSupplierOrder({ deliveryCountry: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label="Kunds onr"
                                        value={supplierOrder?.customerOrderNr || ''}
                                        onChange={(value) => updateSupplierOrder({ customerOrderNr: value })}
                                        labelWidth="w-20"
                                        margintop="3"
                                    />
                                    <LabeledInput
                                        label="Tidigare order"
                                        value={supplierOrder?.basedOnSupplierOrderId || ''}
                                        onChange={(value) => updateSupplierOrder({ basedOnSupplierOrderId: value })}
                                        labelWidth="w-20"
                                        inputWidth="w-25"
                                        margintop="0"
                                        numberOnly
                                    />
                                    <div className="flex items-center text-xs mt-[4px]">
                                        <span className="w-20 flex-none text-xs text-gray-700">Godsmärkning</span>
                                        <div className="flex items-center gap-3 ml-2">
                                            <LabeledSwitch
                                                field="goodsMarkingProdukt"
                                                name="goodsMarkingProdukt"
                                                label="produkt"
                                                labelPosition="right"
                                                value={showProductInGoodsMarking}
                                                onChange={(rowId, field, checked) => handleGoodsMarkingToggle('produkt', checked, { hideProductNameOnPrint: !checked })}
                                                labelWidth="w-auto"
                                                marginTop={0}
                                            />
                                            <LabeledSwitch
                                                field="goodsMarkingKundordernr"
                                                name="goodsMarkingKundordernr"
                                                label="kundordernr"
                                                labelPosition="right"
                                                value={showCustomerOrderNrInGoodsMarking}
                                                onChange={(rowId, field, checked) => handleGoodsMarkingToggle('kundordernr', checked)}
                                                labelWidth="w-auto"
                                                marginTop={0}
                                            />
                                            <LabeledSwitch
                                                field="goodsMarkingKundnamn"
                                                name="goodsMarkingKundnamn"
                                                label="kundnamn"
                                                labelPosition="right"
                                                value={showCustomerNameInGoodsMarking}
                                                onChange={(rowId, field, checked) => handleGoodsMarkingToggle('kundnamn', checked, { hideCustomerNameOnPrint: !checked })}
                                                labelWidth="w-auto"
                                                marginTop={0}
                                            />
                                        </div>
                                    </div>
                                </span>

                                <span>
                                    <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">{supplierOrder?.supplierName ?? 'Saknas'}</p>
                                    <LabeledReactSelect
                                        name="supplierFactoryId"
                                        label="Fabrik"
                                        value={supplierOrder?.supplierFactoryId || ''}
                                        items={supplierFactoryOptions}
                                        onChange={(value) => updateSupplierOrder({ supplierFactoryId: parseNullableInt(value) })}
                                        labelWidth="w-20"
                                        margintop="2"
                                        allowRawValueLabel
                                    />
                                    <LabeledReactSelect
                                        name="inventoryId"
                                        label="Lager"
                                        value={supplierOrder?.inventoryId || ''}
                                        items={inventoryOptions}
                                        onChange={(value) => updateSupplierOrder({ inventoryId: parseNullableInt(value) })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        allowRawValueLabel
                                    />
                                    <LabeledReactSelect
                                        name="yourReference"
                                        label="Er referens"
                                        value={supplierOrder?.yourReference || ''}
                                        items={customerContactPersonItems}
                                        onChange={(value) => updateSupplierOrder({ yourReference: value || '' })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        allowRawValueLabel
                                    />
                                    <LabeledReactSelect
                                        name="ourReference"
                                        label="Vår referens"
                                        value={supplierOrder?.ourReference || ''}
                                        items={legacyUserOptions}
                                        onChange={(value) => updateSupplierOrder({ ourReference: value || '' })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        allowRawValueLabel
                                    />
                                    <LabeledInput
                                        label="Lev.villkor"
                                        value={supplierOrder?.termsOfDelivery || ''}
                                        onChange={(value) => updateSupplierOrder({ termsOfDelivery: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledTextArea
                                        label="Bet.villkor"
                                        value={supplierOrder?.termsOfPayment || ''}
                                        onChange={(value) => updateSupplierOrder({ termsOfPayment: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        height="h-10"
                                    />
                                    <LabeledTextArea
                                        label="Meddelande"
                                        value={supplierOrder?.message || ''}
                                        onChange={(value) => updateSupplierOrder({ message: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        height="h-20"
                                    />
                                    <LabeledTextArea
                                        label="Packinstrukt."
                                        value={supplierOrder?.loadingInstruction || ''}
                                        onChange={(value) => updateSupplierOrder({ loadingInstruction: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        height="h-10"
                                    />
                                </span>

                                <span>
                                    <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">Beställningsinfo</p>
                                    <div className="">
                                        <LabeledSwitch
                                            field="econopackTransportResponsible"
                                            name="econopackTransportResponsible"
                                            label="Econopac trp.ansv."
                                            value={supplierOrder?.econopackTransportResponsible || false}
                                            onChange={(rowId, field, checked) => updateSupplierOrder({ [field]: checked })}
                                            labelWidth="w-35"
                                            marginTop={2}
                                        />

                                        <div className="relative flex items-center text-xs pb-[1px]" ref={deliveryDatePickerRef}>
                                            <label className="w-35 flex-none text-xs text-gray-700">Önskat lev.datum</label>
                                            <button
                                                type="button"
                                                onClick={() => setShowDeliveryDatePicker((prev) => !prev)}
                                                className="w-30 flex items-center justify-between text-left text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                                            >
                                                <span>{deliveryDateDisplayValue || 'Välj datum/vecka'}</span>
                                                <svg
                                                    xmlns="http://www.w3.org/2000/svg"
                                                    viewBox="0 0 24 24"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="1.8"
                                                    className="h-4 w-4 text-gray-500"
                                                    aria-hidden="true"
                                                >
                                                    <rect x="3" y="5" width="18" height="16" rx="2" />
                                                    <path d="M16 3v4M8 3v4M3 10h18" />
                                                </svg>
                                            </button>
                                            {/* <span className="ml-2 text-[11px] text-gray-500">{supplierOrder?.deliveryDateWeekMode ? 'Veckoläge' : 'Datumläge'}</span> */}
                                            {showDeliveryDatePicker && (
                                                <div className="supplier-order-daypicker absolute top-7 left-35 z-40 rounded-sm border border-gray-300 bg-white p-2 shadow-lg px-5">
                                                    <DayPicker
                                                        mode="single"
                                                        locale={sv}
                                                        selected={deliveryDatePickerSelectedDate}
                                                        onSelect={handleDeliveryDateSelect}
                                                        showWeekNumber
                                                        ISOWeek
                                                        weekStartsOn={1}
                                                        components={{
                                                            WeekNumber: ({ week, children }) => (
                                                                <button
                                                                    type="button"
                                                                    className="mt-3 mr-2 inline-flex h-[18px] min-w-[40px] items-center justify-center rounded-md px-1 text-xs leading-none text-blue-700 hover:bg-blue-50"
                                                                    title={`Välj vecka ${week?.weekNumber ?? ''}`}
                                                                    onClick={(event) => {
                                                                        event.preventDefault();
                                                                        event.stopPropagation();
                                                                        const weekDates = Array.isArray(week?.days)
                                                                            ? week.days.map((day) => day?.date).filter((date) => date instanceof Date)
                                                                            : [];
                                                                        handleDeliveryWeekSelect(weekDates);
                                                                    }}
                                                                >
                                                                    <span>{`v. ${children}`}</span>
                                                                </button>
                                                            ),
                                                        }}
                                                    />
                                                </div>
                                            )}
                                        </div>

                                        <div className="relative flex items-center text-xs" ref={confirmedDeliveryDatePickerRef}>
                                            <label className="w-35 flex-none text-xs text-gray-700">Bekräftat lev.datum</label>
                                            <button
                                                type="button"
                                                onClick={() => setShowConfirmedDeliveryDatePicker((prev) => !prev)}
                                                className="w-30 flex items-center justify-between text-left text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                                            >
                                                <span>{confirmedDeliveryDateDisplayValue || 'Välj datum'}</span>
                                                <svg
                                                    xmlns="http://www.w3.org/2000/svg"
                                                    viewBox="0 0 24 24"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="1.8"
                                                    className="h-4 w-4 text-gray-500"
                                                    aria-hidden="true"
                                                >
                                                    <rect x="3" y="5" width="18" height="16" rx="2" />
                                                    <path d="M16 3v4M8 3v4M3 10h18" />
                                                </svg>
                                            </button>
                                            {showConfirmedDeliveryDatePicker && (
                                                <div className="supplier-order-daypicker absolute top-7 left-35 z-40 rounded-sm border border-gray-300 bg-white p-2 shadow-lg px-5">
                                                    <DayPicker
                                                        mode="single"
                                                        locale={sv}
                                                        selected={confirmedDeliveryDatePickerSelectedDate}
                                                        onSelect={handleConfirmedDeliveryDateSelect}
                                                        showWeekNumber
                                                        ISOWeek
                                                        weekStartsOn={1}
                                                        components={{
                                                            WeekNumber: ({ children }) => (
                                                                <span className="mt-3 mr-2 inline-flex h-[18px] min-w-[40px] items-center justify-center rounded-md px-1 text-xs leading-none text-gray-500">
                                                                    {`v. ${children}`}
                                                                </span>
                                                            ),
                                                        }}
                                                    />
                                                </div>
                                            )}
                                        </div>

                                        <div className="mt-5 text-xs">
                                            <p className="text-gray-900 font-bold">Upplaga och pris</p>
                                            <div className="flex items-center gap-0 mt-2">
                                                <span className="text-gray-800">{editionLabel || '\u00A0'}</span>
                                                <span className="ml-2 text-gray-800">{unitLabel || '\u00A0'}</span>
                                                <span className="ml-10 text-gray-800">{purchasePriceLabel || '\u00A0'}</span>
                                                <span className="ml-2 text-xs text-gray-800">{purchaseCurrencyLabel || '\u00A0'}</span>
                                                <span className="ml-10 text-gray-800">{purchaseCurrencyRateLabel ? `Kurs: ${purchaseCurrencyRateLabel}` : '\u00A0'}</span>
                                            </div>
                                            <button
                                                type="button"
                                                className="mt-2 shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 pt-[6px] pb-[4px] rounded-sm"
                                            >
                                                Uppdatera från kalkyl
                                            </button>
                                        </div>

                                        <LabeledInput
                                            label="Producerat antal"
                                            value={supplierOrder?.producedEdition || ''}
                                            onChange={(value) => updateSupplierOrder({ producedEdition: parseNullableInt(value) })}
                                            labelWidth="w-35"
                                            inputWidth="w-30"
                                            margintop="5"
                                            numberOnly
                                        />

                                    </div>
                                </span>

                                <span className="col-span-2">
                                    <OrderCost
                                        supplierOrderId={supplierOrder?.id}
                                        rows={supplierOrder?.orderCosts ?? []}
                                        costOptions={orderCostOptions}
                                        currencyOptions={currencyOptions}
                                        palletFormatId={supplierOrder?.palletFormatId}
                                        eurPallet={supplierOrder?.eurPallet}
                                        nrOfEurPallet={null}
                                        eurPalletValue={null}
                                        palletFormatOptions={palletFormatOptions}
                                        onPalletChange={handlePalletRowChange}
                                        onChange={handleOrderCostChange}
                                    />
                                </span>
                            </div>
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default SupplierOrder;
