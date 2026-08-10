import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import ConfirmationModal from '../../components/ConfirmationModal';
import OrderNavigationTree from '../../components/OrderNavigationTree';
import OrderCost from '../../components/OrderCost';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledTextArea from '../../components/LabeledTextArea';
import apiClient from '../../config/apiClient';
import { formatDateTime } from '../../helpers/dateUtils';
import { parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

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
    supplierOrderId: null,
    supplierOrderNr: '',
    customerId: null,
    customerName: '',
    deliveryAddressName: '',
    deliveryAddress: '',
    deliveryPostalNr: '',
    deliveryPostalAddress: '',
    deliveryCountry: '',
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

const CustomerOrder = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewCustomerOrder = id === 'new' || id == null;

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);

    const [customerOrder, setCustomerOrder] = useState(null);
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

        navigate('/order/customerorders');
    };

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!customerOrder || !originalCustomerOrder) return false;
        return JSON.stringify(customerOrder) !== JSON.stringify(originalCustomerOrder);
    }, [customerOrder, originalCustomerOrder]);

    const blocker = useBlocker(hasUnsavedChanges);

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

    const updateCustomerOrder = useCallback((patch) => {
        setMessages((prev) => prev.filter((message) => message.type !== 'success'));
        setCustomerOrder((prev) => ({ ...prev, ...patch }));
    }, []);

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
        supplierOrderId: parseNullableInt(source?.supplierOrderId),
        customerOrderNr: source?.customerOrderNr || null,
        customerId: parseNullableInt(source?.customerId),
        customerName: source?.customerName || null,
        deliveryAddressName: source?.deliveryAddressName || null,
        deliveryAddress: source?.deliveryAddress || null,
        deliveryPostalNr: source?.deliveryPostalNr || null,
        deliveryPostalAddress: source?.deliveryPostalAddress || null,
        deliveryCountry: source?.deliveryCountry || null,
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
        orderCosts: toArray(source?.orderCosts)
            .filter((row) => row.costId != null)
            .map(buildOrderCostPayload),
    });

    const handleSave = async () => {
        if (!customerOrder) return;

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
                    if (!isActive) return;
                    setCustomerOrder(empty);
                    setOriginalCustomerOrder(structuredClone(empty));
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
    }, [id, isNewCustomerOrder]);

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
                setPalletFormatOptions([]);
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

    const unitLabel = customerOrder?.unitName ?? (customerOrder?.unitId ? `ID ${customerOrder.unitId}` : '');
    const salesCurrencyLabel = customerOrder?.salesCurrencyName ?? (customerOrder?.salesCurrencyId ? `ID ${customerOrder.salesCurrencyId}` : '');
    const editionLabel = formatCompactNumber(customerOrder?.edition, 0);
    const salesPriceLabel = formatCompactNumber(customerOrder?.salesPrice, 2);
    const salesCurrencyRateLabel = formatCompactNumber(customerOrder?.salesCurrencyRate, 4);

    return (
        <div className="relative flex flex-col h-full">
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
                {customerOrder?.id ? (
                    <>Ordererkännande <span className="ml-2 text-red-500">{customerOrder.id}</span></>
                ) : (
                    'Nytt ordererkännande'
                )}
            </h2>

            <div className="flex h-full items-stretch">
                <div className="flex flex-col w-80 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderMetaRow('Skapad:', customerOrder?.created, customerOrder?.createdByUserName)}
                            {renderMetaRow('Redigerad:', customerOrder?.edited, customerOrder?.editedByUserName)}
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
                        entityType="customerOrder"
                        entityId={Number.isInteger(customerOrder?.id) && customerOrder.id > 0 ? customerOrder.id : null}
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
                        </div>
                    </div>

                    <div className="ml-[-5px] w-200 mb-4 rounded-sm border border-gray-200 bg-gradient-to-br from-slate-50 via-white to-slate-100 px-5 py-4 shadow-sm">
                        <div className="grid grid-cols-[3fr_1fr]">
                            <div className="mt-0 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                                {renderReadOnlyInfoRow('Produkt', customerOrder?.product)}
                                {renderReadOnlyInfoRow('Material', customerOrder?.material)}
                                {renderReadOnlyInfoRow('Format', customerOrder?.format)}
                                {renderReadOnlyInfoRow('Färg', customerOrder?.color)}
                                {renderReadOnlyInfoRow('Konstruktion', customerOrder?.construction)}
                                {renderReadOnlyInfoRow('FSC', customerOrder?.isFSC ? 'Ja' : 'Nej')}
                            </div>
                            <div>
                                {renderReadOnlyInfoRow('Leverantörsorder', customerOrder?.supplierOrderNr || 'Saknas')}
                            </div>
                        </div>
                    </div>

                    <div className="mt-10 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-25">
                        <span>
                            <div className="grid grid-cols-[380px_380px_300px] gap-x-20 gap-y-8">
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
                                        label="Kunds onr"
                                        value={customerOrder?.customerOrderNr || ''}
                                        onChange={(value) => updateCustomerOrder({ customerOrderNr: value })}
                                        labelWidth="w-20"
                                        margintop="3"
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
                                        height="h-10"
                                    />
                                    <LabeledTextArea
                                        label="Meddelande"
                                        value={customerOrder?.message || ''}
                                        onChange={(value) => updateCustomerOrder({ message: value })}
                                        labelWidth="w-20"
                                        margintop="0"
                                        height="h-20"
                                    />
                                </span>

                                <span>
                                    <p className="text-sm font-semibold text-gray-500 mb-4 pb-2 text-center border-b border-gray-300">Orderinfo</p>
                                    <div className="mt-5 text-xs">
                                        <p className="text-gray-900 font-bold">Upplaga och pris</p>
                                        <div className="flex items-center gap-0 mt-2">
                                            <span className="text-gray-800">{editionLabel || '\u00A0'}</span>
                                            <span className="ml-2 text-gray-800">{unitLabel || '\u00A0'}</span>
                                            <span className="ml-10 text-gray-800">{salesPriceLabel || '\u00A0'}</span>
                                            <span className="ml-2 text-xs text-gray-800">{salesCurrencyLabel || '\u00A0'}</span>
                                            <span className="ml-10 text-gray-800">{salesCurrencyRateLabel ? `Kurs: ${salesCurrencyRateLabel}` : '\u00A0'}</span>
                                        </div>
                                    </div>
                                </span>

                                <span className="col-span-2">
                                    <OrderCost
                                        customerOrderId={customerOrder?.id}
                                        supplierOrderId={customerOrder?.supplierOrderId}
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
                            </div>
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default CustomerOrder;
