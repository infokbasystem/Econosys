import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ArrowLeft, Pencil, Printer, Save, X } from 'lucide-react';
import { useBlocker, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import ConfirmationModal from '../../components/ConfirmationModal';
import ActionButton from '../../components/ActionButton';
import EntityOrderFlowNavigation from '../../components/EntityOrderFlowNavigation';
import OrderDetailLayout from '../../components/OrderDetailLayout';
import DocumentProductInfo from '../../components/DocumentProductInfo';
import { mergeDocumentProductSnapshot } from '../../helpers/documentProductSnapshot';
import useOrderDetailState from '../../hooks/useOrderDetailState';
import useDocumentTitle from '../../hooks/useDocumentTitle';
import OrderCost from '../../components/OrderCost';
import OrderHistory from '../../components/OrderHistory';
import MailLog from '../../components/MailLog';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledTextArea from '../../components/LabeledTextArea';
import NumberInput from '../../components/NumberInput';
import apiClient from '../../config/apiClient';
import useAppUpdateGuard from '../../hooks/useAppUpdateGuard';
import { formatDateTime } from '../../helpers/dateUtils';
import { parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { resolvePdfFileName } from '../../helpers/pdfFileName';
import { usePdf } from '../../contexts/PdfContext';

const toArray = (value) => (Array.isArray(value) ? value : []);
const toInputValue = (value) => (value == null ? '' : String(value));
const deliveryDateRequiredMessage = 'Välj ett leveransdatum för att kunna spara ordererkännandet.';
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

const createNewCustomerOrderModel = () => ({
    id: 0,
    customerOrderNr: '',
    yourOrderNr: '',
    supplierOrderId: null,
    supplierOrderNr: '',
    customerId: null,
    customerName: '',
    deliveryAddressName: '',
    deliveryAddress: '',
    deliveryPostalNr: '',
    deliveryPostalAddress: '',
    deliveryCountry: '',
    deliveryDate: null,
    deliveryDateWeekMode: false,
    logisticsInfoInternal: '',
    invoicingInfo: '',
    completed: false,
    completedDate: null,
    yourReference: '',
    ourReference: '',
    termsOfDelivery: '',
    termsOfPayment: '',
    message: '',
    product: '',
    material: '',
    format: '',
    color: '',
    construction: '',
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
            <div className="col-span-5"><span>{label}</span></div>
            <div className="col-span-8 font-semibold">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">{userName && `av ${userName}`}</div>
        </div>
    );
};

const CustomerOrder = () => {
    const location = useLocation();
    const navigate = useNavigate();
    const { id } = useParams();
    const [searchParams] = useSearchParams();
    const draftKey = searchParams.get('draftKey');
    const isNewCustomerOrder = id === 'new' || id == null;

    const [customerOrder, setCustomerOrder] = useState(null);
    useDocumentTitle(isNewCustomerOrder ? 'Nytt ordererkännande' : `Ordererkännande ${customerOrder?.customerOrderNr || '-'}`);

    const [loading, setLoading] = useState(true);
    const { messages, setMessages, isInfoPanelExpanded, isInfoPanelUsingResponsiveDefault, toggleInfoPanel } = useOrderDetailState();
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [showPrintWarning, setShowPrintWarning] = useState(false);
    const [showPriceAdjustment, setShowPriceAdjustment] = useState(false);
    const [isSavingPriceAdjustment, setIsSavingPriceAdjustment] = useState(false);
    const [priceAdjustmentError, setPriceAdjustmentError] = useState('');
    const [priceAdjustment, setPriceAdjustment] = useState({ purchasePrice: '', salesPrice: '' });
    const [isPrinting, setIsPrinting] = useState(false);
    const { openPdfPreview, closePdfPreview, markStale } = usePdf();
    const pdfRequestRef = useRef(0);
    const pdfObjectUrlRef = useRef(null);

    const [originalCustomerOrder, setOriginalCustomerOrder] = useState(null);
    const [selectedCustomerDetails, setSelectedCustomerDetails] = useState(null);
    const [, setSelectedDeliveryAddressOption] = useState('');
    const [legacyUserOptions, setLegacyUserOptions] = useState([]);
    const [orderCostOptions, setOrderCostOptions] = useState([]);
    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [palletFormatOptions, setPalletFormatOptions] = useState([]);

    const skipUnsavedCheckRef = useRef(false);

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        window.close();
    };

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!customerOrder || !originalCustomerOrder) return false;
        return JSON.stringify(customerOrder) !== JSON.stringify(originalCustomerOrder);
    }, [customerOrder, originalCustomerOrder]);

    const blocker = useBlocker(hasUnsavedChanges);
    useAppUpdateGuard(() => hasUnsavedChanges() || Boolean(isNewCustomerOrder && customerOrder));

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
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [id]);

    useEffect(() => {
        closePdfPreview();
        setIsPrinting(false);
        setShowPrintWarning(false);
        return () => {
            pdfRequestRef.current += 1;
            if (pdfObjectUrlRef.current) {
                URL.revokeObjectURL(pdfObjectUrlRef.current);
                pdfObjectUrlRef.current = null;
            }
        };
    }, [id, closePdfPreview]);

    const getPdf = async () => {
        if (!(customerOrder?.id > 0) || isPrinting) return;
        if (hasUnsavedChanges()) {
            setShowPrintWarning(true);
            return;
        }

        const requestId = ++pdfRequestRef.current;
        setIsPrinting(true);
        closePdfPreview();
        try {
            const response = await apiClient.get(`/pdf/customerorder/${customerOrder.id}`, { responseType: 'blob' });
            if (requestId !== pdfRequestRef.current) return;
            const fileName = await resolvePdfFileName(response, 'customerorder', customerOrder.id,
                `Ordererkännande ${customerOrder.customerOrderNr || customerOrder.id}.pdf`);
            if (requestId !== pdfRequestRef.current) return;
            if (pdfObjectUrlRef.current) URL.revokeObjectURL(pdfObjectUrlRef.current);
            const url = URL.createObjectURL(response.data);
            pdfObjectUrlRef.current = url;
            openPdfPreview(url, fileName, null, null, { itemType: 'CUSTOMERORDER', itemId: customerOrder.id });
        } catch (error) {
            if (requestId !== pdfRequestRef.current) return;
            console.error('Failed to load customer order PDF:', error);
            setMessages((previous) => [
                ...previous.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte ladda ordererkännande-PDF.' },
            ]);
        } finally {
            if (requestId === pdfRequestRef.current) setIsPrinting(false);
        }
    };

    const updateCustomerOrder = useCallback((patch) => {
        pdfRequestRef.current += 1;
        setIsPrinting(false);
        markStale();
        closePdfPreview();
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setCustomerOrder((prev) => ({ ...prev, ...patch }));
    }, [closePdfPreview, markStale, setMessages]);

    const handleCustomerDeliveryAddressChange = useCallback((value) => {
        const selectedAddress = selectedCustomerDetails?.deliveryAddresses?.find(
            (item, index) => String(item?.id ?? `virtual-${index}`) === String(value),
        );

        setSelectedDeliveryAddressOption(String(value ?? ''));

        updateCustomerOrder({
            deliveryAddressName: selectedAddress?.name ?? '',
            deliveryAddress: selectedAddress?.address ?? '',
            deliveryPostalNr: selectedAddress?.postalNr ?? '',
            deliveryPostalAddress: selectedAddress?.postalAddress ?? '',
            deliveryCountry: selectedAddress?.country ?? '',
        });
    }, [selectedCustomerDetails, updateCustomerOrder]);

    const buildPayload = (source) => ({
        id: parseNullableInt(source?.id) ?? 0,
        quotationId: parseNullableInt(source?.quotationId),
        supplierOrderId: parseNullableInt(source?.supplierOrderId),
        calculationId: parseNullableInt(source?.calculationId),
        customerOrderNr: source?.customerOrderNr || null,
        yourOrderNr: source?.yourOrderNr || null,
        customerId: parseNullableInt(source?.customerId),
        customerName: source?.customerName || null,
        deliveryAddressName: source?.deliveryAddressName || null,
        deliveryAddress: source?.deliveryAddress || null,
        deliveryPostalNr: source?.deliveryPostalNr || null,
        deliveryPostalAddress: source?.deliveryPostalAddress || null,
        deliveryCountry: source?.deliveryCountry || null,
        date: source?.date || null,
        deliveryDate: source?.deliveryDate || null,
        deliveryDateWeekMode: Boolean(source?.deliveryDateWeekMode),
        logisticsInfoInternal: source?.logisticsInfoInternal || null,
        invoicingInfo: source?.invoicingInfo || null,
        completed: Boolean(source?.completed),
        completedDate: source?.completedDate || null,
        timeOfDelivery: source?.timeOfDelivery || null,
        yourReference: source?.yourReference || null,
        ourReference: source?.ourReference || null,
        termsOfDelivery: source?.termsOfDelivery || null,
        termsOfPayment: source?.termsOfPayment || null,
        message: source?.message || null,
        product: source?.product || null,
        material: source?.material || null,
        materialThickness: source?.materialThickness || null,
        format: source?.format || null,
        color: source?.color || null,
        varnish: source?.varnish || null,
        printMethod: source?.printMethod || null,
        productNoteInternal: source?.productNoteInternal || null,
        productMessage: source?.productMessage || null,
        construction: source?.construction || null,
        isFSC: Boolean(source?.isFSC),
        salesCurrencyId: parseNullableInt(source?.salesCurrencyId),
        salesCurrencyRate: parseNullableDecimal(source?.salesCurrencyRate),
        unitId: parseNullableInt(source?.unitId),
        edition: parseNullableInt(source?.edition),
        salesPrice: parseNullableDecimal(source?.salesPrice),
        palletFormatId: parseNullableInt(source?.palletFormatId),
        eurPallet: Boolean(source?.eurPallet),
        nrOfEurPallet: parseNullableInt(source?.nrOfEurPallet),
        eurPalletValue: parseNullableDecimal(source?.eurPalletValue),
        orderCosts: toArray(source?.orderCosts)
            .filter((row) => row.costId != null)
            .map(buildOrderCostPayload),
    });

    const handleSave = async () => {
        if (!customerOrder) return;
        if (!customerOrder.deliveryDate) {
            setMessages((prev) => [
                ...prev.filter((message) => message.text !== deliveryDateRequiredMessage),
                { type: 'error', text: deliveryDateRequiredMessage },
            ]);
            return;
        }

        try {
            const payload = buildPayload(customerOrder);
            const customerOrderId = parseNullableInt(customerOrder?.id);
            const isCreatingNew = !(customerOrderId && customerOrderId > 0);

            const response = customerOrderId && customerOrderId > 0
                ? await apiClient.put(`/customerorders/${customerOrderId}`, payload)
                : await apiClient.post('/customerorders', payload);

            const saved = response?.data ?? null;
            const savedWithEditable = {
                ...saved,
                orderCosts: toArray(saved?.orderCosts).map(mapApiOrderCostToEditable),
            };

            setCustomerOrder(savedWithEditable);
            setOriginalCustomerOrder(structuredClone(savedWithEditable));
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'success'),
                { type: 'success', text: 'Ordererkännande sparades.' },
            ]);

            if (isCreatingNew && saved?.id) {
                if (draftKey) {
                    try {
                        window.localStorage.removeItem(draftKey);
                    } catch (error) {
                        console.error('Failed to remove customer order draft:', error);
                        setMessages((prev) => [
                            ...prev.filter((message) => message.type !== 'warning'),
                            { type: 'warning', text: 'Ordererkännandet sparades, men utkastet kunde inte rensas.' },
                        ]);
                    }
                }
                skipUnsavedCheckRef.current = true;
                navigate(`/order/customerorders/${saved.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save customer order:', error);
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Kunde inte spara ordererkännande.' },
            ]);
        }
    };

    const beginPriceAdjustment = () => {
        if (hasUnsavedChanges()) {
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'error'),
                { type: 'error', text: 'Spara ordererkännandet innan priserna ändras.' },
            ]);
            return;
        }

        setPriceAdjustment({
            purchasePrice: toInputValue(customerOrder.supplierOrderPurchasePrice),
            salesPrice: toInputValue(customerOrder.salesPrice),
        });
        setPriceAdjustmentError('');
        setShowPriceAdjustment(true);
    };

    const handlePriceAdjustmentSave = async () => {
        const purchasePrice = parseNullableDecimal(priceAdjustment.purchasePrice);
        const salesPrice = parseNullableDecimal(priceAdjustment.salesPrice);
        if (purchasePrice == null || salesPrice == null) {
            setPriceAdjustmentError('Ange både inköpspris och försäljningspris.');
            return;
        }

        setIsSavingPriceAdjustment(true);
        setPriceAdjustmentError('');
        try {
            const response = await apiClient.put(
                `/customerorders/${customerOrder.id}/price-adjustment`,
                { purchasePrice, salesPrice },
            );
            const saved = response?.data;
            if (!saved?.id) {
                throw new Error('Price adjustment response did not contain a customer order.');
            }

            const savedWithEditable = {
                ...saved,
                orderCosts: toArray(saved.orderCosts).map(mapApiOrderCostToEditable),
            };
            updateCustomerOrder(savedWithEditable);
            setOriginalCustomerOrder(structuredClone(savedWithEditable));
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'success'),
                { type: 'success', text: 'Inköps- och försäljningspris uppdaterades.' },
            ]);
            setShowPriceAdjustment(false);
        } catch (error) {
            console.error('Failed to update customer order prices:', error);
            setPriceAdjustmentError(error?.response?.data?.message || 'Kunde inte uppdatera inköps- och försäljningspris.');
        } finally {
            setIsSavingPriceAdjustment(false);
        }
    };

    const handleOrderCostChange = useCallback((newRows) => {
        updateCustomerOrder({ orderCosts: newRows });
    }, [updateCustomerOrder]);

    const handlePalletRowChange = useCallback((patch) => {
        updateCustomerOrder(patch);
    }, [updateCustomerOrder]);

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    useEffect(() => {
        let isActive = true;

        const loadCustomerOrder = async () => {
            setLoading(true);

            try {
                if (isNewCustomerOrder) {
                    const empty = createNewCustomerOrderModel();
                    const routeDraft = location.state?.customerOrderDraft;
                    const rawDraft = !routeDraft && draftKey ? window.localStorage.getItem(draftKey) : null;
                    const draft = routeDraft ?? (rawDraft ? JSON.parse(rawDraft) : null);
                    if (!isActive) return;
                    const initialOrder = draft
                        ? { ...empty, ...draft, id: 0, orderCosts: toArray(draft.orderCosts) }
                        : empty;
                    setCustomerOrder(initialOrder);
                    setOriginalCustomerOrder(structuredClone(initialOrder));
                    if (draftKey && !routeDraft && !rawDraft) {
                        setMessages([{ type: 'warning', text: 'Utkastet kunde inte hittas. Ett tomt ordererkännande öppnades i stället.' }]);
                    }
                } else {
                    const requestKey = `customerorders:${id}`;
                    const response = await getSharedRequest(requestKey, () => apiClient.get(`/customerorders/${id}`));
                    if (!isActive) return;
                    const data = response?.data ?? null;
                    const dataWithEditable = {
                        ...data,
                        orderCosts: toArray(data?.orderCosts).map(mapApiOrderCostToEditable),
                    };
                    setCustomerOrder(dataWithEditable);
                    setOriginalCustomerOrder(structuredClone(dataWithEditable));
                }
            } catch (error) {
                console.error('Failed to load customer order:', error);
                if (!isActive) return;
                setCustomerOrder(null);
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        loadCustomerOrder();

        return () => {
            isActive = false;
        };
    }, [draftKey, id, isNewCustomerOrder, location.state, setMessages]);

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
                const [usersRes, costsRes, currenciesRes] = await getSharedRequest('customerorders:form-options', () => Promise.all([
                    apiClient.get('/legacyusers/active-options'),
                    apiClient.post('/costs/search', {}),
                    apiClient.post('/currencies/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }),
                ]));

                if (!isActive) return;

                setLegacyUserOptions(toLegacyUserOptions(usersRes?.data));
                setOrderCostOptions(toArray(costsRes?.data));
                setCurrencyOptions(Array.isArray(currenciesRes?.data?.items) ? currenciesRes.data.items : []);
            } catch (error) {
                console.error('Failed to load customer order form options:', error);
                if (!isActive) return;
                setLegacyUserOptions([]);
                setOrderCostOptions([]);
                setCurrencyOptions([]);
                setPalletFormatOptions([]);
            }
        };

        loadFormOptions();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        const customerId = parseNullableInt(customerOrder?.customerId);
        if (!customerId) {
            setPalletFormatOptions([]);
            return undefined;
        }
        let active = true;
        apiClient.get('/palletformats/searchwithprices', { params: { customerId } })
            .then((response) => {
                if (active) setPalletFormatOptions(toArray(response.data));
            })
            .catch((error) => {
                if (!active) return;
                console.error('Failed to load customer order packaging options:', error);
                setPalletFormatOptions([]);
                setMessages((previous) => [
                    ...previous,
                    { type: 'error', text: 'Kunde inte hämta pall/packning för kunden.' },
                ]);
            });
        return () => { active = false; };
    }, [customerOrder?.customerId, setMessages]);

    useEffect(() => {
        const customerId = parseNullableInt(customerOrder?.customerId);
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
    }, [customerOrder?.customerId]);

    useEffect(() => {
        setSelectedDeliveryAddressOption('');
    }, [customerOrder?.customerId]);

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

    if (!customerOrder) {
        return (
            <div className="pt-2 text-sm text-red-700">
                Kunde inte ladda ordererkännande.
                <button
                    type="button"
                    onClick={() => navigate('/order/customerorders')}
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

    const unitLabel = customerOrder?.unitMultiplicator != null ? `x ${customerOrder.unitMultiplicator}` : '';
    const salesCurrencyLabel = customerOrder?.salesCurrencyName ?? (customerOrder?.salesCurrencyId ? `ID ${customerOrder.salesCurrencyId}` : '');
    const salesPriceLabel = formatCompactNumber(customerOrder?.salesPrice, 2);
    const salesCurrencyRateLabel = formatCompactNumber(customerOrder?.salesCurrencyRate, 4);
    const producedEditionLabel = formatCompactNumber(customerOrder?.supplierOrderProducedEdition, 0);
    const purchaseUnitLabel = customerOrder?.supplierOrderUnitMultiplicator != null ? `x ${customerOrder.supplierOrderUnitMultiplicator}` : '';
    const purchasePriceLabel = formatCompactNumber(customerOrder?.supplierOrderPurchasePrice, 2);
    const purchaseCurrencyLabel = customerOrder?.supplierOrderPurchaseCurrencyName ?? (customerOrder?.supplierOrderPurchaseCurrencyId ? `ID ${customerOrder.supplierOrderPurchaseCurrencyId}` : '');
    const purchaseCurrencyRateLabel = formatCompactNumber(customerOrder?.supplierOrderPurchaseCurrencyRate, 4);
    const editPurchasePrice = parseNullableDecimal(priceAdjustment.purchasePrice);
    const purchasePriceInSalesCurrency = editPurchasePrice != null
        && Number(customerOrder?.supplierOrderPurchaseCurrencyRate) !== 0
        && Number(customerOrder?.salesCurrencyRate) !== 0
        ? editPurchasePrice * Number(customerOrder.supplierOrderPurchaseCurrencyRate) / Number(customerOrder.salesCurrencyRate)
        : null;
    const canEditPrices = Number(customerOrder?.supplierOrderId) > 0
        && Number(customerOrder?.selectedCalculationRowId) > 0;

    return (
        <div className="relative flex min-h-full flex-col">
            {showPriceAdjustment && createPortal(
                <div
                    className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4"
                    onMouseDown={(event) => {
                        if (!isSavingPriceAdjustment && event.target === event.currentTarget) {
                            setShowPriceAdjustment(false);
                        }
                    }}
                >
                    <section
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="customer-order-price-adjustment-title"
                        className="w-full max-w-2xl rounded-sm border-2 border-[#fafad2] bg-[#ffffe0] p-6 shadow-xl"
                        onKeyDown={(event) => {
                            if (event.key === 'Escape' && !isSavingPriceAdjustment) {
                                event.stopPropagation();
                                setShowPriceAdjustment(false);
                            }
                        }}
                    >
                        <div className="relative mb-6 flex items-center justify-center">
                            <h2 id="customer-order-price-adjustment-title" className="text-sm font-semibold">
                                Redigera inköps- och försäljningspris
                            </h2>
                            <button
                                type="button"
                                onClick={() => setShowPriceAdjustment(false)}
                                disabled={isSavingPriceAdjustment}
                                aria-label="Stäng prisredigering"
                                className="absolute right-0 rounded p-1 text-gray-500 hover:bg-gray-100 disabled:opacity-50"
                            >
                                <X size={16} />
                            </button>
                        </div>
                        <div className="grid gap-4 sm:grid-cols-2">
                            <label className="block text-xs text-gray-700">
                                <span className="mb-1 block font-medium">Inköpspris ({purchaseCurrencyLabel || 'valuta saknas'})</span>
                                <input
                                    type="text"
                                    inputMode="decimal"
                                    value={priceAdjustment.purchasePrice}
                                    onChange={(event) => setPriceAdjustment((prev) => ({ ...prev, purchasePrice: event.target.value }))}
                                    className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-900"
                                />
                                <span className="mt-1 block text-gray-500">Kurs: {purchaseCurrencyRateLabel || '-'}</span>
                            </label>
                            <label className="block text-xs text-gray-700">
                                <span className="mb-1 block font-medium">Försäljningspris ({salesCurrencyLabel || 'valuta saknas'})</span>
                                <input
                                    type="text"
                                    inputMode="decimal"
                                    value={priceAdjustment.salesPrice}
                                    onChange={(event) => setPriceAdjustment((prev) => ({ ...prev, salesPrice: event.target.value }))}
                                    className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-900"
                                />
                                <span className="mt-1 block text-gray-500">Kurs: {salesCurrencyRateLabel || '-'}</span>
                            </label>
                        </div>
                        <p className="mt-4 text-xs text-gray-600">
                            Inköpspris i försäljningsvaluta: {formatCompactNumber(purchasePriceInSalesCurrency, 2)} {salesCurrencyLabel}
                        </p>
                        {priceAdjustmentError && (
                            <p role="alert" className="mt-4 text-xs text-red-700">{priceAdjustmentError}</p>
                        )}
                        <div className="mt-6 flex justify-end gap-3">
                            <button
                                type="button"
                                onClick={() => setShowPriceAdjustment(false)}
                                disabled={isSavingPriceAdjustment}
                                className="bg-orange-400 px-6 py-1.5 text-xs text-white shadow-md hover:bg-orange-600 disabled:opacity-50"
                            >
                                Avbryt
                            </button>
                            <button
                                type="button"
                                onClick={() => void handlePriceAdjustmentSave()}
                                disabled={isSavingPriceAdjustment}
                                className="bg-lime-700 px-6 py-1.5 text-xs text-white shadow-md hover:bg-lime-900 disabled:opacity-50"
                            >
                                {isSavingPriceAdjustment ? 'Sparar…' : 'Spara'}
                            </button>
                        </div>
                    </section>
                </div>,
                document.body,
            )}
            <ConfirmationModal
                isOpen={showPrintWarning}
                onClose={() => setShowPrintWarning(false)}
                onConfirm={() => setShowPrintWarning(false)}
                title="Osparade ändringar"
                message="Ordererkännandet har osparade ändringar. Spara innan du skriver ut."
                confirmText=""
                cancelText="Stäng"
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
                title={customerOrder?.id ? <>Ordererkännande <span className="ml-2">{customerOrder.customerOrderNr || '-'}</span></> : 'Nytt ordererkännande'}
                messages={messages}
                isInfoPanelExpanded={isInfoPanelExpanded}
                isInfoPanelUsingResponsiveDefault={isInfoPanelUsingResponsiveDefault}
                onToggleInfoPanel={toggleInfoPanel}
                infoContent={(
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-500">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderMetaRow('Skapad:', customerOrder?.created, customerOrder?.createdByUserName)}
                            {renderMetaRow('Redigerad:', customerOrder?.edited, customerOrder?.editedByUserName)}
                        </div>
                    </div>
                )}
                infoFooter={(
                    <EntityOrderFlowNavigation
                        entityType="customerOrder"
                        entityId={Number.isInteger(customerOrder?.id) && customerOrder.id > 0 ? customerOrder.id : null}
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
                            accent="lime"
                        />
                        {customerOrder.id > 0 && (
                            <ActionButton
                                label={isPrinting ? 'Laddar PDF...' : 'Skriv ut'}
                                icon={Printer}
                                onClick={() => void getPdf()}
                                disabled={isPrinting}
                                accent="slate"
                            />
                        )}
                        {customerOrder.id > 0 && (
                            <div className="flex items-center gap-10 ml-20">
                                <ActionButton
                                    label="Ändra ink-/fsg.pris"
                                    icon={Pencil}
                                    onClick={beginPriceAdjustment}
                                    disabled={!canEditPrices || isSavingPriceAdjustment}
                                    accent="slate"
                                />
                                <MailLog
                                    itemType="customerorder"
                                    itemId={Number(customerOrder.id)}
                                    refreshKey={originalCustomerOrder?.edited}
                                />
                            </div>
                        )}
                    </div>
                </div>

                <DocumentProductInfo
                    type="customerorders"
                    document={customerOrder}
                    dirty={hasUnsavedChanges()}
                    onRefreshed={(response) => {
                        setCustomerOrder((current) => mergeDocumentProductSnapshot(current, 'customerorders', response));
                        setOriginalCustomerOrder((current) => mergeDocumentProductSnapshot(current, 'customerorders', response));
                    }}
                />

                <div className="mt-10 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-25">
                    <span>
                        <div className="grid grid-cols-[380px_380px_330px] gap-x-20 gap-y-8">
                            <span>
                                <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">{customerOrder?.customerName || ''}</p>
                                <LabeledReactSelect
                                    name="customerDeliveryAddressId"
                                    label="Lev. adress"
                                    value={customerOrder?.deliveryAddressName || ''}
                                    items={customerDeliveryAddressItems}
                                    onChange={handleCustomerDeliveryAddressChange}
                                    labelWidth="w-20"
                                    margintop="2"
                                    allowRawValueLabel
                                />
                                <LabeledInput
                                    label=""
                                    value={customerOrder?.deliveryAddressName || ''}
                                    onChange={(value) => updateCustomerOrder({ deliveryAddressName: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                />
                                <LabeledInput
                                    label=""
                                    value={customerOrder?.deliveryAddress || ''}
                                    onChange={(value) => updateCustomerOrder({ deliveryAddress: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                />
                                <div className="flex space-x-1 w-full mt-[0px]">
                                    <LabeledInput
                                        name="deliveryPostalNr"
                                        label=""
                                        value={customerOrder?.deliveryPostalNr || ''}
                                        onChange={(value) => updateCustomerOrder({ deliveryPostalNr: value })}
                                        labelWidth="w-20"
                                        inputWidth="w-25"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    <div className="w-full">
                                        <input
                                            name="deliveryPostalAddress"
                                            value={customerOrder?.deliveryPostalAddress || ''}
                                            onChange={(event) => updateCustomerOrder({ deliveryPostalAddress: event.target.value })}
                                            className="text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                                        />
                                    </div>
                                </div>
                                <LabeledInput
                                    label=""
                                    value={customerOrder?.deliveryCountry || ''}
                                    onChange={(value) => updateCustomerOrder({ deliveryCountry: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                />
                                <LabeledInput
                                    name="material"
                                    label="Material"
                                    value={customerOrder?.material || ''}
                                    onChange={(value) => updateCustomerOrder({ material: value })}
                                    labelWidth="w-20"
                                    margintop="3"
                                />
                                <LabeledInput
                                    label="Kunds onr"
                                    value={customerOrder?.yourOrderNr || ''}
                                    onChange={(value) => updateCustomerOrder({ yourOrderNr: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                />
                                <LabeledTextArea
                                    name="logisticsInfoInternal"
                                    label="Logistikinfo"
                                    value={customerOrder?.logisticsInfoInternal || ''}
                                    onChange={(value) => updateCustomerOrder({ logisticsInfoInternal: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    height="h-10"
                                />
                            </span>

                            <span>
                                <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">{customerOrder?.supplierOrderNr || customerOrder?.supplierName || 'Saknas'}</p>
                                <LabeledReactSelect
                                    name="yourReference"
                                    label="Er referens"
                                    value={customerOrder?.yourReference || ''}
                                    items={customerContactPersonItems}
                                    onChange={(value) => updateCustomerOrder({ yourReference: value || '' })}
                                    labelWidth="w-20"
                                    margintop="2"
                                    allowRawValueLabel
                                />
                                <LabeledReactSelect
                                    name="ourReference"
                                    label="Vår referens"
                                    value={customerOrder?.ourReference || ''}
                                    items={legacyUserOptions}
                                    onChange={(value) => updateCustomerOrder({ ourReference: value || '' })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    allowRawValueLabel
                                />
                                <LabeledInput
                                    label="Lev.villkor"
                                    value={customerOrder?.termsOfDelivery || ''}
                                    onChange={(value) => updateCustomerOrder({ termsOfDelivery: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                />
                                <LabeledTextArea
                                    label="Bet.villkor"
                                    value={customerOrder?.termsOfPayment || ''}
                                    onChange={(value) => updateCustomerOrder({ termsOfPayment: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    height="h-16"
                                />
                                <LabeledTextArea
                                    label="Meddelande"
                                    value={customerOrder?.message || ''}
                                    onChange={(value) => updateCustomerOrder({ message: value })}
                                    labelWidth="w-20"
                                    margintop="0"
                                    height="h-14"
                                />
                            </span>

                            <span>
                                <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">Orderinfo</p>
                                <LabeledDatePicker
                                    label="Leveransdatum"
                                    value={customerOrder?.deliveryDate}
                                    onChange={(nextValue) => {
                                        updateCustomerOrder({ deliveryDate: nextValue });
                                        if (nextValue) {
                                            setMessages((prev) => prev.filter((message) => message.text !== deliveryDateRequiredMessage));
                                        }
                                    }}
                                    weekMode={Boolean(customerOrder?.deliveryDateWeekMode)}
                                    onWeekModeChange={(nextWeekMode) => updateCustomerOrder({ deliveryDateWeekMode: nextWeekMode })}
                                    enableWeekSelect
                                    labelWidth="w-26"
                                    inputWidth="w-35"
                                    margintop="0"
                                    placeholder="Välj datum/vecka"
                                />
                                <div className="flex items-center gap-0">
                                    <LabeledDatePicker
                                        label="Slutleverad"
                                        labelWidth="w-26"
                                        inputWidth="w-35"
                                        name="completedDate"
                                        value={customerOrder?.completedDate}
                                        onChange={(nextValue) => updateCustomerOrder({ completedDate: nextValue })}
                                        margintop="0"
                                    />
                                    <LabeledCheckbox
                                        name="completed"
                                        label="Slutförd"
                                        checked={Boolean(customerOrder?.completed)}
                                        onChange={(checked) => updateCustomerOrder({ completed: checked })}
                                    />
                                </div>
                                <LabeledTextArea
                                    name="invoicingInfo"
                                    label="Faktureringsinfo"
                                    value={customerOrder?.invoicingInfo || ''}
                                    onChange={(value) => updateCustomerOrder({ invoicingInfo: value })}
                                    labelWidth="w-26"
                                    margintop="2"
                                    height="h-8"
                                />
                                <div className="mt-5 text-xs">
                                    <p className="text-gray-900 font-bold">Upplaga och fsg.pris</p>
                                    <div className="flex items-center gap-0 mt-2">
                                        <NumberInput
                                            name="edition"
                                            aria-label="Upplaga"
                                            field="edition"
                                            value={customerOrder?.edition ?? null}
                                            decimals={0}
                                            suffix="st"
                                            onChange={(_rowId, field, value) => updateCustomerOrder({ [field]: value })}
                                            className="w-20 h-6 text-xs text-gray-800 border border-gray-300 rounded-sm bg-white"
                                        />
                                        <span className="w-10 ml-2 text-gray-800">{unitLabel || '\u00A0'}</span>
                                        <span className="w-10 ml-10 text-gray-800 text-right">{salesPriceLabel || '\u00A0'}</span>
                                        <span className="ml-2 text-xs text-gray-800">{salesCurrencyLabel || '\u00A0'}</span>
                                        <span className="ml-10 text-gray-800">{salesCurrencyRateLabel ? `Kurs: ${salesCurrencyRateLabel}` : '\u00A0'}</span>
                                    </div>
                                </div>
                                <div className="mt-5 text-xs">
                                    <p className="text-gray-900 font-bold">Producerat och inköpspris</p>
                                    <div className="flex items-center gap-0 mt-2">
                                        <span className="pr-2 w-20 text-right text-gray-800">{producedEditionLabel ? `${producedEditionLabel} st` : '\u00A0'}</span>
                                        <span className="w-10 ml-2 text-gray-800">{purchaseUnitLabel || '\u00A0'}</span>
                                        <span className="w-10 ml-10 text-gray-800 text-right">{purchasePriceLabel || '\u00A0'}</span>
                                        <span className="ml-2 text-xs text-gray-800">{purchaseCurrencyLabel || '\u00A0'}</span>
                                        <span className="ml-10 text-gray-800">{purchaseCurrencyRateLabel ? `Kurs: ${purchaseCurrencyRateLabel}` : '\u00A0'}</span>
                                    </div>
                                </div>
                            </span>

                            <span className="col-span-2">
                                <OrderCost
                                    customerOrderId={customerOrder?.id}
                                    supplierOrderId={customerOrder?.supplierOrderId}
                                    supplierOrderNr={customerOrder?.supplierOrderNr}
                                    rows={customerOrder?.orderCosts ?? []}
                                    costOptions={orderCostOptions}
                                    currencyOptions={currencyOptions}
                                    palletFormatId={customerOrder?.palletFormatId}
                                    eurPallet={customerOrder?.eurPallet}
                                    nrOfEurPallet={customerOrder?.nrOfEurPallet}
                                    eurPalletValue={customerOrder?.eurPalletValue}
                                    palletFormatOptions={palletFormatOptions}
                                    onPalletChange={handlePalletRowChange}
                                    onChange={handleOrderCostChange}
                                />
                            </span>

                            <span className="col-span-2">
                                <OrderHistory
                                    entityType="customerOrder"
                                    entityId={customerOrder?.id}
                                    refreshKey={originalCustomerOrder?.edited}
                                />
                            </span>
                        </div>
                    </span>
                </div>
            </OrderDetailLayout>
        </div>
    );
};

export default CustomerOrder;
