import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, ClipboardCheck, FileMinus, GripVertical, Pencil, Printer, Save, Trash2 } from 'lucide-react';
import { useBlocker, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import { usePdf } from '../../contexts/PdfContext';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import MailLog from '../../components/MailLog';
import InvoiceCustomerOrdersModal from '../../components/InvoiceCustomerOrdersModal';
import InvoiceInvoicingInfoModal from '../../components/InvoiceInvoicingInfoModal';
import InvoiceStockDeliveryAdjustmentModal from '../../components/InvoiceStockDeliveryAdjustmentModal';
import SearchCustomer from '../../components/SearchCustomer';
import OrderDetailLayout from '../../components/OrderDetailLayout';
import useOrderDetailState from '../../hooks/useOrderDetailState';
import SelectCircleCheckbox from '../../components/SelectCircleCheckbox';
import LabeledSelect from '../../components/LabeledSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledTextArea from '../../components/LabeledTextArea';
import NumberInput from '../../components/NumberInput';
import apiClient from '../../config/apiClient';
import useAppUpdateGuard from '../../hooks/useAppUpdateGuard';
import { formatDateTime } from '../../helpers/dateUtils';
import { getFileNameFromContentDisposition } from '../../helpers/fileUtils';
import { parseNullableInt, parseNullableNumber } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import {
    createNewInvoiceModel,
    createCreditInvoiceDraft,
    CREDIT_INVOICE_TYPE_ID,
    hasCreditStockDeliveries,
    getInvoiceCustomerFields,
    isInvoiceCustomerLocked,
    toDateInputValue,
    autoResizeTextarea,
    applyInvoiceDerivedRows,
    computeInvoiceRowSum,
    getInvoiceInventorySource,
    getInvoiceRowIdentity,
    reorderInvoiceRows,
    VAT_PRICE_TYPE_ID,
    ROUNDING_PRICE_TYPE_ID,
} from '../../helpers/invoiceUtils';


// Backend column is smallint.
const MAX_INVOICE_DAYS = 32767;

const normalizeInvoiceForEditor = (source) => {
    if (!source) {
        return source;
    }

    return {
        ...source,
        invoiceRows: Array.isArray(source?.invoiceRows)
            ? source.invoiceRows.map((row, index) => ({
                ...row,
                tempId: (Number(row?.id) || 0) > 0 ? undefined : (row?.tempId ?? `draft_row_${index}_${Date.now()}`),
            }))
            : [],
        invoiceAccountRows: Array.isArray(source?.invoiceAccountRows)
            ? source.invoiceAccountRows.map((row, index) => ({
                ...row,
                tempKey: (Number(row?.id) || 0) > 0 ? undefined : (row?.tempKey ?? `draft_account_${index}_${Date.now()}`),
            }))
            : [],
    };
};

const renderInvoiceMetaRow = (label, value, userName) => {
    if (!value) {
        return null;
    }

    return (
        <div className="grid grid-cols-21 gap-1 mx-2">
            <div className="col-span-5"><span className="font-medium">{label}</span></div>
            <div className="col-span-8">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">
                {userName && `av ${userName}`}
            </div>
        </div>
    );
};


const Invoice = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const { id } = useParams();
    const [searchParams] = useSearchParams();
    const isNewInvoice = id == null || id === 'new';
    const draftKey = searchParams.get('draftKey');
    const openedInNewTab = searchParams.get('openedInNewTab') === '1';

    const { openPdfPreview, markStale, clearStale, showPdfPanel, closePdfPreview } = usePdf();

    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedWarningReason, setUnsavedWarningReason] = useState('navigate');
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showCustomerOrders, setShowCustomerOrders] = useState(false);
    const [showInvoicingInfo, setShowInvoicingInfo] = useState(false);
    const [showStockDeliveryAdjustment, setShowStockDeliveryAdjustment] = useState(false);
    const [isEditingInvoiceDate, setIsEditingInvoiceDate] = useState(false);
    const [loading, setLoading] = useState(true);
    const [mailLogRevision, setMailLogRevision] = useState(0);
    const [creditRelations, setCreditRelations] = useState(null);
    const [creditRelationsError, setCreditRelationsError] = useState(null);
    const [creditRelationsRetry, setCreditRelationsRetry] = useState(0);
    const [invoiceCustomerOrders, setInvoiceCustomerOrders] = useState(null);
    const [invoiceCustomerOrdersError, setInvoiceCustomerOrdersError] = useState(null);
    const [invoiceCustomerOrdersRetry, setInvoiceCustomerOrdersRetry] = useState(0);
    const { messages, setMessages, isInfoPanelExpanded, isInfoPanelUsingResponsiveDefault, toggleInfoPanel } = useOrderDetailState();

    const [invoice, setInvoice] = useState(null);
    const [originalInvoice, setOriginalInvoice] = useState(null);
    const [rowDrag, setRowDrag] = useState(null);
    const rowElementsRef = useRef(new Map());
    const rowPositionsRef = useRef(null);
    const [newlyCreatedInvoiceId, setNewlyCreatedInvoiceId] = useState(location.state?.newlyCreatedInvoiceId ?? null);
    const [currencies, setCurrencies] = useState([]);
    const [units, setUnits] = useState([]);
    const [paymentTerms, setPaymentTerms] = useState([]);
    const [customerSelectionPending, setCustomerSelectionPending] = useState(false);
    const skipUnsavedCheckRef = useRef(false);
    const createdInvoiceIdRef = useRef(null);
    const customerSelectionDisabled = isInvoiceCustomerLocked(originalInvoice) || isInvoiceCustomerLocked(invoice);
    const inventorySource = getInvoiceInventorySource(invoice);
    const originalInventorySource = getInvoiceInventorySource(originalInvoice);

    useEffect(() => {
        const invoiceId = invoice?.id;
        if (!(invoiceId > 0)) return undefined;

        let controller;
        const loadCreditRelations = async () => {
            controller?.abort();
            controller = new AbortController();
            const signal = controller.signal;
            try {
                const { data } = await apiClient.get(`/invoices/${invoiceId}/credit-relations`, { signal });
                if (!Array.isArray(data?.creditInvoices)) throw new Error('Invalid invoice credit relations response.');
                if (signal.aborted) return;
                setCreditRelations({ invoiceId, ...data });
                setCreditRelationsError(null);
            } catch (error) {
                if (signal.aborted) return;
                console.error('Failed to load invoice credit relations:', error);
                setCreditRelations(null);
                setCreditRelationsError(invoiceId);
            }
        };
        loadCreditRelations();
        window.addEventListener('focus', loadCreditRelations);
        return () => {
            controller?.abort();
            window.removeEventListener('focus', loadCreditRelations);
        };
    }, [invoice?.id, creditRelationsRetry]);

    useEffect(() => {
        const invoiceId = invoice?.id;
        if (!(invoiceId > 0)) {
            setInvoiceCustomerOrders(null);
            setInvoiceCustomerOrdersError(null);
            return undefined;
        }

        let controller;
        const loadCustomerOrders = async () => {
            controller?.abort();
            controller = new AbortController();
            const signal = controller.signal;
            setInvoiceCustomerOrdersError(null);
            try {
                const { data } = await apiClient.get(`/invoices/${invoiceId}/customer-orders`, { signal });
                if (!Array.isArray(data)) throw new Error('Invalid invoice customer order response.');
                if (signal.aborted) return;
                setInvoiceCustomerOrders({ invoiceId, orders: data });
            } catch (error) {
                if (signal.aborted) return;
                console.error('Failed to load invoice customer order invoicing info:', error);
                setInvoiceCustomerOrdersError(invoiceId);
            }
        };
        loadCustomerOrders();
        window.addEventListener('focus', loadCustomerOrders);
        return () => {
            controller?.abort();
            window.removeEventListener('focus', loadCustomerOrders);
        };
    }, [invoice?.id, invoiceCustomerOrdersRetry]);

    useEffect(() => {
        if (!originalInvoice) return;
        if (inventorySource === originalInventorySource) {
            setInvoice((prev) => prev ? { ...prev, invoiceAccountRows: originalInvoice.invoiceAccountRows } : prev);
            setMessages((prev) => prev.filter((message) => message.code !== 'inventory-preview'));
            return;
        }
        let active = true;
        const timer = window.setTimeout(async () => {
            try {
                const response = await apiClient.post('/invoices/accounting-preview', {
                    customerId: invoice?.customerId,
                    invoiceTypeCode: invoice?.invoiceTypeCode,
                    invoiceRows: JSON.parse(inventorySource),
                });
                if (!active) return;
                setInvoice((prev) => ({
                    ...prev,
                    invoiceAccountRows: response.data.map((row, index) => ({ ...row, tempKey: `preview_${index}` })),
                }));
                setMessages((prev) => prev.filter((message) => message.code !== 'inventory-preview'));
            } catch (error) {
                if (!active) return;
                console.error('Failed to recalculate invoice inventory accounting:', error);
                setMessages((prev) => [
                    ...prev.filter((message) => message.code !== 'inventory-preview'),
                    { type: 'error', code: 'inventory-preview', text: `Kunde inte beräkna lagerbokföringen: ${error?.response?.data?.message ?? error.message}` },
                ]);
            }
        }, 250);
        return () => {
            active = false;
            window.clearTimeout(timer);
        };
    }, [inventorySource, originalInventorySource, originalInvoice, invoice?.customerId, invoice?.invoiceTypeCode, setMessages]);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!invoice || !originalInvoice) return false;
        return JSON.stringify(invoice) !== JSON.stringify(originalInvoice);
    }, [invoice, originalInvoice]);

    const blocker = useBlocker(hasUnsavedChanges);
    useAppUpdateGuard(() => hasUnsavedChanges() || Boolean(isNewInvoice && invoice));

    useEffect(() => {
        return () => {
            closePdfPreview?.();
        };
    }, [closePdfPreview]);

    useEffect(() => {
        const handleBeforeUnload = (e) => {
            if (hasUnsavedChanges()) {
                e.preventDefault();
                e.returnValue = '';
                return '';
            }
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
        setIsEditingInvoiceDate(false);
    }, [id]);

    useEffect(() => {
        if (location.state?.newlyCreatedInvoiceId == null) return;

        // Consume the creation marker so refresh/back navigation does not replay it.
        navigate(`${location.pathname}${location.search}${location.hash}`, {
            replace: true,
            state: { ...location.state, newlyCreatedInvoiceId: null },
        });
    }, [location, navigate]);

    const handleBackClick = () => {
        const hasSingleEntryHistory = window.history.length <= 1;
        const shouldCloseNewTab = openedInNewTab || hasSingleEntryHistory;
        const canNavigateBack = window.history.length > 1;

        if (shouldCloseNewTab) {
            window.close();

            // Browsers can block closing tabs not opened by script.
            // If that happens, fall back to normal navigation behavior.
            if (!window.closed) {
                if (canNavigateBack) {
                    navigate(-1);
                    return;
                }

                navigate('/finance/searchinvoice');
            }

            return;
        }

        if (canNavigateBack) {
            navigate(-1);
            return;
        }

        navigate('/finance/searchinvoice');
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);

        if (unsavedWarningReason === 'print') {
            getPdf({ ignoreUnsaved: true });
            return;
        }

        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    const handleChange = (field, value) => {
        markStale();
        setMessages((prev) => prev.filter((msg) => msg.type !== 'success'));
        setInvoice((prev) => {
            if (!prev) return prev;

            if (field === 'vatAccount') {
                const vatRowIndex = (prev.invoiceRows ?? []).findIndex(
                    (row) => Number(row?.priceTypeId) === VAT_PRICE_TYPE_ID,
                );

                if (vatRowIndex === -1) {
                    return prev;
                }

                const updatedRows = (prev.invoiceRows ?? []).map((row, index) => (
                    index === vatRowIndex
                        ? { ...row, accountNr: value }
                        : row
                ));

                return {
                    ...prev,
                    invoiceRows: applyInvoiceDerivedRows(updatedRows, prev.vatRate),
                };
            }

            return {
                ...prev,
                [field]: value,
            };
        });

        if (showPdfPanel) {
            closePdfPreview();
        }
    };

    const handleCustomerSelect = (customer) => {
        if (customerSelectionDisabled) return;

        markStale();
        setMessages((prev) => prev.filter((msg) => msg.type !== 'success' && msg.type !== 'warning'));
        setCustomerSelectionPending(false);
        setInvoice((prev) => {
            if (!prev) return prev;
            const fields = getInvoiceCustomerFields(customer);
            return { ...prev, ...fields, invoiceRows: applyInvoiceDerivedRows(prev.invoiceRows, fields.vatRate) };
        });

        if (showPdfPanel) {
            closePdfPreview();
        }
    };

    const handleInvoiceRowChange = (idOrTempId, field, value) => {
        markStale();

        setInvoice((prev) => ({
            ...prev,
            invoiceRows: applyInvoiceDerivedRows((prev?.invoiceRows ?? []).map((row) => {
                if (getInvoiceRowIdentity(row) !== idOrTempId) {
                    return row;
                }

                const next = { ...row, [field]: value };
                if (field === 'nrOf' || field === 'unitPrice' || field === 'unitId') {
                    next.sum = computeInvoiceRowSum(next, units);
                }

                return next;
            }), prev?.vatRate),
        }));
    };

    const addInvoiceRow = () => {
        markStale();
        const tempId = `temp_${Date.now()}`;

        setInvoice((prev) => {
            const existingRows = prev?.invoiceRows ?? [];
            const maxSortOrder = existingRows.reduce((max, row) => {
                const sortOrder = Number(row?.sortOrder);
                return Number.isFinite(sortOrder) ? Math.max(max, sortOrder) : max;
            }, 0);

            return {
                ...prev,
                invoiceRows: applyInvoiceDerivedRows([
                    ...existingRows,
                    {
                        tempId,
                        text: '',
                        weight: null,
                        nrOf: null,
                        unitPrice: null,
                        sum: null,
                        accountNr: '',
                        costCenter: '',
                        vatGround: true,
                        unitId: null,
                        calculate: true,
                        sortOrder: maxSortOrder + 1,
                    },
                ], prev?.vatRate),
            };
        });
    };

    const removeInvoiceRow = (idOrTempId) => {
        markStale();

        setInvoice((prev) => ({
            ...prev,
            invoiceRows: applyInvoiceDerivedRows((prev?.invoiceRows ?? []).filter((row) => getInvoiceRowIdentity(row) !== idOrTempId), prev?.vatRate),
        }));
    };

    const captureRowPositions = () => {
        rowPositionsRef.current = new Map(
            [...rowElementsRef.current].map(([rowId, element]) => [rowId, element.getBoundingClientRect().top]),
        );
        rowElementsRef.current.forEach((element) => {
            element.getAnimations().forEach((animation) => animation.cancel());
        });
    };

    const moveInvoiceRow = (sourceId, targetId) => {
        if (sourceId === targetId) return;
        captureRowPositions();
        markStale();
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setInvoice((prev) => prev ? {
            ...prev,
            invoiceRows: reorderInvoiceRows(prev.invoiceRows, sourceId, targetId),
        } : prev);
    };

    const buildInvoicePayload = (source) => {
        const invoiceRows = Array.isArray(source?.invoiceRows)
            ? source.invoiceRows.map((row) => ({
                accountNr: row?.accountNr ?? null,
                calculate: Boolean(row?.calculate),
                compareWithOrder: Boolean(row?.compareWithOrder),
                costCenter: row?.costCenter ?? null,
                deliveryFromStockId: parseNullableInt(row?.deliveryFromStockId),
                deliveryToCustomerId: parseNullableInt(row?.deliveryToCustomerId),
                doNotAggregateWithParent: Boolean(row?.doNotAggregateWithParent),
                invoiceRowNumber: row?.invoiceRowNumber ?? row?.sortOrder ?? null,
                nrOf: parseNullableNumber(row?.nrOf),
                oldDbId: parseNullableInt(row?.oldDbId),
                orderCostId: parseNullableInt(row?.orderCostId),
                priceKey: row?.priceKey ?? null,
                priceTypeId: parseNullableInt(row?.priceTypeId),
                sortOrder: row?.sortOrder ?? null,
                sum: parseNullableNumber(row?.sum),
                text: row?.text ?? null,
                text2: row?.text2 ?? null,
                unitId: parseNullableInt(row?.unitId),
                unitPrice: parseNullableNumber(row?.unitPrice),
                vatGround: Boolean(row?.vatGround),
                weight: parseNullableNumber(row?.weight),
            }))
            : [];

        const invoiceAccountRows = Array.isArray(source?.invoiceAccountRows)
            ? source.invoiceAccountRows.map((row) => ({
                accountNr: row?.accountNr ?? null,
                costCenter: row?.costCenter ?? null,
                nrOf: parseNullableNumber(row?.nrOf),
                oldDbId: parseNullableInt(row?.oldDbId),
                sum: parseNullableNumber(row?.sum),
                text: row?.text ?? null,
                unitPrice: parseNullableNumber(row?.unitPrice),
            }))
            : [];

        return {
            account: source?.account ?? null,
            accountDate: source?.accountDate ?? null,
            address: source?.address ?? null,
            address2: source?.address2 ?? null,
            collectInvoice: Boolean(source?.collectInvoice),
            country: source?.country ?? null,
            created: source?.created ?? null,
            createdBy: parseNullableInt(source?.createdBy),
            creditingInvoiceNumber: parseNullableInt(source?.creditingInvoiceNumber),
            customerId: parseNullableInt(source?.customerId),
            customerName: source?.customerName ?? null,
            customerOrderId: parseNullableInt(source?.customerOrderId),
            costCenter: source?.costCenter ?? null,
            deliveryFromStockId: parseNullableInt(source?.deliveryFromStockId),
            deliveryToCustomerId: parseNullableInt(source?.deliveryToCustomerId),
            edited: source?.edited ?? null,
            editedBy: parseNullableInt(source?.editedBy),
            endInvoiced: Boolean(source?.endInvoiced),
            inventoryCurrencyRate: parseNullableNumber(source?.inventoryCurrencyRate),
            invoiceDate: source?.invoiceDate ?? null,
            invoiceDays: parseNullableInt(source?.invoiceDays),
            invoiceNumber: parseNullableInt(source?.invoiceNumber),
            invoiceOk: Boolean(source?.invoiceOk),
            invoiceTypeCode: source?.invoiceTypeCode ?? null,
            isSettled: Boolean(source?.isSettled),
            journalNr: parseNullableInt(source?.journalNr),
            languageId: parseNullableInt(source?.languageId),
            noteInternal: source?.noteInternal ?? null,
            oldDbId: parseNullableInt(source?.oldDbId),
            ourOrderNrFreeInvoice: source?.ourOrderNrFreeInvoice ?? null,
            ourReference: source?.ourReference ?? null,
            postalAddress: source?.postalAddress ?? null,
            postalNr: source?.postalNr ?? null,
            printed: Boolean(source?.printed),
            salesCurrencyRate: parseNullableNumber(source?.salesCurrencyRate),
            salesCurrencyId: parseNullableInt(source?.salesCurrencyId),
            termsOfPayment: source?.termsOfPayment ?? null,
            termsOfDelivery: source?.termsOfDelivery ?? null,
            timeOfDelivery: source?.timeOfDelivery ?? null,
            unitId: parseNullableInt(source?.unitId),
            vatNr: source?.vatNr ?? null,
            yourOrderNr: source?.yourOrderNr ?? null,
            yourReference: source?.yourReference ?? null,
            invoiceRows,
            invoiceAccountRows,
        };
    };

    const getPdf = async ({ ignoreUnsaved = false } = {}) => {
        if (!invoice?.id) return;

        if (!ignoreUnsaved && hasUnsavedChanges()) {
            setUnsavedWarningReason('print');
            setShowUnsavedWarning(true);
            return;
        }

        openPdfPreview('');

        try {
            const response = await apiClient.get(`/pdf/invoice/${invoice.id}`, {
                responseType: 'blob',
            });
            const contentDisposition = response.headers['content-disposition'];
            const apiFileName = getFileNameFromContentDisposition(contentDisposition)
                || `Faktura ${invoice.invoiceNumber ?? invoice.id}.pdf`;
            const blob = response.data;
            const url = URL.createObjectURL(blob);
            openPdfPreview(url, apiFileName, () => setMailLogRevision((value) => value + 1), { invoiceId: invoice.id }, { itemType: 'INVOICE', itemId: invoice.id });
        } catch (error) {
            console.error('Failed to get invoice PDF:', error);
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error'),
                { type: 'error', text: 'Kunde inte skriva ut fakturan. PDF endpoint saknas eller kunde inte laddas.' },
            ]);
        }
    };

    const handleCreateCredit = () => {
        if (hasUnsavedChanges() || customerSelectionPending) {
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'warning'),
                { type: 'warning', text: 'Spara fakturan innan du skapar en kreditfaktura.' },
            ]);
            return;
        }
        if (!invoice.invoiceNumber) {
            setMessages((prev) => [...prev, { type: 'error', text: 'Fakturan saknar fakturanummer och kan inte krediteras.' }]);
            return;
        }
        const draftWindow = window.open('', '_blank');
        if (!draftWindow) {
            setMessages((prev) => [...prev, { type: 'error', text: 'Kunde inte öppna kreditfakturan. Tillåt popupfönster och försök igen.' }]);
            return;
        }
        try {
            const draftKey = `invoice-draft-${Date.now()}-${crypto.randomUUID()}`;
            window.localStorage.setItem(draftKey, JSON.stringify(createCreditInvoiceDraft(invoice)));
            draftWindow.location.href = `/finance/invoice/new?draftKey=${encodeURIComponent(draftKey)}&openedInNewTab=1`;
        } catch (error) {
            draftWindow.close();
            console.error('Failed to create credit invoice draft:', error);
            setMessages((prev) => [...prev, { type: 'error', text: 'Kunde inte skapa kreditfakturan.' }]);
        }
    };

    const handleAdjustStockDeliveries = () => {
        if (hasUnsavedChanges()) {
            setMessages((prev) => [
                ...prev.filter((message) => message.type !== 'warning'),
                { type: 'warning', text: 'Spara kreditfakturan innan du justerar leveranserna.' },
            ]);
            return;
        }
        setShowStockDeliveryAdjustment(true);
    };

    const handleSave = async () => {
        if (!invoice) return;

        if (customerSelectionPending) {
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'success' && msg.type !== 'warning'),
                { type: 'warning', text: 'Välj en kund i sökresultatet innan du sparar fakturan.' },
            ]);
            return;
        }

        const invoiceDays = parseNullableInt(invoice?.invoiceDays);
        if (invoiceDays != null && (invoiceDays < 0 || invoiceDays > MAX_INVOICE_DAYS)) {
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'success' && msg.type !== 'error' && msg.type !== 'warning'),
                { type: 'error', text: `Fakt.dagar måste vara mellan 0 och ${MAX_INVOICE_DAYS}.` },
            ]);
            return;
        }

        try {
            const payload = buildInvoicePayload(invoice);
            const invoiceId = parseNullableInt(invoice?.id);
            const isCreatingNew = !(invoiceId && invoiceId > 0);

            const response = invoiceId && invoiceId > 0
                ? await apiClient.put(`/invoices/${invoiceId}`, payload)
                : await apiClient.post('/invoices', payload);

            const savedInvoice = normalizeInvoiceForEditor(response.data);
            setInvoice(savedInvoice ?? null);
            setOriginalInvoice(structuredClone(savedInvoice));
            clearStale();
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'success' && msg.type !== 'error' && msg.type !== 'warning'),
                { type: 'success', text: 'Fakturan sparades.' },
            ]);

            if (isCreatingNew && savedInvoice?.id) {
                createdInvoiceIdRef.current = savedInvoice.id;
                setNewlyCreatedInvoiceId(savedInvoice.id);
                if (draftKey) {
                    window.localStorage.removeItem(draftKey);
                }
                skipUnsavedCheckRef.current = true;
                navigate(`/finance/invoice/${savedInvoice.id}`, {
                    replace: true,
                    state: { newlyCreatedInvoiceId: savedInvoice.id },
                });
                if (hasCreditStockDeliveries(savedInvoice)) {
                    setShowStockDeliveryAdjustment(true);
                }
            }
        } catch (error) {
            console.error('Failed to save invoice:', error);

            const status = error?.response?.status;
            const data = error?.response?.data;
            const serverMessage = typeof data === 'string'
                ? data
                : data?.message;
            const validationText = data?.errors && typeof data.errors === 'object'
                ? Object.entries(data.errors)
                    .filter(([field]) => field !== 'request')
                    .map(([field, fieldErrors]) => `${field.replace(/^\$\./, '')}: ${[].concat(fieldErrors).join(', ')}`)
                    .join(' | ')
                : '';
            const detailText = Array.isArray(data?.details)
                ? data.details
                    .map((detail) => `${detail.type}: ${detail.message}`)
                    .join(' | ')
                : validationText;

            let text = 'Kunde inte spara fakturan.';
            if (status === 404 || status === 405) {
                text = 'Sparfunktionen for faktura ar inte tillganglig i API:et an.';
            } else if (detailText) {
                text = `Kunde inte spara fakturan: ${detailText}`;
            } else if (serverMessage) {
                text = `Kunde inte spara fakturan: ${serverMessage}`;
            }

            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error' && msg.type !== 'warning'),
                { type: 'error', text },
            ]);
        }
    };

    const handleDelete = async () => {
        if (!invoice?.id) return;

        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/invoices/${invoice.id}`);
            navigate('/finance/searchinvoice');
        } catch (error) {
            console.error('Failed to delete invoice:', error);

            const status = error?.response?.status;
            const text = status === 404 || status === 405
                ? 'Raderingsfunktionen for faktura ar inte tillganglig i API:et an.'
                : 'Kunde inte radera fakturan.';

            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error'),
                { type: 'error', text },
            ]);
        }
    };

    useEffect(() => {
        let isActive = true;

        const loadPaymentTerms = async () => {
            try {
                const response = await getSharedRequest('termsofpayment:search:invoice-form', () => apiClient.get('/termsofpayment/search'));
                if (!Array.isArray(response.data)) {
                    throw new Error('Invalid payment terms response.');
                }
                if (isActive) {
                    setPaymentTerms(response.data.filter((term) => term.active && term.name?.trim()));
                }
            } catch (error) {
                console.error('Failed loading invoice payment terms:', error);
                if (!isActive) return;
                setMessages((prev) => [
                    ...prev,
                    { type: 'warning', text: 'Kunde inte ladda betalningsvillkor. Du kan fortfarande skriva villkoren manuellt.' },
                ]);
            }
        };

        void loadPaymentTerms();
        return () => {
            isActive = false;
        };
    }, [setMessages]);

    useEffect(() => {
        // The create response already contains the saved invoice and its rows.
        if (!isNewInvoice && String(createdInvoiceIdRef.current) === id) {
            createdInvoiceIdRef.current = null;
            return;
        }

        let isActive = true;

        const loadOptions = async () => {
            try {
                const [currencyRes, unitRes] = await Promise.all([
                    getSharedRequest('currencies:search:invoice-form', () => apiClient.post('/currencies/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    })),
                    getSharedRequest('units:search:invoice-form', () => apiClient.post('/units/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    })),
                ]);

                if (!isActive) return;
                setCurrencies(Array.isArray(currencyRes?.data?.items) ? currencyRes.data.items : []);
                setUnits(Array.isArray(unitRes?.data?.items) ? unitRes.data.items : []);
            } catch (error) {
                console.error('Failed loading invoice options:', error);
                if (!isActive) return;
                setCurrencies([]);
                setUnits([]);
            }
        };

        const loadInvoice = async () => {
            setLoading(true);

            try {
                await loadOptions();

                if (isNewInvoice) {
                    if (draftKey) {
                        const rawDraft = window.localStorage.getItem(draftKey);
                        if (rawDraft) {
                            const parsedDraft = JSON.parse(rawDraft);
                            const normalizedDraft = normalizeInvoiceForEditor(parsedDraft);
                            setInvoice(normalizedDraft);
                            setOriginalInvoice(structuredClone(normalizedDraft));
                        } else {
                            const emptyInvoice = createNewInvoiceModel();
                            setInvoice(emptyInvoice);
                            setOriginalInvoice(structuredClone(emptyInvoice));
                            setMessages([{ type: 'warning', text: 'Utkastet kunde inte hittas. En tom faktura öppnades i stället.' }]);
                        }
                    } else {
                        const emptyInvoice = createNewInvoiceModel();
                        setInvoice(emptyInvoice);
                        setOriginalInvoice(structuredClone(emptyInvoice));
                    }
                } else {
                    const response = await getSharedRequest(`invoices:${id}`, () => apiClient.get(`/invoices/${id}`));
                    if (!isActive) return;
                    const data = response.data;

                    const normalized = normalizeInvoiceForEditor(data);

                    setInvoice(normalized ?? null);
                    setOriginalInvoice(structuredClone(normalized));
                }

                if (isActive) {
                    clearStale();
                }
            } catch (error) {
                console.error('Failed to load invoice:', error);
                if (!isActive) return;
                setInvoice(null);
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        void loadInvoice();
        return () => {
            isActive = false;
        };
    }, [draftKey, id, isNewInvoice]);

    const totals = useMemo(() => {
        const rows = invoice?.invoiceRows ?? [];

        const vatRow = rows.find((row) => Number(row?.priceTypeId) === VAT_PRICE_TYPE_ID);
        const roundingRow = rows.find((row) => Number(row?.priceTypeId) === ROUNDING_PRICE_TYPE_ID);

        const hasVatRow = Boolean(vatRow);
        const hasRoundingRow = Boolean(roundingRow);

        let sumExVat = 0;
        let vatFromBase = 0;
        let rounding = 0;

        rows.forEach((row) => {
            const priceTypeId = Number(row?.priceTypeId);
            const rowSum = parseNullableNumber(row?.sum) ?? 0;

            if (priceTypeId === VAT_PRICE_TYPE_ID) {
                return;
            }

            if (priceTypeId === ROUNDING_PRICE_TYPE_ID) {
                rounding = rowSum;
                return;
            }

            sumExVat += rowSum;
            if (row?.vatGround) {
                vatFromBase += rowSum * (invoice?.vatRate ?? 0.25);
            }
        });

        const vat = hasVatRow
            ? (parseNullableNumber(vatRow?.sum) ?? 0)
            : vatFromBase;

        const vatAccount = hasVatRow ? vatRow?.accountNr ?? '' : '';

        const roundingValue = hasRoundingRow
            ? (parseNullableNumber(roundingRow?.sum) ?? 0)
            : rounding;

        return {
            sumExVat,
            vat,
            roundingValue,
            sumInclVat: sumExVat + vat + roundingValue,
            vatAccount,
        };
    }, [invoice?.invoiceRows, invoice?.vatRate]);

    const visibleInvoiceRows = useMemo(() => (
        (rowDrag?.rows ?? invoice?.invoiceRows ?? []).filter((row) => {
            const priceTypeId = Number(row?.priceTypeId);
            return priceTypeId !== VAT_PRICE_TYPE_ID && priceTypeId !== ROUNDING_PRICE_TYPE_ID;
        })
    ), [invoice?.invoiceRows, rowDrag?.rows]);

    useLayoutEffect(() => {
        const previousPositions = rowPositionsRef.current;
        rowPositionsRef.current = null;
        if (!previousPositions || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        rowElementsRef.current.forEach((element, rowId) => {
            const previousTop = previousPositions.get(rowId);
            if (previousTop == null) return;
            const offset = previousTop - element.getBoundingClientRect().top;
            if (Math.abs(offset) < 1) return;
            element.animate(
                [{ transform: `translateY(${offset}px)` }, { transform: 'translateY(0)' }],
                { duration: 180, easing: 'ease-out' },
            );
        });
    }, [visibleInvoiceRows]);

    if (loading) {
        return (
            <div className="pl-10 space-y-4 pt-1 ml-15 mr-20">
                <Skeleton height={30} width={420} className="mb-5" />
                <Skeleton height={200} />
                <Skeleton height={290} />
                <Skeleton height={80} />
            </div>
        );
    }

    if (!invoice) {
        return (
            <div className="pt-2 text-sm text-red-700">
                Kunde inte ladda fakturan.
                <button
                    type="button"
                    onClick={() => navigate('/finance/searchinvoice')}
                    className="ml-4 rounded-sm border border-gray-300 bg-white px-3 py-1 text-xs text-gray-700 hover:bg-gray-100"
                >
                    Tillbaka
                </button>
            </div>
        );
    }

    const dueDate = invoice?.invoiceDate && invoice?.invoiceDays != null
        ? new Date(new Date(invoice.invoiceDate).getTime() + (Number(invoice.invoiceDays) * 24 * 60 * 60 * 1000))
        : null;
    const relations = creditRelations?.invoiceId === invoice.id ? creditRelations : null;
    const creditedInvoice = invoice.id > 0 ? relations?.creditedInvoice : (
        invoice.creditedInvoiceId ? { id: invoice.creditedInvoiceId, invoiceNumber: invoice.creditingInvoiceNumber } : null
    );
    const relatedCustomerOrders = invoiceCustomerOrders?.invoiceId === invoice.id
        ? invoiceCustomerOrders.orders
        : null;
    const invoicingInfoEntries = (relatedCustomerOrders ?? []).filter((order) =>
        typeof order.invoicingInfo === 'string' && order.invoicingInfo.trim().length > 0);
    const invoicingInfoSummary = invoicingInfoEntries.map((order) => order.invoicingInfo.trim());
    const invoiceReference = (reference) => (
        <a href={`/finance/invoice/${reference.id}?openedInNewTab=1`} target="_blank" rel="noopener noreferrer"
            className="underline hover:text-red-700">
            {reference.invoiceNumber}
        </a>
    );

    return (
        <div className="relative flex min-h-full flex-col">
            {showCustomerOrders && (
                <InvoiceCustomerOrdersModal invoiceId={invoice.id} onClose={() => setShowCustomerOrders(false)}
                    onSaved={() => setMessages((prev) => [...prev.filter((message) => message.type !== 'success'), { type: 'success', text: 'Ordererkännandena uppdaterades.' }])}
                />
            )}
            {showInvoicingInfo && relatedCustomerOrders && (
                <InvoiceInvoicingInfoModal
                    orders={relatedCustomerOrders}
                    onClose={() => setShowInvoicingInfo(false)}
                />
            )}
            {showStockDeliveryAdjustment && invoice.id > 0 && (
                <InvoiceStockDeliveryAdjustmentModal invoiceId={invoice.id} onClose={() => setShowStockDeliveryAdjustment(false)}
                    onAdjusted={() => setMessages((prev) => [
                        ...prev.filter((message) => message.type !== 'success'),
                        { type: 'success', text: 'Lagerleveranserna justerades.' },
                    ])}
                />
            )}
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA FAKTURA"
                message={`Är du saker på att du vill radera faktura ${invoice.invoiceNumber ?? invoice.id}? Åtgarden kan inte Ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparande ändringar"
                message={
                    unsavedWarningReason === 'print'
                        ? 'Du har osparande ändringar. Vill du skriva ut utan att spara?'
                        : 'Det finns ej sparande ändringar, vill du ändå fortsätta?'
                }
                confirmText={unsavedWarningReason === 'print' ? 'Skriv ut anda' : 'Fortsätt ändå'}
                cancelText="Avbryt"
                isDestructive={false}
            />

            <OrderDetailLayout
                title={invoice?.id ? (
                    <>Faktura <span
                        className={`ml-2 inline-block rounded-sm ${!isNewInvoice && newlyCreatedInvoiceId === invoice.id ? 'invoice-number-created' : ''}`}
                        onAnimationEnd={(event) => {
                            if (event.animationName === 'invoice-number-highlight') {
                                setNewlyCreatedInvoiceId(null);
                            }
                        }}
                    >{invoice.invoiceNumber ?? invoice.id}</span></>
                ) : (
                    'Ny faktura'
                )}
                messages={messages}
                isInfoPanelExpanded={isInfoPanelExpanded}
                isInfoPanelUsingResponsiveDefault={isInfoPanelUsingResponsiveDefault}
                onToggleInfoPanel={toggleInfoPanel}
                mainMaxWidthClass=""
                infoContent={(
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-500">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderInvoiceMetaRow('Skapad:', invoice?.created, invoice?.createdByUserName)}
                            {renderInvoiceMetaRow('Redigerad:', invoice?.edited, invoice?.editedByUserName)}
                            <div className="grid grid-cols-21 gap-1 mt-4 mx-2">
                                <div className="col-span-5"><span className="font-medium">Fakt.nr:</span></div>
                                <div className="col-span-16">
                                    {invoice.invoiceNumber}
                                    {Number(invoice.invoiceTypeCode) === CREDIT_INVOICE_TYPE_ID && (
                                        <span className="ml-3 text-red-500">
                                            KREDIT, krediterar {creditedInvoice ? invoiceReference(creditedInvoice) : invoice.creditingInvoiceNumber}
                                        </span>
                                    )}
                                    {relations?.creditInvoices.length > 0 && (
                                        <span className="ml-3 text-red-500">
                                            Krediterad av{' '}
                                            {relations.creditInvoices.map((credit, index) => (
                                                <React.Fragment key={credit.id}>
                                                    {index > 0 && ', '}{invoiceReference(credit)}
                                                </React.Fragment>
                                            ))}
                                        </span>
                                    )}
                                </div>
                            </div>
                            {creditRelationsError === invoice.id && (
                                <div role="alert" className="mx-2 text-red-700">
                                    Kunde inte hämta kreditinformation.{' '}
                                    <button type="button" className="underline" onClick={() => setCreditRelationsRetry((value) => value + 1)}>
                                        Försök igen
                                    </button>
                                </div>
                            )}
                            {invoice.invoiceDate && (
                                <div className="grid grid-cols-21 gap-1 mx-2 mt-4 items-center">
                                    <div className="col-span-5"><span className="font-medium">Fakt.dag:</span></div>
                                    <div className="col-span-8">
                                        {isEditingInvoiceDate ? (
                                            <LabeledDatePicker
                                                inputAriaLabel="Fakturadatum"
                                                portal
                                                value={invoice.invoiceDate}
                                                onChange={(value) => {
                                                    if (toDateInputValue(value) !== toDateInputValue(invoice.invoiceDate)) {
                                                        handleChange('invoiceDate', value);
                                                    }
                                                    setIsEditingInvoiceDate(false);
                                                }}
                                                clearable={false}
                                                required
                                                margintop="0"
                                            />
                                        ) : toDateInputValue(invoice.invoiceDate)}
                                    </div>
                                    <div className="col-span-8">
                                        <button
                                            type="button"
                                            onClick={() => setIsEditingInvoiceDate((current) => !current)}
                                            aria-label={isEditingInvoiceDate ? 'Avbryt ändring av fakturadatum' : 'Ändra fakturadatum'}
                                            title={isEditingInvoiceDate ? 'Avbryt' : 'Ändra fakturadatum'}
                                            className="inline-flex items-center text-gray-400 hover:text-gray-700"
                                        >
                                            <Pencil size={13} aria-hidden="true" />
                                        </button>
                                    </div>
                                </div>
                            )}
                            {dueDate && (
                                <div className="grid grid-cols-21 gap-1 mx-2">
                                    <div className="col-span-5"><span className="font-medium">Förfaller:</span></div>
                                    <div className="col-span-8">{dueDate.toLocaleDateString('sv-SE')}</div>
                                </div>
                            )}
                            <div className="grid grid-cols-21 gap-1 mx-2 mt-4 items-center">
                                <div className="col-span-5"><span className="font-medium">Bokförd:</span></div>
                                <div className="col-span-8">
                                    <span className={`inline-flex items-center rounded-full px-2 py-[4px] text-tiny text-white ${invoice.accountDate ? 'bg-lime-600' : 'bg-red-400'}`}>
                                        {invoice.accountDate ? toDateInputValue(invoice.accountDate) : 'Ej bokförd'}
                                    </span>
                                </div>
                            </div>
                            <div className="mr-2 mt-4">
                                <div className=" ml-2 mb-1 font-medium">Faktureringsinfo</div>
                                {relatedCustomerOrders === null ? (
                                    invoiceCustomerOrdersError === invoice.id ? (
                                        <p role="alert" className="text-red-700">Kunde inte ladda faktureringsinfo.</p>
                                    ) : (
                                        <p role="status" className="text-gray-500">Laddar faktureringsinfo…</p>
                                    )
                                ) : invoicingInfoSummary.length > 0 ? (
                                    <button
                                        type="button"
                                        onClick={() => setShowInvoicingInfo(true)}
                                        aria-label="Visa faktureringsinfo per kundorder"
                                        className="w-full rounded-sm border border-gray-300 bg-white px-2 text-left text-xs text-gray-700 hover:bg-gray-50"
                                    >
                                        {invoicingInfoSummary.map((text, index) => (
                                            <span
                                                key={`${index}-${text}`}
                                                className={`block whitespace-pre-wrap break-words py-2 ${index > 0 ? '' : ''}`}
                                            >
                                                {text}
                                            </span>
                                        ))}
                                    </button>
                                ) : (
                                    <p className="rounded-sm border border-gray-300 bg-white px-2 py-2 text-gray-500">
                                        Ingen faktureringsinfo
                                    </p>
                                )}
                                {invoiceCustomerOrdersError === invoice.id && (
                                    <button
                                        type="button"
                                        onClick={() => setInvoiceCustomerOrdersRetry((value) => value + 1)}
                                        className="mt-1 text-[11px] text-red-700 underline"
                                    >
                                        Försök igen
                                    </button>
                                )}
                            </div>
                        </div>
                    </div>
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
                        {invoice?.id !== 0 && (
                            <ActionButton
                                label="Skriv ut"
                                icon={Printer}
                                onClick={getPdf}
                                accent="sky"
                            />
                        )}
                        <div className="flex items-center gap-10 ml-20">
                            {invoice?.id > 0 && Number(invoice.invoiceTypeCode) !== CREDIT_INVOICE_TYPE_ID && (
                                <ActionButton label="Kreditera" icon={FileMinus} onClick={handleCreateCredit} accent="sky" />
                            )}
                            {invoice?.id > 0 && hasCreditStockDeliveries(invoice) && (
                                <ActionButton label="Justera leveranser" icon={ClipboardCheck} onClick={handleAdjustStockDeliveries} accent="sky" />
                            )}
                            {invoice?.id > 0 && (
                                <ActionButton label="Klara OE" icon={ClipboardCheck} onClick={() => setShowCustomerOrders(true)} accent="lime" />
                            )}
                            {invoice?.id > 0 && <MailLog key={invoice.id} itemType="invoice" itemId={invoice.id} refreshKey={mailLogRevision} />}
                        </div>
                    </div>

                    <div className="flex items-center gap-6">
                        {invoice?.id !== 0 && (
                            <ActionButton
                                label="Radera"
                                icon={Trash2}
                                onClick={() => setShowDeleteConfirm(true)}
                                accent="rose"
                            />
                        )}
                    </div>
                </div>

                <div className="mt-8 flex flex-wrap items-start gap-x-15 gap-y-8">
                    <div className="w-[350px] max-w-full min-w-0">
                        <div className="flex items-center mb-[1px]">
                            <span className="w-20 shrink-0 text-xs text-gray-500">Kund</span>
                            <SearchCustomer
                                key={`${invoice?.id}-${invoice?.customerId ?? ''}-${invoice?.customerName ?? ''}`}
                                selectedCustomerName={invoice?.customerName ?? ''}
                                selectedCustomerId={invoice?.customerId}
                                disabled={customerSelectionDisabled}
                                onCustomerSelect={handleCustomerSelect}
                                onPendingSelectionChange={setCustomerSelectionPending}
                                width="w-full"
                                className="min-w-0 flex-1"
                            />
                        </div>
                        <LabeledInput
                            label="Adress"
                            value={invoice?.address || ''}
                            onChange={(value) => handleChange('address', value)}
                            labelWidth="w-20"
                            margintop="2"
                        />
                        <LabeledInput
                            label=""
                            value={invoice?.address2 || ''}
                            onChange={(value) => handleChange('address2', value)}
                            labelWidth="w-20"
                            margintop="0"
                        />
                        <div className='flex space-x-1 w-full mt-[0px]'>
                            <LabeledInput
                                label="Postnr"
                                value={invoice?.postalNr || ''}
                                onChange={(value) => handleChange('postalNr', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <input
                                name="city"
                                value={invoice?.postalAddress || ''}
                                onChange={(e) => handleChange('postalAddress', e.target.value)}
                                className='text-xs w-2/3 border border-gray-300 rounded-sm mb-[1px] px-2 py-1 focus:outline-none bg-white' />
                        </div>
                        <LabeledInput
                            label="Land"
                            value={invoice?.country || ''}
                            onChange={(value) => handleChange('country', value)}
                            labelWidth="w-20"
                            margintop="0"
                        />
                        <LabeledInput
                            label="Momsreg.nr"
                            value={invoice?.vatNr || ''}
                            onChange={(value) => handleChange('vatNr', value)}
                            labelWidth="w-20"
                            margintop="0"
                        />
                    </div>
                    <div className="w-[300px] max-w-full min-w-0">
                        <LabeledInput
                            label="Vår referens"
                            value={invoice?.ourReference || ''}
                            onChange={(value) => handleChange('ourReference', value)}
                            labelWidth="w-25"
                            margintop="0"
                        />
                        <LabeledInput
                            label="Er referens"
                            value={invoice?.yourReference || ''}
                            onChange={(value) => handleChange('yourReference', value)}
                            labelWidth="w-25"
                            margintop="0"
                        />
                        <LabeledInput
                            label="Ert ordernr."
                            value={invoice?.yourOrderNr || ''}
                            onChange={(value) => handleChange('yourOrderNr', value)}
                            labelWidth="w-25"
                            margintop="0"
                        />
                        <LabeledTextArea
                            label="Vårt ordernr."
                            value={invoice?.ourOrderNrFreeInvoice || ''}
                            onChange={(value) => handleChange('ourOrderNrFreeInvoice', value)}
                            labelWidth="w-25"
                            margintop="0"
                            height="h-12"
                        />
                        <LabeledDatePicker
                            label="Lev.datum"
                            value={invoice?.timeOfDelivery || ''}
                            onChange={(value) => handleChange('timeOfDelivery', value)}
                            labelWidth="w-25"
                            valueType="input"
                            margintop="0"
                        />
                    </div>
                    <div className="w-[150px] max-w-full min-w-0">
                        <LabeledSelect
                            label="Valuta"
                            value={invoice?.salesCurrencyId || ''}
                            onChange={(value) => handleChange('salesCurrencyId', value)}
                            labelWidth="w-20"
                            margintop="0"
                            items={currencies}
                            disabled={currencies.length === 0}
                        />
                        <LabeledInput
                            label="Fakt.dagar"
                            value={invoice?.invoiceDays ?? ''}
                            onChange={(value) => handleChange('invoiceDays', value)}
                            labelWidth="w-20"
                            margintop="0"
                            type='number'
                            decimals='0'
                            integerOnly='true'
                        />
                        <LabeledInput
                            label="Konto kundf."
                            value={invoice?.account || ''}
                            onChange={(value) => handleChange('account', value)}
                            labelWidth="w-20"
                            margintop="0"
                        />
                        <LabeledInput
                            label="Konto moms"
                            value={totals?.vatAccount || ''}
                            onChange={(value) => handleChange('vatAccount', value)}
                            labelWidth="w-20"
                            margintop="0"
                        />
                        <LabeledSwitch
                            field="invoiceOk"
                            name="invoiceOk"
                            label="Godkänd for bokf."
                            value={Boolean(invoice?.invoiceOk)}
                            onChange={(rowId, field, checked) => handleChange(field, checked)}
                            labelWidth="w-20"
                            marginTop={0}
                        />
                    </div>
                    <div className="min-w-0 basis-[150px] grow">
                        <LabeledTextArea
                            label="Bet.villkor"
                            value={invoice?.termsOfPayment || ''}
                            onChange={(value) => handleChange('termsOfPayment', value)}
                            popupItems={paymentTerms}
                            popupOnSelect={(term) => {
                                handleChange('termsOfPayment', term.name ?? '');
                                handleChange('invoiceDays', term.paymentDays ?? null);
                            }}
                            labelWidth="w-12"
                            margintop="0"
                            height="h-20"
                        />
                        <LabeledInput
                            label="Lev.villkor"
                            value={invoice?.termsOfDelivery || ''}
                            onChange={(value) => handleChange('termsOfDelivery', value)}
                            labelWidth="w-19"
                            margintop="0"
                        />
                        <LabeledTextArea
                            label="Anteckning"
                            value={invoice?.noteInternal || ''}
                            onChange={(value) => handleChange('noteInternal', value)}
                            labelWidth="w-19"
                            margintop="0"
                            height="h-10"
                        />
                    </div>
                </div>

                <div className="mt-8 flex items-start gap-6 pb-20">
                    <div className="min-w-0 flex-1">
                        <table className="w-full table-fixed text-xs border-collapse" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <thead>
                                <tr className="border-b border-gray-300">
                                    <th className="w-[3%] py-2 text-center text-tiny font-medium text-gray-400">Sort</th>
                                    <th className="w-[32%] px-4 py-2 text-left text-tiny font-medium text-gray-400">Text</th>
                                    <th className="w-[7%] pr-2 py-2 text-right text-tiny font-medium text-gray-400">Vikt</th>
                                    <th className="w-[7%] pr-2 py-2 text-right text-tiny font-medium text-gray-400">Antal</th>
                                    <th className="w-[8%] pr-2 py-2 text-right text-tiny font-medium text-gray-400">APris</th>
                                    <th className="w-[8%] pr-4 py-2 text-right text-tiny font-medium text-gray-400">Enhet</th>
                                    <th className="w-[9%] pr-2 py-2 text-right text-tiny font-medium text-gray-400">Summa</th>
                                    <th className="w-[8%] pr-2 py-2 text-right text-tiny font-medium text-gray-400">Konto</th>
                                    <th className="w-[5%] px-2 py-2 text-center text-tiny font-medium text-gray-400">Moms</th>
                                    <th className="w-[7%] px-2 py-2 text-center text-tiny font-medium text-gray-400">Atgard</th>
                                </tr>
                            </thead>
                            <tbody className="bg-white">
                                {visibleInvoiceRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={10} className="px-2 py-4 text-center text-gray-400">Inga rader registrerade.</td>
                                    </tr>
                                ) : (
                                    visibleInvoiceRows.map((row, index) => {
                                        const idOrTempId = getInvoiceRowIdentity(row);

                                        return (
                                            <tr
                                                key={idOrTempId}
                                                ref={(element) => {
                                                    if (element) rowElementsRef.current.set(idOrTempId, element);
                                                    else rowElementsRef.current.delete(idOrTempId);
                                                }}
                                                className={`border-b border-gray-100 ${rowDrag?.sourceId === idOrTempId ? 'opacity-50 bg-lime-50 outline outline-1 outline-lime-500' : ''}`}
                                                onDragOver={(event) => {
                                                    if (!rowDrag) return;
                                                    event.preventDefault();
                                                    event.dataTransfer.dropEffect = 'move';
                                                    if (rowDrag.sourceId === idOrTempId) return;
                                                    if (event.currentTarget.getAnimations().some((animation) => animation.playState === 'running')) return;
                                                    const sourceIndex = visibleInvoiceRows.findIndex((item) => getInvoiceRowIdentity(item) === rowDrag.sourceId);
                                                    const bounds = event.currentTarget.getBoundingClientRect();
                                                    const midpoint = bounds.top + bounds.height / 2;
                                                    if (sourceIndex < index && event.clientY < midpoint) return;
                                                    if (sourceIndex > index && event.clientY > midpoint) return;
                                                    captureRowPositions();
                                                    setRowDrag({
                                                        ...rowDrag,
                                                        rows: reorderInvoiceRows(rowDrag.rows, rowDrag.sourceId, idOrTempId),
                                                    });
                                                }}
                                                onDrop={(event) => {
                                                    if (!rowDrag) return;
                                                    event.preventDefault();
                                                    if (rowDrag.rows !== invoice.invoiceRows) {
                                                        markStale();
                                                        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
                                                        setInvoice((prev) => ({ ...prev, invoiceRows: rowDrag.rows }));
                                                    }
                                                    setRowDrag(null);
                                                }}
                                            >
                                                <td className="py-0 text-xs text-center text-gray-600">
                                                    {row.sortOrder}
                                                </td>
                                                <td className="p-0">
                                                    <textarea
                                                        value={row.text ?? ''}
                                                        onChange={(e) => {
                                                            handleInvoiceRowChange(idOrTempId, 'text', e.target.value);
                                                            autoResizeTextarea(e.target);
                                                        }}
                                                        onInput={(e) => autoResizeTextarea(e.target)}
                                                        ref={(element) => autoResizeTextarea(element)}
                                                        rows={1}
                                                        className="w-full h-full text-xs mb-[-4px] px-3 py-1 bg-transparent border-0 focus:outline-none focus:bg-white focus:border focus:border-blue-400 resize-none overflow-hidden leading-4"
                                                    />
                                                </td>
                                                <td className="p-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="weight"
                                                            value={row.weight ?? ''}
                                                            decimals={Number(row.priceTypeId) === 2 ? 5 : 0}
                                                            hideZeroDecimals
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="p-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="nrOf"
                                                            value={Number(row.priceTypeId) === 2 && Number(row.nrOf) === -1 ? '' : row.nrOf ?? ''}
                                                            disabled={Number(row.priceTypeId) === 2 && Number(row.nrOf) === -1}
                                                            decimals={0}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="unitPrice"
                                                            value={row.unitPrice ?? ''}
                                                            decimals={Number(row.priceTypeId) === 2 ? 5 : 2}
                                                            hideZeroDecimals={Number(row.priceTypeId) === 2}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <select
                                                            value={row.unitId || ''}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'unitId', e.target.value)}
                                                            className="w-full h-full text-xs text-right border-0 focus:outline-none"
                                                        >
                                                            <option value=""></option>
                                                            {units.map((unit) => (
                                                                <option key={unit.id} value={unit.id}>{unit.name}</option>
                                                            ))}
                                                        </select>
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="sum"
                                                            value={row.sum ?? ''}
                                                            decimals={2}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <input
                                                            value={row.accountNr ?? ''}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'accountNr', e.target.value)}
                                                            className="w-full h-full text-xs pr-2 border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 flex items-center justify-center border border-transparent bg-white focus-within:border-blue-400">
                                                        <SelectCircleCheckbox
                                                            checked={Boolean(row.vatGround)}
                                                            onChange={() => handleInvoiceRowChange(idOrTempId, 'vatGround', !row.vatGround)}
                                                            ariaLabel="Momsgrund"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 flex items-center justify-center gap-1 border border-transparent bg-white focus-within:border-blue-400">
                                                        <button
                                                            type="button"
                                                            onClick={() => removeInvoiceRow(idOrTempId)}
                                                            aria-label={`Ta bort fakturarad ${index + 1}`}
                                                            title="Ta bort rad"
                                                            className="rounded p-1 text-red-600 hover:bg-red-50 focus-visible:outline focus-visible:outline-blue-400"
                                                        >
                                                            <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                                                        </button>
                                                        <button
                                                            type="button"
                                                            draggable={visibleInvoiceRows.length > 1}
                                                            disabled={visibleInvoiceRows.length < 2}
                                                            aria-label={`Flytta fakturarad ${index + 1}`}
                                                            aria-keyshortcuts="Alt+ArrowUp Alt+ArrowDown"
                                                            title="Dra för att flytta raden. Alt + pil upp/ned flyttar med tangentbordet."
                                                            className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-700 cursor-grab active:cursor-grabbing focus-visible:outline focus-visible:outline-blue-400 disabled:opacity-30 disabled:cursor-default"
                                                            onDragStart={(event) => {
                                                                event.dataTransfer.effectAllowed = 'move';
                                                                event.dataTransfer.setData('text/plain', String(idOrTempId));
                                                                setRowDrag({ sourceId: idOrTempId, rows: invoice.invoiceRows });
                                                            }}
                                                            onDragEnd={() => {
                                                                if (rowDrag) captureRowPositions();
                                                                setRowDrag(null);
                                                            }}
                                                            onKeyDown={(event) => {
                                                                if (!event.altKey || !['ArrowUp', 'ArrowDown'].includes(event.key)) return;
                                                                event.preventDefault();
                                                                const target = visibleInvoiceRows[index + (event.key === 'ArrowUp' ? -1 : 1)];
                                                                if (target) moveInvoiceRow(idOrTempId, getInvoiceRowIdentity(target));
                                                            }}
                                                        >
                                                            <GripVertical className="h-3.5 w-3.5" aria-hidden="true" />
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                                {/* Empty row for adding new entries */}
                                <tr className="border-b border-gray-100">
                                    <td className="px-2 py-0.5 text-xs text-gray-400"></td>
                                    <td colSpan="9" className="px-2 py-2">
                                        <button
                                            type="button"
                                            onClick={addInvoiceRow}
                                            className="text-xs text-gray-400 hover:text-gray-600"
                                        >
                                            + Lägg till rad
                                        </button>
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                        {/* Summary Section */}
                        {visibleInvoiceRows.length > 0 && (
                            <div className="flex justify-end mt-6">
                                <div className="border border-gray-300 bg-lime-50 px-6 py-3 rounded-sm shadow-sm">
                                    <div className="grid grid-cols-4 gap-8 text-xs">
                                        <div className="text-left">
                                            <div className="text-gray-500 font-medium mb-1">BELOPP</div>
                                            <div className=" text-gray-800 font-semibold">
                                                {totals.sumExVat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} SEK
                                            </div>
                                        </div>
                                        <div className="text-left">
                                            <div className="text-gray-500 font-medium mb-1">MOMS</div>
                                            <div className="text-gray-800 font-semibold">
                                                {totals.vat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} SEK
                                            </div>
                                        </div>
                                        <div className="text-left">
                                            <div className="text-gray-500 font-medium mb-1">ÖRESUTJ.</div>
                                            <div className="text-gray-800 font-semibold">
                                                {totals.roundingValue.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} SEK
                                            </div>
                                        </div>
                                        <div className="text-left">
                                            <div className="text-gray-500 font-medium mb-1">ATT BETALA</div>
                                            <div className="text-gray-800 font-semibold">
                                                {totals.sumInclVat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} SEK
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>

                    <div className="w-[350px] shrink-0">
                        <table className="w-full table-fixed text-xs border-collapse" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <thead className="[&_th]:font-semibold [&_th]:text-gray-900 [&_th]:tracking-[0.12em]">
                                <tr className="border-b border-gray-300">
                                    <th className="w-[44%] px-2 py-2 text-left text-tiny font-medium text-gray-400">Bokf.rad</th>
                                    <th className="w-[26%] px-2 py-2 text-right text-tiny font-medium text-gray-400">Summa</th>
                                    <th className="w-[18%] px-2 py-2 text-right text-tiny font-medium text-gray-400">Konto</th>
                                    <th className="w-[12%] px-2 py-2 text-right text-tiny font-medium text-gray-400">Res</th>
                                </tr>
                            </thead>
                            <tbody className="bg-white">
                                {(invoice?.invoiceAccountRows ?? []).length === 0 ? (
                                    <tr>
                                        <td colSpan={4} className="px-2 py-4 text-center text-gray-400">Inga bokföringsrader.</td>
                                    </tr>
                                ) : (
                                    (invoice.invoiceAccountRows ?? []).map((row, index) => {
                                        const accountRowKey = (Number(row?.id) || 0) > 0
                                            ? row.id
                                            : (row.tempKey ?? `${row.text ?? ''}-${row.accountNr ?? ''}-${row.costCenter ?? ''}`);
                                        const sum = parseNullableNumber(row.sum) ?? 0;

                                        return (
                                            <tr key={accountRowKey} className={`border-b border-gray-100 ${index % 2 === 0 ? 'bg-white' : 'bg-gray-50'}`}>
                                                <td className="px-2 py-1 text-gray-700">{row.text ?? ''}</td>
                                                <td className="px-2 py-1 text-right text-gray-800">{sum.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                                                <td className="px-2 py-1 text-right text-gray-700">{row.accountNr ?? ''}</td>
                                                <td className="px-2 py-1 text-right text-gray-700">{row.costCenter ?? ''}</td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>

            </OrderDetailLayout>
        </div>
    );
};

export default Invoice;