import React, { useCallback, useEffect, useState, useRef } from 'react';
import { useNavigate, useParams, useBlocker } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import ToggleSwitch from '../../components/ToggleSwitch';
import { formatDateTime } from '../../helpers/dateUtils';
import { parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import LabeledTextArea from '../../components/LabeledTextArea';
import InventorySelectModal from '../../modals/InventorySelectModal';

const createNewCustomerModel = () => ({
    id: 0,
    name: '',
    sortName: '',
    address: '',
    address2: '',
    postalNr: '',
    postalAddress: '',
    visitingAddress: '',
    country: 'SVERIGE',
    countryId: 752,
    reference: '',
    telephone1: '',
    telephone2: '',
    telephone3: '',
    fax: '',
    email: '',
    orgNr: '',
    vatNr: '',
    note: '',
    loadingInstruction: '',
    paymentDays: 0,
    creditLimit: 0,
    externalKey: '',
    currencyId: null,
    languageId: 1,
    accountNrAccountsReceivable: null,
    accountNrEarnings: null,
    active: true,
    export: false,
    eu: false,
    vat: true,
    vatRate: 25,
    specialUnitHandling: false,
    printForDocumentScanning: false,
    invoicePalletsSeparately: false,
    invoiceCostsSeparately: false,
    invoiceCostsSeparatelyImmediately: false,
    lockOrder: false,
    invoiceRowsInProductNameOrder: false,
    termsOfDelivery: '',
    termsOfPayment: '',
    responsibleUserId: null,
    supportEmployeeId: null,
    orderNrPrefix: '',
    lastActivity: '',
    createdAt: null,
    createdBy: null,
    createdByUserName: '',
    editedAt: null,
    editedBy: null,
    editedByUserName: '',
    contactPersons: [],
    deliveryAddresses: [],
});

const makeTempId = () => `temp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const defaultLanguageOptions = [
    { id: 1, name: 'Svenska' },
    { id: 2, name: 'English' },
    { id: 3, name: 'Deutsch' },
];

const countryCodeOptions = [
    { id: 752, name: 'SE', countryName: 'SVERIGE' },
    { id: 208, name: 'DK', countryName: 'DANMARK' },
    { id: 246, name: 'FI', countryName: 'FINLAND' },
    { id: 578, name: 'NO', countryName: 'NORGE' },
    { id: 276, name: 'DE', countryName: 'TYSKLAND' },
    { id: 826, name: 'GB', countryName: 'STORBRITANNIEN' },
];

const deliveryAddressCountryCodeOptions = [
    { id: 'SE', name: 'SE', countryName: 'SVERIGE' },
    { id: 'DK', name: 'DK', countryName: 'DANMARK' },
    { id: 'FI', name: 'FI', countryName: 'FINLAND' },
    { id: 'NO', name: 'NO', countryName: 'NORGE' },
    { id: 'DE', name: 'DE', countryName: 'TYSKLAND' },
    { id: 'GB', name: 'GB', countryName: 'STORBRITANNIEN' },
];

const renderMetaRow = (label, value, userName, userId) => {
    if (!value) {
        return null;
    }

    const actor = userName || (userId != null ? `Anv. ${userId}` : null);

    return (
        <div className="grid grid-cols-21 gap-1 mx-2">
            <div className="col-span-5"><span className="font-medium">{label}</span></div>
            <div className="col-span-8">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">{actor && `av ${actor}`}</div>
        </div>
    );
};

const toArray = (value) => (Array.isArray(value) ? value : []);
const getItemIdentity = (item) => item?.id ?? item?.tempId;

const Customer = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewCustomer = id === 'new';

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [showInventorySelectModal, setShowInventorySelectModal] = useState(false);
    const [isLoadingInventoryOptions, setIsLoadingInventoryOptions] = useState(false);
    const [inventoryOptions, setInventoryOptions] = useState([]);

    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [deliveryTermOptions, setDeliveryTermOptions] = useState([]);
    const [paymentTermOptions, setPaymentTermOptions] = useState([]);
    const [userOptions, setUserOptions] = useState([]);
    const [languageOptions, setLanguageOptions] = useState(defaultLanguageOptions);

    const [customer, setCustomer] = useState(null);
    const [originalCustomer, setOriginalCustomer] = useState(null);
    const [selectedContactIdentity, setSelectedContactIdentity] = useState(null);
    const [selectedAddressIdentity, setSelectedAddressIdentity] = useState(null);
    const skipUnsavedCheckRef = useRef(false);
    const pendingContactFocusIdentityRef = useRef(null);
    const pendingAddressFocusIdentityRef = useRef(null);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!customer || !originalCustomer) return false;
        return JSON.stringify(customer) !== JSON.stringify(originalCustomer);
    }, [customer, originalCustomer]);

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

    useEffect(() => {
        let isActive = true;

        const loadFormOptions = async () => {
            try {
                const requestKey = 'customers:form-options';
                const response = await getSharedRequest(requestKey, () => apiClient.get('/customers/form-options'));
                const payload = response?.data ?? {};

                if (!isActive) return;

                setUserOptions(toArray(payload.users));
                setCurrencyOptions(toArray(payload.currencies));
                setDeliveryTermOptions(toArray(payload.termsOfDelivery));
                setPaymentTermOptions(toArray(payload.termsOfPayment));
                setLanguageOptions(toArray(payload.languages).length > 0 ? toArray(payload.languages) : defaultLanguageOptions);
            } catch (error) {
                console.error('Failed to load customer form options:', error);
                if (!isActive) return;
                setUserOptions([]);
                setCurrencyOptions([]);
                setDeliveryTermOptions([]);
                setPaymentTermOptions([]);
                setLanguageOptions(defaultLanguageOptions);
            }
        };

        void loadFormOptions();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const loadCustomer = async () => {
            setLoading(true);

            try {
                if (isNewCustomer) {
                    const nextCustomer = createNewCustomerModel();

                    if (currencyOptions.length > 0) {
                        const sekOption = currencyOptions.find((item) => String(item.name).toUpperCase() === 'SEK');
                        nextCustomer.currencyId = sekOption?.id ?? currencyOptions[0].id;
                    }

                    if (languageOptions.length > 0) {
                        const swedishOption = languageOptions.find((item) => String(item.name).toLowerCase().includes('svensk'));
                        nextCustomer.languageId = swedishOption?.id ?? languageOptions[0].id;
                    }

                    if (!isActive) return;
                    setCustomer(nextCustomer);
                    setOriginalCustomer(structuredClone(nextCustomer));
                    return;
                }

                const customerId = parseNullableInt(id);
                if (!customerId || customerId <= 0) {
                    if (!isActive) return;
                    setCustomer(null);
                    setOriginalCustomer(null);
                    return;
                }

                const response = await getSharedRequest(`customers:${customerId}`, () => apiClient.get(`/customers/${customerId}`));
                if (!isActive) return;

                const loadedCustomerBase = response?.data?.customer ?? null;
                const loadedCustomer = loadedCustomerBase
                    ? {
                        ...loadedCustomerBase,
                        contactPersons: toArray(response?.data?.contactPersons),
                        deliveryAddresses: toArray(response?.data?.deliveryAddresses),
                    }
                    : null;
                setCustomer(loadedCustomer);
                setOriginalCustomer(structuredClone(loadedCustomer));
            } catch (error) {
                console.error('Failed to load customer view data:', error);
                if (!isActive) return;
                setMessages([{ type: 'error', text: 'Kunde inte lasa kunddata.' }]);
                setCustomer((prev) => prev ?? (isNewCustomer ? createNewCustomerModel() : null));
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        void loadCustomer();

        return () => {
            isActive = false;
        };
    }, [id, isNewCustomer]);

    useEffect(() => {
        if (!isNewCustomer || !customer || customer.id !== 0) {
            return;
        }

        const sekOption = currencyOptions.find((item) => String(item.name).toUpperCase() === 'SEK');
        const defaultCurrencyId = sekOption?.id ?? currencyOptions[0]?.id ?? null;
        const swedishOption = languageOptions.find((item) => String(item.name).toLowerCase().includes('svensk'));
        const defaultLanguageId = swedishOption?.id ?? languageOptions[0]?.id ?? null;

        const shouldFillCurrency = customer.currencyId == null && defaultCurrencyId != null;
        const shouldFillLanguage = customer.languageId == null && defaultLanguageId != null;

        if (!shouldFillCurrency && !shouldFillLanguage) {
            return;
        }

        setCustomer((prev) => {
            if (!prev || prev.id !== 0) return prev;
            return {
                ...prev,
                ...(prev.currencyId == null && defaultCurrencyId != null ? { currencyId: defaultCurrencyId } : {}),
                ...(prev.languageId == null && defaultLanguageId != null ? { languageId: defaultLanguageId } : {}),
            };
        });

        setOriginalCustomer((prev) => {
            if (!prev || prev.id !== 0) return prev;
            return {
                ...prev,
                ...(prev.currencyId == null && defaultCurrencyId != null ? { currencyId: defaultCurrencyId } : {}),
                ...(prev.languageId == null && defaultLanguageId != null ? { languageId: defaultLanguageId } : {}),
            };
        });
    }, [currencyOptions, customer, isNewCustomer, languageOptions]);

    useEffect(() => {
        const contactItems = toArray(customer?.contactPersons);
        if (contactItems.length === 0) {
            setSelectedContactIdentity(null);
            return;
        }

        const hasSelected = selectedContactIdentity != null
            && contactItems.some((item) => getItemIdentity(item) === selectedContactIdentity);

        if (!hasSelected) {
            setSelectedContactIdentity(getItemIdentity(contactItems[0]));
        }
    }, [customer?.contactPersons, selectedContactIdentity]);

    useEffect(() => {
        const addressItems = toArray(customer?.deliveryAddresses);
        if (addressItems.length === 0) {
            setSelectedAddressIdentity(null);
            return;
        }

        const hasSelected = selectedAddressIdentity != null
            && addressItems.some((item) => getItemIdentity(item) === selectedAddressIdentity);

        if (!hasSelected) {
            setSelectedAddressIdentity(getItemIdentity(addressItems[0]));
        }
    }, [customer?.deliveryAddresses, selectedAddressIdentity]);

    useEffect(() => {
        if (pendingContactFocusIdentityRef.current == null) {
            return;
        }

        if (selectedContactIdentity !== pendingContactFocusIdentityRef.current) {
            return;
        }

        const input = document.getElementById('customer-contact-name-input');
        if (input instanceof HTMLInputElement) {
            input.focus();
            input.select();
            pendingContactFocusIdentityRef.current = null;
        }
    }, [customer?.contactPersons, selectedContactIdentity]);

    useEffect(() => {
        if (pendingAddressFocusIdentityRef.current == null) {
            return;
        }

        if (selectedAddressIdentity !== pendingAddressFocusIdentityRef.current) {
            return;
        }

        const input = document.getElementById('customer-delivery-address-name-input');
        if (input instanceof HTMLInputElement) {
            input.focus();
            input.select();
            pendingAddressFocusIdentityRef.current = null;
        }
    }, [customer?.deliveryAddresses, selectedAddressIdentity]);

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/order/customers');
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        blocker.reset();
    };

    const handleChange = (field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setCustomer((prev) => ({
            ...prev,
            [field]: resolvedValue,
        }));
    };

    const handleContactChange = (identity, field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setCustomer((prev) => ({
            ...prev,
            contactPersons: toArray(prev?.contactPersons).map((item) => (
                getItemIdentity(item) === identity
                    ? { ...item, [field]: resolvedValue }
                    : item
            )),
        }));
    };

    const handleAddressChange = (identity, field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setCustomer((prev) => ({
            ...prev,
            deliveryAddresses: toArray(prev?.deliveryAddresses).map((item) => (
                getItemIdentity(item) === identity
                    ? { ...item, [field]: resolvedValue }
                    : item
            )),
        }));
    };

    const handleAddressCountryCodeChange = (identity, value) => {
        const normalizedCode = String(value || '').toUpperCase();
        const selectedOption = deliveryAddressCountryCodeOptions.find((item) => item.id === normalizedCode);

        setCustomer((prev) => ({
            ...prev,
            deliveryAddresses: toArray(prev?.deliveryAddresses).map((item) => (
                getItemIdentity(item) === identity
                    ? {
                        ...item,
                        countryCode: normalizedCode,
                        country: selectedOption?.countryName ?? '',
                    }
                    : item
            )),
        }));
    };

    const loadInventoryOptions = useCallback(async () => {
        setIsLoadingInventoryOptions(true);

        try {
            const requestKey = 'inventories:customer-delivery-place-options';
            const response = await getSharedRequest(requestKey, () => apiClient.post('/inventories/search', {
                pagination: { pageNumber: 1, pageSize: 1000 },
                orderBy: [{ field: 'name', direction: 'asc' }],
            }));

            const allItems = toArray(response?.data?.items);
            const selectableItems = allItems.filter((item) => Boolean(item?.isInventory) || Boolean(item?.isOmlast));
            setInventoryOptions(selectableItems);
        } catch (error) {
            console.error('Failed to load inventories for customer delivery place:', error);
            setInventoryOptions([]);
            setMessages([{ type: 'error', text: 'Kunde inte hamta lager / omlastningsplatser.' }]);
        } finally {
            setIsLoadingInventoryOptions(false);
        }
    }, []);

    const openInventorySelectModal = () => {
        setShowInventorySelectModal(true);
        void loadInventoryOptions();
    };

    const addAddressFromInventory = (inventory) => {
        const tempId = makeTempId();
        const inventoryCountryCode = String(inventory?.countryCode ?? '').toUpperCase();
        const newAddress = {
            tempId,
            name: inventory?.name || 'Ny',
            address: inventory?.address ?? '',
            postalNr: inventory?.postalNrText ?? '',
            postalAddress: inventory?.postalAddress ?? '',
            country: inventory?.country ?? '',
            palletRegistrationNr: '',
            isDefault: false,
            address2: '',
            postalNrValue: parseNullableInt(inventory?.postalNr),
            countryCode: inventoryCountryCode,
            logisticsInfoInternal: '',
            inventoryId: parseNullableInt(inventory?.id),
            nextTransportDeliveryAddressId: null,
            positionId: parseNullableInt(inventory?.positionId),
            addressExtra: inventory?.addressExtra ?? '',
        };

        setCustomer((prev) => ({
            ...prev,
            deliveryAddresses: [...toArray(prev?.deliveryAddresses), newAddress],
        }));

        pendingAddressFocusIdentityRef.current = tempId;
        setSelectedAddressIdentity(tempId);
        setShowInventorySelectModal(false);
    };

    const addContact = () => {
        const tempId = makeTempId();
        const newContact = {
            tempId,
            customerContactPersonName: 'Ny',
            contactPerson: '',
            title: '',
            email: '',
            telephone: '',
            cellphone: '',
            mailQuotation: false,
            mailCustomerOrder: false,
            mailInvoice: false,
            mailTransportOrder: false,
            mailGeneralInfo: false,
            mailCallOffConfirmation: false,
        };

        setCustomer((prev) => ({
            ...prev,
            contactPersons: [...toArray(prev?.contactPersons), newContact],
        }));
        pendingContactFocusIdentityRef.current = tempId;
        setSelectedContactIdentity(tempId);
    };

    const addAddress = () => {
        const tempId = makeTempId();
        const newAddress = {
            tempId,
            name: 'Ny',
            address: '',
            postalNr: '',
            postalAddress: '',
            country: '',
            palletRegistrationNr: '',
            isDefault: false,
            address2: '',
            postalNrValue: null,
            countryCode: '',
            logisticsInfoInternal: '',
            inventoryId: null,
            nextTransportDeliveryAddressId: null,
            positionId: null,
            addressExtra: '',
        };

        setCustomer((prev) => ({
            ...prev,
            deliveryAddresses: [...toArray(prev?.deliveryAddresses), newAddress],
        }));
        pendingAddressFocusIdentityRef.current = tempId;
        setSelectedAddressIdentity(tempId);
    };

    const onDeleteContact = (identity) => {
        setCustomer((prev) => ({
            ...prev,
            contactPersons: toArray(prev?.contactPersons).filter((item) => getItemIdentity(item) !== identity),
        }));
        setSelectedContactIdentity(null);
    };

    const onDeleteAddress = (identity) => {
        setCustomer((prev) => ({
            ...prev,
            deliveryAddresses: toArray(prev?.deliveryAddresses).filter((item) => getItemIdentity(item) !== identity),
        }));
        setSelectedAddressIdentity(null);
    };

    const handleCountryCodeChange = (value) => {
        const selectedOption = countryCodeOptions.find((item) => String(item.id) === String(value));
        setCustomer((prev) => ({
            ...prev,
            countryId: parseNullableInt(value),
            country: selectedOption?.countryName ?? '',
        }));
    };

    const buildPayload = (source) => ({
        name: source?.name ?? '',
        sortName: source?.sortName ?? '',
        address: source?.address ?? '',
        address2: source?.address2 ?? '',
        postalNr: source?.postalNr ?? '',
        postalAddress: source?.postalAddress ?? '',
        visitingAddress: source?.visitingAddress ?? '',
        reference: source?.reference ?? '',
        telephone1: source?.telephone1 ?? '',
        telephone2: source?.telephone2 ?? '',
        telephone3: source?.telephone3 ?? '',
        fax: source?.fax ?? '',
        note: source?.note ?? '',
        loadingInstruction: source?.loadingInstruction ?? '',
        responsibleUserId: parseNullableInt(source?.responsibleUserId),
        vat: Boolean(source?.vat),
        active: Boolean(source?.active),
        country: source?.country ?? '',
        termsOfDelivery: source?.termsOfDelivery ?? '',
        termsOfPayment: source?.termsOfPayment ?? '',
        paymentDays: parseNullableInt(source?.paymentDays),
        languageId: parseNullableInt(source?.languageId),
        vatNr: source?.vatNr ?? '',
        email: source?.email ?? '',
        orgNr: source?.orgNr ?? '',
        eu: Boolean(source?.eu),
        export: Boolean(source?.export),
        creditLimit: parseNullableInt(source?.creditLimit),
        currencyId: parseNullableInt(source?.currencyId),
        externalKey: source?.externalKey ?? '',
        vatRate: parseNullableInt(source?.vatRate),
        oneWayPallet: false,
        eurPallet: false,
        specialUnitHandling: Boolean(source?.specialUnitHandling),
        printForDocumentScanning: Boolean(source?.printForDocumentScanning),
        lockOrder: Boolean(source?.lockOrder),
        accountNrAccountsReceivable: parseNullableInt(source?.accountNrAccountsReceivable),
        accountNrEarnings: parseNullableInt(source?.accountNrEarnings),
        supportEmployeeId: parseNullableInt(source?.supportEmployeeId),
        lastActivity: source?.lastActivity ?? '',
        countryId: parseNullableInt(source?.countryId),
        invoicePalletsSeparately: Boolean(source?.invoicePalletsSeparately),
        invoiceCostsSeparately: Boolean(source?.invoiceCostsSeparately),
        invoiceCostsSeparatelyImmediately: Boolean(source?.invoiceCostsSeparatelyImmediately),
        invoiceRowsInProductNameOrder: Boolean(source?.invoiceRowsInProductNameOrder),
        orderNrPrefix: source?.orderNrPrefix ?? '',
        deliveryAddresses: toArray(source?.deliveryAddresses).map((item) => ({
            id: parseNullableInt(item?.id),
            name: item?.name ?? '',
            address: item?.address ?? '',
            postalNr: item?.postalNr ?? '',
            postalAddress: item?.postalAddress ?? '',
            country: item?.country ?? '',
            palletRegistrationNr: item?.palletRegistrationNr ?? '',
            isDefault: Boolean(item?.isDefault),
            address2: item?.address2 ?? '',
            postalNrValue: parseNullableInt(item?.postalNrValue),
            countryCode: item?.countryCode ?? '',
            logisticsInfoInternal: item?.logisticsInfoInternal ?? '',
            inventoryId: parseNullableInt(item?.inventoryId),
            nextTransportDeliveryAddressId: parseNullableInt(item?.nextTransportDeliveryAddressId),
            positionId: parseNullableInt(item?.positionId),
            addressExtra: item?.addressExtra ?? '',
        })),
        contactPersons: toArray(source?.contactPersons).map((item) => ({
            id: parseNullableInt(item?.id),
            customerContactPersonName: item?.customerContactPersonName ?? '',
            contactPerson: item?.contactPerson ?? '',
            email: item?.email ?? '',
            telephone: item?.telephone ?? '',
            cellphone: item?.cellphone ?? '',
            mailQuotation: Boolean(item?.mailQuotation),
            mailCustomerOrder: Boolean(item?.mailCustomerOrder),
            mailInvoice: Boolean(item?.mailInvoice),
            mailTransportOrder: Boolean(item?.mailTransportOrder),
            mailGeneralInfo: Boolean(item?.mailGeneralInfo),
            mailCallOffConfirmation: Boolean(item?.mailCallOffConfirmation),
            title: item?.title ?? '',
        })),
    });

    const handleSave = async () => {
        if (!customer) return;

        try {
            const payload = buildPayload(customer);
            const customerId = parseNullableInt(customer.id);
            const isCreatingNew = !(customerId && customerId > 0);

            const response = isCreatingNew
                ? await apiClient.post('/customers', payload)
                : await apiClient.put(`/customers/${customerId}`, payload);

            const savedCustomer = response?.data ?? null;
            const mergedCustomer = {
                ...customer,
                ...savedCustomer,
            };

            setCustomer(mergedCustomer);
            setOriginalCustomer(structuredClone(mergedCustomer));
            setMessages([{ type: 'success', text: 'Kunden sparades.' }]);

            if (isCreatingNew && savedCustomer?.id) {
                skipUnsavedCheckRef.current = true;
                navigate(`/order/customers/${savedCustomer.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save customer:', error);
            setMessages([{ type: 'error', text: 'Kunde inte spara kunden.' }]);
        }
    };

    const handleDelete = async () => {
        if (!customer?.id) return;

        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/customers/${customer.id}`);
            navigate('/order/customers');
        } catch (error) {
            console.error('Failed to delete customer:', error);
            setMessages([{ type: 'error', text: 'Kunde inte radera kunden.' }]);
        }
    };

    if (loading) {
        return (
            <div className="h-full px-6 py-4">
                <Skeleton height={26} width={260} />
                <div className="mt-6 grid grid-cols-[320px_minmax(0,1fr)] gap-6">
                    <Skeleton height={420} />
                    <Skeleton height={520} />
                </div>
            </div>
        );
    }

    if (!customer) {
        return (
            <div className="h-full px-6 py-10 text-sm text-gray-600">
                Kunden kunde inte hittas.
            </div>
        );
    }

    const selectedContact = toArray(customer?.contactPersons).find((item) => getItemIdentity(item) === selectedContactIdentity) ?? null;
    const selectedAddress = toArray(customer?.deliveryAddresses).find((item) => getItemIdentity(item) === selectedAddressIdentity) ?? null;
    const isSelectedAddressInventoryLinked = parseNullableInt(selectedAddress?.inventoryId) != null;
    const nextTransportDeliveryAddressOptions = toArray(customer?.deliveryAddresses)
        .filter((item) => {
            const optionId = parseNullableInt(item?.id);
            if (optionId == null) {
                return false;
            }

            if (parseNullableInt(item?.inventoryId) != null) {
                return false;
            }

            return optionId !== parseNullableInt(selectedAddress?.id);
        })
        .map((item) => ({
            id: parseNullableInt(item?.id),
            name: item?.name || `Lev.adress ${item?.id}`,
        }));

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA KUND"
                message={`Ar du saker pa att du vill radera kund ${customer.name || customer.id}? Atgarden kan inte angra.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade andringar"
                message="Det finns osparade andringar. Vill du fortsatta anda?"
                confirmText="Fortsatt anda"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <InventorySelectModal
                isOpen={showInventorySelectModal}
                onClose={() => setShowInventorySelectModal(false)}
                onSelect={addAddressFromInventory}
                inventories={inventoryOptions}
                isLoading={isLoadingInventoryOptions}
            />

            <h2 className="ml-90 text-sm pt-2 pb-2 text-gray-700">
                {customer?.id ? (
                    <>Kund <span className="ml-2 text-red-500">{customer.id}</span></>
                ) : 'Ny kund'}
            </h2>

            <div className="flex h-full items-stretch">
                <div className="flex flex-col w-80 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderMetaRow('Skapad:', customer?.createdAt, customer?.createdByUserName, customer?.createdBy)}
                            {renderMetaRow('Redigerad:', customer?.editedAt, customer?.editedByUserName, customer?.editedBy)}
                        </div>
                    </div>

                    <hr className="mt-5 border-gray-300" />
                    <h2 className="text-sm text-center text-gray-700 mt-5">Meddelanden</h2>
                    {messages.length === 0 ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            {messages.map((message, index) => (
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
                        <div className="flex items-center space-x-4">
                            {customer?.id !== 0 && (
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

                    <div className="mt-8 grid w-full grid-cols-[370px_370px_200px_1fr] gap-x-20">
                        <div>
                            <LabeledInput label="Kundnr" value={customer?.id || ''} disabled labelWidth="w-20" margintop="0" />
                            <LabeledInput label="Namn" value={customer?.name || ''} onChange={(value) => handleChange('name', value)} labelWidth="w-20" />
                            <LabeledInput label="Sortering" value={customer?.sortName || ''} onChange={(value) => handleChange('sortName', value)} labelWidth="w-20" />
                            <LabeledInput label="Adress" value={customer?.address || ''} onChange={(value) => handleChange('address', value)} labelWidth="w-20" margintop={2} />
                            <LabeledInput label="" value={customer?.address2 || ''} onChange={(value) => handleChange('address2', value)} labelWidth="w-20" />
                            <div className="flex items-center pb-[1px] text-xs">
                                <label className="w-20 flex-none text-xs text-gray-700" />
                                <input
                                    type="text"
                                    value={customer?.postalNr || ''}
                                    onChange={(event) => handleChange('postalNr', event.target.value)}
                                    className="w-24 rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                                />
                                <input
                                    type="text"
                                    value={customer?.postalAddress || ''}
                                    onChange={(event) => handleChange('postalAddress', event.target.value)}
                                    className="ml-1 flex-1 rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                                />
                            </div>
                            {/* <LabeledInput label="Besoksadr." value={customer?.visitingAddress || ''} onChange={(value) => handleChange('visitingAddress', value)} labelWidth="w-20" margintop="1" /> */}
                            <div className="flex items-center pb-[1px] text-xs">
                                <label className="w-20 flex-none text-xs text-gray-700">Land</label>
                                <div className="w-24">
                                    <LabeledReactSelect
                                        name="countryId"
                                        label=""
                                        value={customer?.countryId || ''}
                                        items={countryCodeOptions}
                                        onChange={handleCountryCodeChange}
                                        margintop="0"
                                    />
                                </div>
                                <input
                                    type="text"
                                    value={customer?.country || ''}
                                    onChange={(event) => handleChange('country', event.target.value)}
                                    className="ml-1 flex-1 rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                                />
                            </div>
                            <LabeledInput label="Referens" value={customer?.reference || ''} onChange={(value) => handleChange('reference', value)} labelWidth="w-20" margintop="2" />
                            <LabeledInput label="Vaxeltel." value={customer?.telephone1 || ''} onChange={(value) => handleChange('telephone1', value)} labelWidth="w-20" />
                            <LabeledInput label="Direkttel" value={customer?.telephone2 || ''} onChange={(value) => handleChange('telephone2', value)} labelWidth="w-20" />
                            <LabeledInput label="Mobiltel" value={customer?.telephone3 || ''} onChange={(value) => handleChange('telephone3', value)} labelWidth="w-20" />
                            <LabeledInput label="Epost" value={customer?.email || ''} onChange={(value) => handleChange('email', value)} labelWidth="w-20" />
                        </div>

                        <div>
                            <LabeledInput label="Organisationsnr." value={customer?.orgNr || ''} onChange={(value) => handleChange('orgNr', value)} labelWidth="w-24" margintop="0" />
                            <LabeledInput label="Vat.nr." value={customer?.vatNr || ''} onChange={(value) => handleChange('vatNr', value)} labelWidth="w-24" />
                            <LabeledTextArea label="Notering" value={customer?.note || ''} onChange={(value) => handleChange('note', value)} labelWidth="w-24" margintop="2" rows={4} />
                            <LabeledTextArea label="Packinstruktion" value={customer?.loadingInstruction || ''} onChange={(value) => handleChange('loadingInstruction', value)} labelWidth="w-24" rows={3} />
                            <LabeledReactSelect name="termsOfDelivery" label="Leveransvillkor" value={customer?.termsOfDelivery || ''} items={deliveryTermOptions} onChange={(value) => handleChange('termsOfDelivery', value)} labelWidth="w-24" margintop="2" allowRawValueLabel />
                            <LabeledReactSelect name="termsOfPayment" label="Betalningsvillkor" value={customer?.termsOfPayment || ''} items={paymentTermOptions} onChange={(value) => handleChange('termsOfPayment', value)} labelWidth="w-24" allowRawValueLabel />
                            <LabeledReactSelect name="responsibleUserId" label="Saljare" value={customer?.responsibleUserId || ''} items={userOptions} onChange={(value) => handleChange('responsibleUserId', value)} labelWidth="w-24" margintop="2" />
                            <LabeledReactSelect name="supportEmployeeId" label="Support" value={customer?.supportEmployeeId || ''} items={userOptions} onChange={(value) => handleChange('supportEmployeeId', value)} labelWidth="w-24" />
                            <LabeledInput label="Ordernr-prefix" value={customer?.orderNrPrefix || ''} onChange={(value) => handleChange('orderNrPrefix', value)} labelWidth="w-24" />
                        </div>

                        <div>
                            <LabeledInput label="Betaldagar" value={customer?.paymentDays ?? ''} onChange={(value) => handleChange('paymentDays', value)} labelWidth="w-20" type="number" integerOnly />
                            <LabeledInput label="Kreditlimit" value={customer?.creditLimit ?? ''} onChange={(value) => handleChange('creditLimit', value)} labelWidth="w-20" type="number" integerOnly />
                            <LabeledReactSelect name="currencyId" label="Valuta" value={customer?.currencyId || ''} items={currencyOptions} onChange={(value) => handleChange('currencyId', value)} labelWidth="w-20" margintop="2" />
                            <LabeledReactSelect name="languageId" label="Sprak" value={customer?.languageId || ''} items={languageOptions} onChange={(value) => handleChange('languageId', value)} labelWidth="w-20" allowRawValueLabel />
                            <LabeledInput label="Konto, kf" value={customer?.accountNrAccountsReceivable ?? ''} onChange={(value) => handleChange('accountNrAccountsReceivable', value)} labelWidth="w-20" margintop="2" type="number" integerOnly />
                            <LabeledInput label="Konto, intäkt" value={customer?.accountNrEarnings ?? ''} onChange={(value) => handleChange('accountNrEarnings', value)} labelWidth="w-20" type="number" integerOnly />

                            <LabeledSwitch field="export" name="export" label="Export" value={Boolean(customer?.export)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-20" marginTop={2} />
                            <LabeledSwitch field="eu" name="eu" label="Eu" value={Boolean(customer?.eu)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-20" />
                            <div className="flex items-center">
                                <span className="w-20 flex-none text-xs text-gray-700">Moms</span>
                                <ToggleSwitch
                                    id="customer-vat"
                                    name="vat"
                                    field="vat"
                                    checked={Boolean(customer?.vat)}
                                    onChange={(_rowId, field, checked) => handleChange(field, checked)}
                                    className=""
                                />
                                <div className="relative ml-2">
                                    <input
                                        type="text"
                                        value={customer?.vatRate ?? ''}
                                        onChange={(event) => handleChange('vatRate', event.target.value)}
                                        className="w-16 rounded-sm border border-gray-300 bg-white px-2 pr-5 py-1 text-xs focus:outline-none"
                                    />
                                    <span className="pointer-events-none absolute inset-y-0 right-2 flex items-center text-xs text-gray-500">%</span>
                                </div>
                            </div>
                        </div>

                        <div>
                            <LabeledSwitch field="active" name="active" label="Är aktiv" value={Boolean(customer?.active)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-20" marginTop={2} />
                            <LabeledSwitch field="specialUnitHandling" name="specialUnitHandling" label="Spec. enhet" value={Boolean(customer?.specialUnitHandling)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" marginTop={2} />
                            <LabeledSwitch field="printForDocumentScanning" name="printForDocumentScanning" label="Utskr.scanning" value={Boolean(customer?.printForDocumentScanning)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                            <LabeledSwitch field="invoicePalletsSeparately" name="invoicePalletsSeparately" label="Fakturera pall separat" value={Boolean(customer?.invoicePalletsSeparately)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                            <LabeledSwitch field="invoiceCostsSeparately" name="invoiceCostsSeparately" label="Fakt. kostnader separat" value={Boolean(customer?.invoiceCostsSeparately)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                            <LabeledSwitch field="invoiceCostsSeparatelyImmediately" name="invoiceCostsSeparatelyImmediately" label="Fakt. kostnader direkt" value={Boolean(customer?.invoiceCostsSeparatelyImmediately)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                            <LabeledSwitch field="lockOrder" name="lockOrder" label="Sparra order" value={Boolean(customer?.lockOrder)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                            <LabeledSwitch field="invoiceRowsInProductNameOrder" name="invoiceRowsInProductNameOrder" label="Fakturaader i produktnamnordn." value={Boolean(customer?.invoiceRowsInProductNameOrder)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-34" />
                        </div>

                    </div>

                    <div className="grid grid-cols-2 gap-15 mt-15">
                        <span>
                            <p className="text-xs text-center border-b border-gray-300 pb-2">Kontaktpersoner</p>
                            <div className="grid grid-cols-2 gap-10 mt-2">
                                <div className="border-r border-gray-300 mt-1 px-2 h-full overflow-y-auto">
                                    {toArray(customer?.contactPersons).length > 0 ? (
                                        <ul className="mt-2 space-y-2">
                                            {toArray(customer?.contactPersons).map((contact) => {
                                                const itemIdentity = getItemIdentity(contact);
                                                const isSelected = selectedContactIdentity === itemIdentity;

                                                return (
                                                    <li
                                                        key={itemIdentity}
                                                        className={`cursor-pointer text-xs p-1 m-0 rounded-sm hover:bg-yellow-100 ${isSelected ? 'bg-yellow-200' : ''}`}
                                                        onClick={() => setSelectedContactIdentity(itemIdentity)}
                                                    >
                                                        <div className="flex justify-between items-center h-5 px-3">
                                                            <div className="">{contact?.customerContactPersonName || contact?.contactPerson || ''}</div>
                                                            <div className="">
                                                                {contact?.title ? (
                                                                    <span className="inline-flex max-w-full items-center rounded-full bg-gray-500 px-3 py-1 text-[10px] font-medium text-slate-50">
                                                                        <span className="truncate">{contact.title}</span>
                                                                    </span>
                                                                ) : null}
                                                            </div>
                                                        </div>
                                                    </li>
                                                );
                                            })}
                                        </ul>
                                    ) : (
                                        <p className="text-xs ms-1 font-light mt-5">Inga kontakter</p>
                                    )}
                                    <div className="mt-3">
                                        <button type="button" onClick={addContact} className="w-full shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-2">Lagg till kontakt</button>
                                    </div>
                                </div>
                                <div className={(selectedContact ? '' : 'opacity-50 pointer-events-none') + ' mt-1'}>
                                    <LabeledInput
                                        id="customer-contact-name-input"
                                        name="customerContactPersonName"
                                        label="Namn"
                                        value={selectedContact?.customerContactPersonName || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'customerContactPersonName', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    {/* <LabeledInput
                                        name="contactPerson"
                                        label="Kontakt"
                                        value={selectedContact?.contactPerson || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'contactPerson', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    /> */}
                                    <LabeledInput
                                        name="email"
                                        label="Email"
                                        value={selectedContact?.email || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'email', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    <LabeledInput
                                        name="telephone"
                                        label="Telefon"
                                        value={selectedContact?.telephone || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'telephone', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    <LabeledInput
                                        name="cellphone"
                                        label="Mobil"
                                        value={selectedContact?.cellphone || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'cellphone', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    <LabeledInput
                                        name="title"
                                        label="Titel"
                                        value={selectedContact?.title || ''}
                                        onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'title', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    />
                                    <LabeledSwitch
                                        field="mailQuotation"
                                        name="mailQuotation"
                                        label="Offert"
                                        value={Boolean(selectedContact?.mailQuotation)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailCustomerOrder"
                                        name="mailCustomerOrder"
                                        label="Oe"
                                        value={Boolean(selectedContact?.mailCustomerOrder)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailInvoice"
                                        name="mailInvoice"
                                        label="Faktura"
                                        value={Boolean(selectedContact?.mailInvoice)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailTransportOrder"
                                        name="mailTransportOrder"
                                        label="Transport"
                                        value={Boolean(selectedContact?.mailTransportOrder)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailGeneralInfo"
                                        name="mailGeneralInfo"
                                        label="Info"
                                        value={Boolean(selectedContact?.mailGeneralInfo)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailCallOffConfirmation"
                                        name="mailCallOffConfirmation"
                                        label="Avrop"
                                        value={Boolean(selectedContact?.mailCallOffConfirmation)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <button
                                        type="button"
                                        onClick={() => onDeleteContact(getItemIdentity(selectedContact))}
                                        className="text-xs text-red-500 hover:text-red-700 mt-5"
                                    >
                                        Radera kontakt
                                    </button>
                                </div>
                            </div>
                        </span>

                        <span>
                            <p className="text-xs text-center border-b border-gray-300 pb-2">Leveransadresser</p>
                            <div className="grid grid-cols-2 gap-10 mt-2">
                                <div className="border-r border-gray-300 mt-1 px-2 h-full overflow-y-auto">
                                    {toArray(customer?.deliveryAddresses).length > 0 ? (
                                        <ul className="mt-2 space-y-2">
                                            {toArray(customer?.deliveryAddresses).map((address) => {
                                                const itemIdentity = getItemIdentity(address);
                                                const isSelected = selectedAddressIdentity === itemIdentity;

                                                return (
                                                    <li
                                                        key={itemIdentity}
                                                        className={`cursor-pointer text-xs p-1 m-0 rounded-sm hover:bg-yellow-100 ${isSelected ? 'bg-yellow-200' : ''}`}
                                                        onClick={() => setSelectedAddressIdentity(itemIdentity)}
                                                    >
                                                        <div className="flex justify-between items-center h-5 px-3">
                                                            <div className="">{address?.name || ''}</div>
                                                        </div>
                                                    </li>
                                                );
                                            })}
                                        </ul>
                                    ) : (
                                        <p className="text-xs ms-1 font-light mt-5">Inga leveransadresser</p>
                                    )}
                                    <div className="mt-3">
                                        <button type="button" onClick={addAddress} className="w-full shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-[5px]">Lägg till lev.adress</button>
                                        <button type="button" onClick={openInventorySelectModal} className="w-full mt-2 shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-[5px]">Lägg till lageradress / omlasningsplats</button>
                                    </div>
                                </div>
                                <div className={(selectedAddress ? '' : 'opacity-50 pointer-events-none') + ' mt-1'}>
                                    <LabeledInput
                                        id="customer-delivery-address-name-input"
                                        name="name"
                                        label="Lev.adress"
                                        value={selectedAddress?.name || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'name', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        disabled={isSelectedAddressInventoryLinked}
                                        placeholder=""
                                    />
                                    <LabeledInput
                                        name="address"
                                        label="Adress"
                                        value={selectedAddress?.address || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'address', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        disabled={isSelectedAddressInventoryLinked}
                                        placeholder=""
                                    />
                                    <div className="flex items-center w-full pb-[1px]">
                                        <label className="w-20 flex-none text-xs text-gray-700">Postnr</label>
                                        <input
                                            name="postalNrValue"
                                            type="number"
                                            value={selectedAddress?.postalNrValue ?? ''}
                                            onChange={(event) => handleAddressChange(getItemIdentity(selectedAddress), 'postalNrValue', event.target.value)}
                                            disabled={isSelectedAddressInventoryLinked}
                                            className={`text-xs w-24 border border-gray-300 rounded-sm px-2 py-1 focus:outline-none ${!isSelectedAddressInventoryLinked ? 'bg-white' : ''}`}
                                            placeholder="Nr"
                                        />
                                        <input
                                            name="postalNr"
                                            value={selectedAddress?.postalNr || ''}
                                            onChange={(event) => handleAddressChange(getItemIdentity(selectedAddress), 'postalNr', event.target.value)}
                                            disabled={isSelectedAddressInventoryLinked}
                                            className={`ml-1 text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none ${!isSelectedAddressInventoryLinked ? 'bg-white' : ''}`}
                                            placeholder="Text"
                                        />
                                    </div>
                                    <LabeledInput
                                        name="postalAddress"
                                        label="Postort"
                                        value={selectedAddress?.postalAddress || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'postalAddress', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        disabled={isSelectedAddressInventoryLinked}
                                        placeholder=""
                                    />
                                    <div className="flex items-center w-full pb-[1px] text-xs">
                                        <label className="w-20 flex-none text-xs text-gray-700">Land</label>
                                        <LabeledReactSelect
                                            name="countryCode"
                                            label=""
                                            value={String(selectedAddress?.countryCode || '').toUpperCase()}
                                            items={deliveryAddressCountryCodeOptions}
                                            onChange={(value) => handleAddressCountryCodeChange(getItemIdentity(selectedAddress), value)}
                                            margintop="0"
                                            inputWidth="w-24"
                                            isDisabled={isSelectedAddressInventoryLinked}
                                        />
                                        <input
                                            type="text"
                                            name="country"
                                            value={selectedAddress?.country || ''}
                                            onChange={(event) => handleAddressChange(getItemIdentity(selectedAddress), 'country', event.target.value)}
                                            disabled={isSelectedAddressInventoryLinked}
                                            className={`ml-1 text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none ${!isSelectedAddressInventoryLinked ? 'bg-white' : ''}`}
                                        />
                                    </div>
                                    <LabeledInput
                                        name="addressExtra"
                                        label="Adress extra"
                                        value={selectedAddress?.addressExtra || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'addressExtra', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        disabled={isSelectedAddressInventoryLinked}
                                        placeholder=""
                                    />
                                    {/* <LabeledInput
                                        name="palletRegistrationNr"
                                        label="Pallregnr"
                                        value={selectedAddress?.palletRegistrationNr || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'palletRegistrationNr', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        placeholder=""
                                    /> */}
                                    <LabeledTextArea
                                        name="logisticsInfoInternal"
                                        label="Logistikinfo"
                                        value={selectedAddress?.logisticsInfoInternal || ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'logisticsInfoInternal', value)}
                                        labelWidth="w-20"
                                        margintop="2"
                                        rows={2}
                                        placeholder=""
                                    />
                                    {/* <LabeledInput
                                        name="inventoryId"
                                        label="Inventory ID"
                                        type="number"
                                        integerOnly
                                        value={selectedAddress?.inventoryId ?? ''}
                                        onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'inventoryId', value)}
                                        labelWidth="w-20"
                                        margintop="0"
                                        disabled={isSelectedAddressInventoryLinked}
                                        placeholder=""
                                    /> */}
                                    {isSelectedAddressInventoryLinked && (
                                        <LabeledReactSelect
                                            name="nextTransportDeliveryAddressId"
                                            label="Next trp adr"
                                            value={selectedAddress?.nextTransportDeliveryAddressId ?? ''}
                                            items={nextTransportDeliveryAddressOptions}
                                            onChange={(value) => handleAddressChange(getItemIdentity(selectedAddress), 'nextTransportDeliveryAddressId', parseNullableInt(value))}
                                            labelWidth="w-20"
                                            margintop="0"
                                            isDisabled={nextTransportDeliveryAddressOptions.length === 0}
                                        />
                                    )}
                                    <LabeledSwitch
                                        field="isDefault"
                                        name="isDefault"
                                        label="Huvudadress"
                                        value={Boolean(selectedAddress?.isDefault)}
                                        onChange={(_rowId, field, checked) => handleAddressChange(getItemIdentity(selectedAddress), field, checked)}
                                        labelWidth="w-20"
                                        marginTop={4}
                                    />
                                    <button
                                        type="button"
                                        onClick={() => onDeleteAddress(getItemIdentity(selectedAddress))}
                                        className="text-xs text-red-500 hover:text-red-700 mt-5"
                                    >
                                        Radera lev.adress
                                    </button>
                                </div>
                            </div>
                        </span>
                    </div>



                </div>
            </div>
        </div>
    );
};

export default Customer;