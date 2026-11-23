import React, { useCallback, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { ArrowLeft, ClipboardCopy, ExternalLink, Printer, RefreshCw, Save, Trash2 } from 'lucide-react';
import { Link, useBlocker, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import { usePdf } from '../../contexts/PdfContext';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import EntityOrderFlowNavigation from '../../components/EntityOrderFlowNavigation';
import OrderDetailLayout from '../../components/OrderDetailLayout';
import OrderProductInfo from '../../components/OrderProductInfo';
import useOrderDetailState from '../../hooks/useOrderDetailState';
import useDocumentTitle from '../../hooks/useDocumentTitle';
import OrderCost from '../../components/OrderCost';
import OrderHistory from '../../components/OrderHistory';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledTextArea from '../../components/LabeledTextArea';
import apiClient from '../../config/apiClient';
import useAppUpdateGuard from '../../hooks/useAppUpdateGuard';
import { formatDateTime } from '../../helpers/dateUtils';
import { resolvePdfFileName } from '../../helpers/pdfFileName';
import { formatNumber, parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { fetchSupplierOrderPackaging } from './supplierOrderPackaging';

const toArray = (value) => (Array.isArray(value) ? value : []);
const toInputValue = (value) => (value == null ? '' : String(value));
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
const getTodayDate = () => new Date().toLocaleDateString('sv-SE');
const inventoryRequiredMessage = 'Ett lager måste först väljas';
const deliveryDateRequiredMessage = 'Måste ha valt ett Önskat lev.datum för att kunna spara en beställning, VARNING';
const confirmedDeliveryDateRequiredMessage = 'Måste ha valt ett Bekräftat lev.datum för att kunna skapa ett OE';

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
    inPriceCurrencyId: parseNullableInt(row.inPriceCurrencyId),
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
    nrOfPallets: null,
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
            <div className="col-span-8 font-semibold">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">{userName && `av ${userName}`}</div>
        </div>
    );
};

const SupplierOrder = () => {
    const location = useLocation();
    const navigate = useNavigate();
    const { id } = useParams();
    const [searchParams] = useSearchParams();
    const calculationId = parseNullableInt(searchParams.get('calculationId'));
    const isNewSupplierOrder = id === 'new' || id == null;

    const [supplierOrder, setSupplierOrder] = useState(null);
    useDocumentTitle(isNewSupplierOrder ? 'Ny beställning' : `Beställning ${supplierOrder?.supplierOrderNr || '-'}`);

    const {
        openPdfPreview,
        markStale,
        clearStale,
        showPdfPanel,
        closePdfPreview,
    } = usePdf();

    const [loading, setLoading] = useState(true);
    const [loadingPackagingInstruction, setLoadingPackagingInstruction] = useState(false);
    const { messages, setMessages, isInfoPanelExpanded, isInfoPanelUsingResponsiveDefault, toggleInfoPanel } = useOrderDetailState();
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedWarningReason, setUnsavedWarningReason] = useState('navigate');

    const [originalSupplierOrder, setOriginalSupplierOrder] = useState(null);
    const [selectedCustomerDetails, setSelectedCustomerDetails] = useState(null);
    const [supplierCurrencyId, setSupplierCurrencyId] = useState(null);
    const [supplierCurrencyLoading, setSupplierCurrencyLoading] = useState(false);
    const [supplierCurrencyError, setSupplierCurrencyError] = useState('');
    const [, setSelectedDeliveryAddressOption] = useState('');
    const [legacyUserOptions, setLegacyUserOptions] = useState([]);
    const [supplierFactoryOptions, setSupplierFactoryOptions] = useState([]);
    const [inventoryOptions, setInventoryOptions] = useState([]);
    const [orderCostOptions, setOrderCostOptions] = useState([]);
    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [palletFormatOptions, setPalletFormatOptions] = useState([]);
    const [workflowOpen, setWorkflowOpen] = useState(false);
    const [workflowLoading, setWorkflowLoading] = useState(false);
    const [workflowSaving, setWorkflowSaving] = useState(false);
    const [workflowDraftReady, setWorkflowDraftReady] = useState(false);
    const [workflowOptions, setWorkflowOptions] = useState(null);
    const [selectedCalculationRowId, setSelectedCalculationRowId] = useState('');
    const [selectedInquiryId, setSelectedInquiryId] = useState('');
    const [selectedQuotationId, setSelectedQuotationId] = useState('');

    const skipUnsavedCheckRef = React.useRef(false);

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
    useAppUpdateGuard(() => hasUnsavedChanges() || Boolean(isNewSupplierOrder && supplierOrder) || workflowOpen || workflowSaving);

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

            const fileName = await resolvePdfFileName(response, 'supplierorder', supplierOrder.id, `Beställning ${supplierOrder.id}.pdf`);
            const blob = response.data;
            const url = URL.createObjectURL(blob);
            openPdfPreview(url, fileName, null, null, { itemType: 'SUPPLIERORDER', itemId: supplierOrder.id });
            return { url, fileName };
        } catch (error) {
            console.error('Failed to get supplier order PDF:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte ladda beställnings-PDF.' },
            ]);
            return null;
        }
    }, [hasUnsavedChanges, openPdfPreview, setMessages, supplierOrder?.id]);

    const updateSupplierOrder = useCallback((patch) => {
        markStale();
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setSupplierOrder((prev) => ({ ...prev, ...patch }));
        if (showPdfPanel) {
            closePdfPreview?.();
        }
    }, [closePdfPreview, markStale, setMessages, showPdfPanel]);

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
        inquiryId: parseNullableInt(source?.inquiryId),
        quotationId: parseNullableInt(source?.quotationId),
        calculationId: parseNullableInt(source?.calculationId),
        supplierId: parseNullableInt(source?.supplierId),
        supplierFactoryId: parseNullableInt(source?.supplierFactoryId),
        inventoryId: parseNullableInt(source?.inventoryId),
        supplierName: source?.supplierName || null,
        address: source?.address || null,
        address2: source?.address2 || null,
        postalNr: source?.postalNr || null,
        postalAddress: source?.postalAddress || null,
        country: source?.country || null,
        customerId: parseNullableInt(source?.customerId),
        customerDeliveryAddressId: parseNullableInt(source?.customerDeliveryAddressId),
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
        productCode: source?.productCode || null,
        material: source?.material || null,
        format: source?.format || null,
        color: source?.color || null,
        construction: source?.construction || null,
        goodsMarking: source?.hideCustomerInfoOnPrint || source?.hideCustomerNameOnPrint || source?.hideProductNameOnPrint
            ? null
            : source?.goodsMarking || null,
        preparation: source?.preparation || null,
        loadingInstruction: source?.loadingInstruction || null,
        unitId: parseNullableInt(source?.unitId),
        selectedCalculationRowId: parseNullableInt(source?.selectedCalculationRowId),
        edition: parseNullableInt(source?.edition),
        purchasePrice: parseNullableDecimal(source?.purchasePrice),
        purchaseCurrencyId: parseNullableInt(source?.purchaseCurrencyId),
        purchaseCurrencyRate: parseNullableDecimal(source?.purchaseCurrencyRate),
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
        oneWayPallet: Boolean(source?.oneWayPallet),
        producedEdition: parseNullableInt(source?.producedEdition),
        nrOfPallets: parseNullableInt(source?.nrOfPallets),
        orderCosts: toArray(source?.orderCosts)
            .filter((row) => row.costId != null)
            .map(buildOrderCostPayload),
    });

    useEffect(() => {
        if (!isNewSupplierOrder || !calculationId || calculationId <= 0) {
            return undefined;
        }

        let isActive = true;
        setWorkflowOpen(true);
        setWorkflowLoading(true);
        apiClient.get(`/supplierorder-workflow/calculations/${calculationId}`)
            .then((response) => {
                if (!isActive) return;
                const options = response?.data ?? {};
                setWorkflowOptions(options);
                setSelectedCalculationRowId(String(options?.calculationRows?.[0]?.id ?? ''));
                setSelectedInquiryId(String(options?.inquiries?.[0]?.id ?? ''));
                setSelectedQuotationId(String(options?.quotations?.[0]?.id ?? ''));

                const errors = toArray(options?.errors);
                if (errors.length > 0) {
                    setMessages((previous) => [
                        ...previous.filter((message) => message.type !== 'error'),
                        ...errors.map((text) => ({ type: 'error', text })),
                    ]);
                    setWorkflowOpen(false);
                    return;
                }
                const warnings = toArray(options?.warnings);
                if (warnings.length > 0) {
                    setMessages((previous) => [
                        ...previous.filter((message) => message.type !== 'warning'),
                        ...warnings.map((text) => ({ type: 'warning', text })),
                    ]);
                }
            })
            .catch((error) => {
                console.error('Failed to load supplier order workflow options:', error);
                if (!isActive) return;
                setWorkflowOpen(false);
                setMessages((previous) => [
                    ...previous.filter((message) => message.type !== 'error'),
                    { type: 'error', text: error?.response?.data?.message || 'Kunde inte läsa valen för beställningen.' },
                ]);
            })
            .finally(() => {
                if (isActive) setWorkflowLoading(false);
            });

        return () => {
            isActive = false;
        };
    }, [calculationId, isNewSupplierOrder, setMessages]);

    const createWorkflowInquiry = async () => {
        setWorkflowSaving(true);
        try {
            const response = await apiClient.post(`/supplierorder-workflow/calculations/${calculationId}/inquiries`);
            const inquiry = response?.data;
            setWorkflowOptions((previous) => ({
                ...previous,
                inquiries: [inquiry, ...toArray(previous?.inquiries)],
            }));
            setSelectedInquiryId(String(inquiry?.id ?? ''));
        } catch (error) {
            console.error('Failed to create inquiry from calculation:', error);
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: error?.response?.data?.message || 'Kunde inte skapa förfrågan från kalkylen.' },
            ]);
        } finally {
            setWorkflowSaving(false);
        }
    };

    const createWorkflowQuotation = async () => {
        if (!selectedInquiryId) return;
        setWorkflowSaving(true);
        try {
            const response = await apiClient.post(
                `/supplierorder-workflow/calculations/${calculationId}/inquiries/${selectedInquiryId}/quotations`,
            );
            const quotation = response?.data;
            setWorkflowOptions((previous) => ({
                ...previous,
                quotations: [quotation, ...toArray(previous?.quotations)],
            }));
            setSelectedQuotationId(String(quotation?.id ?? ''));
        } catch (error) {
            console.error('Failed to create quotation from calculation:', error);
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: error?.response?.data?.message || 'Kunde inte skapa affärsförslag från kalkylen.' },
            ]);
        } finally {
            setWorkflowSaving(false);
        }
    };

    const finishSupplierOrderWorkflow = async () => {
        if (!selectedCalculationRowId || !selectedInquiryId || !selectedQuotationId) return;
        setWorkflowSaving(true);
        try {
            const response = await apiClient.post(
                `/supplierorder-workflow/calculations/${calculationId}/draft`,
                {
                    calculationRowId: Number(selectedCalculationRowId),
                    inquiryId: Number(selectedInquiryId),
                    quotationId: Number(selectedQuotationId),
                },
            );
            const draft = response?.data?.supplierOrder;
            if (!draft) {
                throw new Error('Supplier order draft response did not contain a supplier order.');
            }
            const editableDraft = {
                ...createNewSupplierOrderModel(),
                ...draft,
                orderCosts: [],
            };
            setSupplierFactoryOptions(toArray(response?.data?.supplierFactories));
            setPalletFormatOptions(toArray(response?.data?.palletFormats));
            setSupplierOrder(editableDraft);
            setOriginalSupplierOrder(structuredClone(editableDraft));
            setWorkflowDraftReady(true);
            setWorkflowOpen(false);
            const warnings = toArray(response?.data?.warnings);
            if (warnings.length > 0) {
                setMessages((previous) => [
                    ...previous.filter((message) => !warnings.includes(message.text)),
                    ...warnings.map((text) => ({ type: 'warning', text })),
                ]);
            }
        } catch (error) {
            console.error('Failed to create supplier order draft:', error);
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: error?.response?.data?.message || error.message || 'Kunde inte skapa beställning från kalkylen.' },
            ]);
        } finally {
            setWorkflowSaving(false);
        }
    };

    const cancelSupplierOrderWorkflow = () => {
        setWorkflowOpen(false);
        skipUnsavedCheckRef.current = true;
        navigate('/order/supplierorders', { replace: true });
    };

    const handleRefreshPackagingInstruction = async () => {
        setLoadingPackagingInstruction(true);
        try {
            const packaging = await fetchSupplierOrderPackaging(apiClient, supplierOrder);
            updateSupplierOrder(packaging);
        } catch (error) {
            console.error('Failed to refresh supplier order packaging instruction:', error);
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: error?.response?.data?.message || error.message || 'Kunde inte hämta packinstruktionen från kalkylen.' },
            ]);
        } finally {
            setLoadingPackagingInstruction(false);
        }
    };

    const showDateValidationMessage = (text) => {
        setMessages((previous) => [
            ...previous.filter((message) => message.text !== text),
            { type: 'error', text },
        ]);
    };

    const clearDateValidationMessage = (text) => {
        setMessages((previous) => previous.filter((message) => message.text !== text));
    };

    const handleSave = async () => {
        if (!supplierOrder) return;
        if (supplierOrder.inventoryId == null) {
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: inventoryRequiredMessage },
            ]);
            return;
        }
        if (!supplierOrder.deliveryDate) {
            showDateValidationMessage(deliveryDateRequiredMessage);
            return;
        }

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

    const canStartSupplierOrderAction = () => {
        if (!supplierOrder?.id || supplierOrder.id <= 0) return false;
        if (hasUnsavedChanges()) {
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'warning'),
                { type: 'warning', text: 'Spara beställningen innan du skapar ett nytt ordererkännande eller en repeatorder.' },
            ]);
            return false;
        }

        return true;
    };

    const handleCreateCustomerOrder = async () => {
        if (!canStartSupplierOrderAction()) return;
        if (supplierOrder.econopackTransportResponsible && !supplierOrder.confirmedDeliveryDate) {
            showDateValidationMessage(confirmedDeliveryDateRequiredMessage);
            return;
        }

        const draftWindow = window.open('', '_blank');
        if (!draftWindow) {
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Webbläsaren blockerade den nya fliken. Tillåt popup-fönster och försök igen.' },
            ]);
            return;
        }

        try {
            const quotationResponse = supplierOrder.quotationId
                ? await apiClient.get(`/quotations/${supplierOrder.quotationId}`)
                : null;
            const quotation = quotationResponse?.data;
            const customerId = quotation?.customerId ?? supplierOrder.customerId ?? null;
            const customerResponse = customerId
                ? await apiClient.get(`/customers/${customerId}`)
                : null;
            const quotationRow = toArray(quotation?.quotationRows).find(
                (row) => Number(row?.calculationRowId) === Number(supplierOrder.selectedCalculationRowId),
            );
            const customerOrderDraft = {
                id: 0,
                yourOrderNr: supplierOrder.customerOrderNr ?? '',
                supplierOrderId: supplierOrder.id,
                supplierOrderNr: supplierOrder.supplierOrderNr || '',
                quotationId: supplierOrder.quotationId ?? null,
                customerId,
                customerName: quotation?.customerName ?? supplierOrder.customerName ?? '',
                deliveryAddressName: quotation?.deliveryAddressName ?? supplierOrder.deliveryAddressName ?? '',
                deliveryAddress: quotation?.deliveryAddress ?? supplierOrder.deliveryAddress ?? '',
                deliveryPostalNr: quotation?.deliveryPostalNr ?? supplierOrder.deliveryPostalNr ?? '',
                deliveryPostalAddress: quotation?.deliveryPostalAddress ?? supplierOrder.deliveryPostalAddress ?? '',
                deliveryCountry: quotation?.deliveryCountry ?? supplierOrder.deliveryCountry ?? '',
                date: getTodayDate(),
                yourReference: quotation?.yourReference ?? supplierOrder.yourReference ?? '',
                ourReference: customerResponse?.data?.customer?.responsibleUserName ?? '',
                termsOfDelivery: quotation?.termsOfDelivery ?? supplierOrder.termsOfDelivery ?? '',
                termsOfPayment: quotation?.termsOfPayment ?? supplierOrder.termsOfPayment ?? '',
                timeOfDelivery: quotation?.timeOfDelivery ?? supplierOrder.timeOfDelivery ?? '',
                message: quotation?.message ?? supplierOrder.message ?? '',
                product: quotation?.product ?? supplierOrder.product ?? '',
                material: quotation?.material || supplierOrder.material || '',
                format: quotation?.format ?? supplierOrder.format ?? '',
                color: quotation?.color ?? supplierOrder.color ?? '',
                construction: quotation?.construction ?? supplierOrder.construction ?? '',
                isFSC: Boolean(supplierOrder.isFSC),
                salesCurrencyId: quotation?.salesCurrencyId ?? null,
                salesCurrencyRate: quotation?.salesCurrencyRate ?? null,
                unitId: quotation?.unitId ?? supplierOrder.unitId ?? null,
                selectedCalculationRowId: supplierOrder.selectedCalculationRowId ?? null,
                edition: supplierOrder.edition ?? quotationRow?.edition ?? null,
                salesPrice: quotationRow?.price ?? null,
                palletFormatId: supplierOrder.palletFormatId ?? null,
                eurPallet: Boolean(supplierOrder.eurPallet),
                nrOfEurPallet: null,
                eurPalletValue: quotation?.eurPalletValue ?? null,
                orderCosts: toArray(supplierOrder.orderCosts).map((row) => ({
                    ...row,
                    customerOrderId: null,
                })),
            };

            const draftKey = `customer-order-draft-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
            window.localStorage.setItem(draftKey, JSON.stringify(customerOrderDraft));
            draftWindow.location.href = `/order/customerorders/new?draftKey=${encodeURIComponent(draftKey)}`;
        } catch (error) {
            draftWindow.close();
            console.error('Failed to prepare customer order from supplier order:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte förbereda ordererkännande från beställningen.' },
            ]);
        }
    };

    const handleCreateRepeatOrder = () => {
        if (!canStartSupplierOrderAction()) return;

        const repeatDraft = {
            ...supplierOrder,
            id: 0,
            supplierOrderNr: '',
            basedOnSupplierOrderId: supplierOrder.id,
            date: getTodayDate(),
            confirmed: false,
            confirmedDeliveryDate: null,
            confirmedDeliveryDateWeekMode: false,
            orderCosts: [],
            created: null,
            edited: null,
            createdByUserName: null,
            editedByUserName: null,
        };

        navigate('/order/supplierorders/new', { state: { supplierOrderDraft: repeatDraft } });
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
                    const draft = location.state?.supplierOrderDraft;
                    if (!isActive) return;
                    const initialOrder = draft
                        ? { ...empty, ...draft, id: 0, orderCosts: toArray(draft.orderCosts) }
                        : empty;
                    setSupplierOrder(initialOrder);
                    setOriginalSupplierOrder(structuredClone(initialOrder));
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
    }, [id, isNewSupplierOrder, location.state]);

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
                        orderBy: [{ field: 'name', direction: 'asc' }],
                        pagination: { pageNumber: 1, pageSize: 1000 },
                    }),
                ]));

                if (!isActive) return;

                setLegacyUserOptions(toLegacyUserOptions(usersRes?.data));
                setSupplierFactoryOptions((previous) => (
                    calculationId && calculationId > 0 ? previous : []
                ));
                setInventoryOptions(toArray(inventoriesRes?.data?.items));
                setOrderCostOptions(toArray(costsRes?.data));
                setCurrencyOptions(Array.isArray(currenciesRes?.data?.items) ? currenciesRes.data.items : []);
                setPalletFormatOptions((previous) => (
                    calculationId && calculationId > 0 ? previous : []
                ));
            } catch (error) {
                console.error('Failed to load supplier order form options:', error);
                if (!isActive) return;
                setLegacyUserOptions([]);
                setSupplierFactoryOptions((previous) => (
                    calculationId && calculationId > 0 ? previous : []
                ));
                setInventoryOptions([]);
                setOrderCostOptions([]);
                setCurrencyOptions([]);
                setPalletFormatOptions((previous) => (
                    calculationId && calculationId > 0 ? previous : []
                ));
            }
        };

        loadFormOptions();

        return () => {
            isActive = false;
        };
    }, [calculationId, id, isNewSupplierOrder]);

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
        const supplierId = parseNullableInt(supplierOrder?.supplierId);
        if (!supplierId || supplierId <= 0) {
            setSupplierCurrencyId(null);
            setSupplierCurrencyLoading(false);
            setSupplierCurrencyError('');
            return;
        }

        let isActive = true;
        setSupplierCurrencyId(null);
        setSupplierCurrencyLoading(true);
        setSupplierCurrencyError('');

        const loadSupplierCurrency = async () => {
            try {
                const requestKey = `suppliers:${supplierId}`;
                const response = await getSharedRequest(requestKey, () => apiClient.get(`/suppliers/${supplierId}`));
                if (!isActive) return;
                const currencyId = parseNullableInt(response?.data?.currencyId);
                setSupplierCurrencyId(currencyId > 0 ? currencyId : null);
            } catch (error) {
                console.error('Failed to load supplier currency:', error);
                if (!isActive) return;
                setSupplierCurrencyError('Kunde inte läsa leverantörens valuta.');
            } finally {
                if (isActive) setSupplierCurrencyLoading(false);
            }
        };

        loadSupplierCurrency();

        return () => {
            isActive = false;
        };
    }, [supplierOrder?.supplierId]);

    useEffect(() => {
        if (supplierCurrencyLoading || supplierCurrencyId == null) return;

        const currentRows = supplierOrder?.orderCosts;
        if (!Array.isArray(currentRows) || currentRows.length === 0) return;

        let changed = false;
        const updatedRows = currentRows.map((row) => {
            if (row.id != null || row.inPriceCurrencyId != null) return row;

            const selectedCost = orderCostOptions.find((option) => String(option.id) === String(row.costId));
            if (!selectedCost?.isSupplier) return row;

            changed = true;
            return { ...row, inPriceCurrencyId: supplierCurrencyId };
        });

        if (changed) updateSupplierOrder({ orderCosts: updatedRows });
    }, [
        orderCostOptions,
        supplierCurrencyId,
        supplierCurrencyLoading,
        supplierOrder?.orderCosts,
        updateSupplierOrder,
    ]);

    useEffect(() => {
        setSelectedDeliveryAddressOption('');
    }, [supplierOrder?.customerId]);

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
                    onClick={handleBackClick}
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
    const customerId = parseNullableInt(supplierOrder?.customerId);
    const supplierId = parseNullableInt(supplierOrder?.supplierId);

    const unitLabel = supplierOrder?.unitName ?? (supplierOrder?.unitId ? `ID ${supplierOrder.unitId}` : '');
    const purchaseCurrencyLabel = supplierOrder?.purchaseCurrencyName ?? (supplierOrder?.purchaseCurrencyId ? `ID ${supplierOrder.purchaseCurrencyId}` : '');
    const editionLabel = formatNumber(supplierOrder?.edition, 0);
    const purchasePriceLabel = formatCompactNumber(supplierOrder?.purchasePrice, 2);
    const purchaseCurrencyRateLabel = formatCompactNumber(supplierOrder?.purchaseCurrencyRate, 4);

    return (
        <div className="relative flex min-h-full flex-col">
            {workflowOpen && createPortal(
                <div className="fixed inset-0 z-50 overflow-y-auto">
                    <div className="fixed inset-0 bg-black/50 z-40" />
                    <div className="relative z-50 flex min-h-screen items-start justify-center pt-20 pb-8">
                    <section
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="supplier-order-workflow-title"
                        aria-busy={workflowLoading || workflowSaving}
                        className="relative bg-[#f4f3f2] rounded-sm shadow-xl max-w-4xl w-full mx-4 p-6"
                    >
                        <div className="relative flex items-center justify-center mb-4">
                            <h2 id="supplier-order-workflow-title" className="text-sm font-semibold text-center">
                                Skapa beställning
                            </h2>
                            <button
                                type="button"
                                onClick={cancelSupplierOrderWorkflow}
                                disabled={workflowSaving}
                                aria-label="Stäng"
                                className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1 disabled:opacity-50"
                            >
                                ×
                            </button>
                        </div>
                        <div className="mx-2 mt-7 text-xs text-gray-700 sm:mx-10">
                        {workflowLoading ? (
                            <p role="status" className="text-center">Laddar val från kalkylen…</p>
                        ) : (
                            <>
                                <div className="grid gap-6 lg:grid-cols-2">
                                    <div className="space-y-5">
                                        <fieldset>
                                            <legend className="mb-2 text-xs font-medium text-gray-700">Förfrågan</legend>
                                            {toArray(workflowOptions?.inquiries).length === 0 ? (
                                                <p className="text-xs text-gray-500">Det finns ingen förfrågan kopplad till kalkylen.</p>
                                            ) : (
                                                <div className="max-h-48 space-y-1 overflow-y-auto">
                                                    {workflowOptions.inquiries.map((inquiry) => (
                                                        <label key={inquiry.id} className="flex cursor-pointer items-center gap-2 px-2 py-2 text-xs hover:bg-white/60">
                                                            <input
                                                                type="radio"
                                                                name="supplier-order-inquiry"
                                                                value={inquiry.id}
                                                                checked={selectedInquiryId === String(inquiry.id)}
                                                                onChange={(event) => setSelectedInquiryId(event.target.value)}
                                                            />
                                                            <span>
                                                                {inquiry.customerName || `Förfrågan ${inquiry.id}`}
                                                                <span className="ml-2 text-xs text-gray-500">
                                                                    #{inquiry.id}{inquiry.createdAt ? ` · ${formatDateTime(inquiry.createdAt)}` : ''}
                                                                </span>
                                                        </span>
                                                        </label>
                                                    ))}
                                                </div>
                                            )}
                                            <button
                                                type="button"
                                                onClick={createWorkflowInquiry}
                                                disabled={workflowSaving}
                                                className="mt-2 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50"
                                            >
                                                Ny förfrågan
                                            </button>
                                        </fieldset>

                                        <fieldset>
                                            <legend className="mb-2 text-xs font-medium text-gray-700">Kalkylrad</legend>
                                            <div className="max-h-48 space-y-1 overflow-y-auto">
                                                {toArray(workflowOptions?.calculationRows).map((row) => (
                                                    <label key={row.id} className="flex cursor-pointer items-center gap-2 px-2 py-2 text-xs hover:bg-white/60">
                                                        <input
                                                            type="radio"
                                                            name="supplier-order-calculation-row"
                                                            value={row.id}
                                                            checked={selectedCalculationRowId === String(row.id)}
                                                            onChange={(event) => setSelectedCalculationRowId(event.target.value)}
                                                        />
                                                        <span>
                                                            Upplaga {row.edition ?? '-'} · Inköpspris {formatCompactNumber(row.purchasePrice)}
                                                        </span>
                                                    </label>
                                                ))}
                                            </div>
                                        </fieldset>
                                    </div>
                                    <fieldset>
                                        <legend className="mb-2 text-xs font-medium text-gray-700">Affärsförslag</legend>
                                        {toArray(workflowOptions?.quotations).length === 0 ? (
                                            <p className="text-xs text-gray-500">Det finns inget affärsförslag kopplat till kalkylen.</p>
                                        ) : (
                                            <div className="max-h-64 space-y-1 overflow-y-auto">
                                                {workflowOptions.quotations.map((quotation) => (
                                                    <label key={quotation.id} className="flex cursor-pointer items-center gap-2 px-2 py-2 text-xs hover:bg-white/60">
                                                        <input
                                                            type="radio"
                                                            name="supplier-order-quotation"
                                                            value={quotation.id}
                                                            checked={selectedQuotationId === String(quotation.id)}
                                                            onChange={(event) => setSelectedQuotationId(event.target.value)}
                                                        />
                                                        <span>
                                                            {quotation.customerName || `Affärsförslag ${quotation.id}`}
                                                            <span className="ml-2 text-xs text-gray-500">
                                                                #{quotation.id}{quotation.createdAt ? ` · ${formatDateTime(quotation.createdAt)}` : ''}
                                                            </span>
                                                        </span>
                                                    </label>
                                                ))}
                                            </div>
                                        )}
                                        <button
                                            type="button"
                                            onClick={createWorkflowQuotation}
                                            disabled={!selectedInquiryId || workflowSaving}
                                            className="mt-2 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50"
                                        >
                                            Nytt affärsförslag
                                        </button>
                                    </fieldset>
                                </div>
                                <div className="flex flex-wrap gap-4 mt-6 mb-3 pt-4 justify-end">
                                    <button
                                        type="button"
                                        onClick={cancelSupplierOrderWorkflow}
                                        disabled={workflowSaving}
                                        className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-5 p-[5px] disabled:opacity-50"
                                    >
                                        Avbryt
                                    </button>
                                    <button
                                        type="button"
                                        onClick={finishSupplierOrderWorkflow}
                                        disabled={!selectedInquiryId || !selectedCalculationRowId || !selectedQuotationId || workflowSaving}
                                        className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50"
                                    >
                                        {workflowSaving ? 'Skapar…' : 'Skapa beställning'}
                                    </button>
                                </div>
                            </>
                        )}
                        </div>
                    </section>
                    </div>
                </div>,
                document.body,
            )}

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

            <OrderDetailLayout
                title={supplierOrder?.id ? <>Beställning <span className="ml-2">{supplierOrder.supplierOrderNr || '-'}</span></> : 'Ny beställning'}
                messages={messages}
                isInfoPanelExpanded={isInfoPanelExpanded}
                isInfoPanelUsingResponsiveDefault={isInfoPanelUsingResponsiveDefault}
                onToggleInfoPanel={toggleInfoPanel}
                infoContent={(
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-500">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderMetaRow('Skapad:', supplierOrder?.created, supplierOrder?.createdByUserName)}
                            {renderMetaRow('Redigerad:', supplierOrder?.edited, supplierOrder?.editedByUserName)}
                        </div>
                    </div>
                )}
                infoFooter={(
                    <EntityOrderFlowNavigation
                        entityType="supplierOrder"
                        entityId={Number.isInteger(supplierOrder?.id) && supplierOrder.id > 0 ? supplierOrder.id : null}
                    />
                )}
            >
                <div className="flex justify-between w-full mb-5">
                    <div className="flex items-center gap-6">
                        <ActionButton
                            label="Tillbaka"
                            icon={ArrowLeft}
                            onClick={handleBackClick}
                            accent="slate"
                        />
                        <ActionButton
                            label="Spara"
                            icon={Save}
                            onClick={handleSave}
                            disabled={Boolean(calculationId && calculationId > 0 && !workflowDraftReady)}
                            accent="lime"
                        />
                        {supplierOrder?.id !== 0 && (
                            <ActionButton
                                label="Skriv ut"
                                icon={Printer}
                                onClick={() => void getPdf()}
                                accent="sky"
                            />
                        )}
                        {supplierOrder?.id > 0 && !supplierOrder?.blockOrderCalculationId && (
                            <>
                                <ActionButton
                                    label="Nytt ordererkännande"
                                    icon={ClipboardCopy}
                                    onClick={() => void handleCreateCustomerOrder()}
                                    accent="indigo"
                                />
                                <ActionButton
                                    label="Repeatorder"
                                    icon={ClipboardCopy}
                                    onClick={handleCreateRepeatOrder}
                                    accent="violet"
                                />
                            </>
                        )}
                    </div>
                    <div className="flex items-center gap-6">
                        {supplierOrder?.id !== 0 && (
                            <ActionButton
                                label="Radera"
                                icon={Trash2}
                                onClick={() => setShowDeleteConfirm(true)}
                                accent="rose"
                            />
                        )}
                    </div>
                </div>

                <OrderProductInfo
                    calculationId={supplierOrder?.calculationId}
                    inquiryId={supplierOrder?.inquiryId}
                    quotationId={supplierOrder?.quotationId}
                    supplierOrderId={supplierOrder?.id}
                    selectedCalculationRowId={supplierOrder?.selectedCalculationRowId}
                />

                <div className="mt-10 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-25">
                    <span>
                        <div className="grid grid-cols-[380px_380px_300px] gap-x-20 gap-y-8">
                            <span>
                                <div className="mb-4 pb-2 text-center border-b border-gray-300">
                                    <p className="flex items-center justify-center gap-1 text-sm font-semibold text-gray-500">
                                        {supplierOrder?.customerName || ''}
                                        {customerId > 0 && (
                                            <Link
                                                to={`/order/customers/${customerId}`}
                                                target="_blank"
                                                rel="noopener noreferrer"
                                                aria-label="Öppna kund"
                                                title="Öppna kund"
                                                className="ml-2 inline-flex rounded-sm text-sky-700 hover:text-sky-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-sky-500"
                                            >
                                                <ExternalLink size={14} aria-hidden="true" />
                                            </Link>
                                        )}
                                    </p>
                                </div>
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
                                            value={Boolean(supplierOrder?.hideProductNameOnPrint)}
                                            onChange={(rowId, field, checked) => updateSupplierOrder({ hideProductNameOnPrint: checked })}
                                            labelWidth="w-auto"
                                            marginTop={0}
                                        />
                                        <LabeledSwitch
                                            field="goodsMarkingKundordernr"
                                            name="goodsMarkingKundordernr"
                                            label="kundordernr"
                                            labelPosition="right"
                                            value={Boolean(supplierOrder?.hideCustomerInfoOnPrint)}
                                            onChange={(rowId, field, checked) => updateSupplierOrder({ hideCustomerInfoOnPrint: checked })}
                                            labelWidth="w-auto"
                                            marginTop={0}
                                        />
                                        <LabeledSwitch
                                            field="goodsMarkingKundnamn"
                                            name="goodsMarkingKundnamn"
                                            label="kundnamn"
                                            labelPosition="right"
                                            value={Boolean(supplierOrder?.hideCustomerNameOnPrint)}
                                            onChange={(rowId, field, checked) => updateSupplierOrder({ hideCustomerNameOnPrint: checked })}
                                            labelWidth="w-auto"
                                            marginTop={0}
                                        />
                                    </div>
                                </div>
                            </span>

                            <span>
                                <p className="mb-4 flex items-center justify-center gap-1 border-b border-gray-300 pb-2 text-center text-sm font-semibold text-gray-500">
                                    {supplierOrder?.supplierName ?? 'Saknas'}
                                    {supplierId > 0 && (
                                        <Link
                                            to={`/order/suppliers/${supplierId}`}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            aria-label="Öppna leverantör"
                                            title="Öppna leverantör"
                                            className="inline-flex rounded-sm p-0.5 text-sky-700 hover:text-sky-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-sky-500"
                                        >
                                            <ExternalLink size={14} aria-hidden="true" />
                                        </Link>
                                    )}
                                </p>
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
                                    label="Lager *"
                                    value={supplierOrder?.inventoryId || ''}
                                    items={inventoryOptions}
                                    onChange={(value) => {
                                        updateSupplierOrder({ inventoryId: parseNullableInt(value) });
                                        setMessages((previous) => previous.filter((message) => message.text !== inventoryRequiredMessage));
                                    }}
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
                                    label="Notering"
                                    value={supplierOrder?.message || ''}
                                    onChange={(value) => updateSupplierOrder({ message: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    height="h-15"
                                />
                                <LabeledTextArea
                                    label="Packinstrukt."
                                    labelAction={
                                        <button
                                            type="button"
                                            onClick={handleRefreshPackagingInstruction}
                                            disabled={loadingPackagingInstruction || !(supplierOrder?.inquiryId > 0 || supplierOrder?.calculationId > 0)}
                                            className="inline-flex items-center gap-1 mt-1 text-tiny text-sky-700 hover:underline focus-visible:underline disabled:text-gray-400 disabled:cursor-not-allowed"
                                            title="Hämta packinstruktion och palltyp från kalkylen"
                                        >
                                            <RefreshCw size={12} className={loadingPackagingInstruction ? 'shrink-0 animate-spin' : 'shrink-0'} aria-hidden="true" />
                                            {loadingPackagingInstruction ? 'Hämtar...' : 'Hämta'}
                                        </button>
                                    }
                                    value={supplierOrder?.loadingInstruction || ''}
                                    onChange={(value) => updateSupplierOrder({ loadingInstruction: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    height="h-15"
                                />
                                <div className="mt-2 flex gap-3 text-xs">
                                    <span className="w-20 shrink-0 text-gray-500">Packning</span>
                                    <span>{supplierOrder?.packagingType || '-'}</span>
                                </div>
                            </span>

                            <span>
                                <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">Beställningsinfo</p>
                                <div className="">
                                    <LabeledSwitch
                                        field="econopackTransportResponsible"
                                        name="econopackTransportResponsible"
                                        label="Econopac trp.ansv."
                                        value={supplierOrder?.econopackTransportResponsible || false}
                                        onChange={(_rowId, _field, checked) => {
                                            updateSupplierOrder({ econopackTransportResponsible: checked });
                                            if (!checked) clearDateValidationMessage(confirmedDeliveryDateRequiredMessage);
                                        }}
                                        labelWidth="w-35"
                                        marginTop={2}
                                    />

                                    <LabeledDatePicker
                                        label="Önskat lev.datum"
                                        value={supplierOrder?.deliveryDate}
                                        onChange={(nextValue) => {
                                            updateSupplierOrder({ deliveryDate: nextValue });
                                            if (nextValue) clearDateValidationMessage(deliveryDateRequiredMessage);
                                        }}
                                        weekMode={Boolean(supplierOrder?.deliveryDateWeekMode)}
                                        onWeekModeChange={(nextWeekMode) => updateSupplierOrder({ deliveryDateWeekMode: nextWeekMode })}
                                        enableWeekSelect
                                        labelWidth="w-35"
                                        inputWidth="w-35"
                                        margintop="0"
                                        placeholder="Välj datum/vecka"
                                    />

                                    <LabeledDatePicker
                                        label="Bekräftat lev.datum"
                                        value={supplierOrder?.confirmedDeliveryDate}
                                        onChange={(nextValue) => {
                                            updateSupplierOrder({ confirmedDeliveryDate: nextValue });
                                            if (nextValue) clearDateValidationMessage(confirmedDeliveryDateRequiredMessage);
                                        }}
                                        weekMode={Boolean(supplierOrder?.confirmedDeliveryDateWeekMode)}
                                        onWeekModeChange={(nextWeekMode) => updateSupplierOrder({ confirmedDeliveryDateWeekMode: nextWeekMode })}
                                        enableWeekSelect
                                        labelWidth="w-35"
                                        inputWidth="w-35"
                                        margintop="0"
                                        placeholder="Välj datum/vecka"
                                    />

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
                                    <LabeledInput
                                        label="Antal pall"
                                        name="nrOfPallets"
                                        aria-label="Antal pall"
                                        type="number"
                                        integerOnly
                                        value={supplierOrder?.nrOfPallets ?? null}
                                        onChange={(value) => updateSupplierOrder({ nrOfPallets: parseNullableInt(value) })}
                                        labelWidth="w-35"
                                        inputWidth="w-30"
                                        margintop="0"
                                    />

                                </div>
                            </span>

                            <span className="col-span-2">
                                <OrderCost
                                    supplierOrderId={supplierOrder?.id > 0 ? supplierOrder.id : null}
                                    supplierOrderNr={supplierOrder?.supplierOrderNr}
                                    showForDraft
                                    rows={supplierOrder?.orderCosts ?? []}
                                    costOptions={orderCostOptions}
                                    currencyOptions={currencyOptions}
                                    supplierCurrencyId={supplierCurrencyId}
                                    supplierCurrencyLoading={supplierCurrencyLoading}
                                    supplierCurrencyError={supplierCurrencyError}
                                    palletFormatId={supplierOrder?.palletFormatId}
                                    eurPallet={supplierOrder?.eurPallet}
                                    nrOfEurPallet={null}
                                    eurPalletValue={null}
                                    palletFormatOptions={palletFormatOptions}
                                    onPalletChange={handlePalletRowChange}
                                    onChange={handleOrderCostChange}
                                />
                            </span>

                            <span className="col-span-2">
                                <OrderHistory
                                    entityType="supplierOrder"
                                    entityId={supplierOrder?.id}
                                    showForDraft
                                    refreshKey={originalSupplierOrder?.edited}
                                />
                            </span>
                        </div>
                    </span>
                </div>
            </OrderDetailLayout>
        </div>
    );
};

export default SupplierOrder;
