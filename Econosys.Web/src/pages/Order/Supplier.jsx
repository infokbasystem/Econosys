import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledTextArea from '../../components/LabeledTextArea';
import { parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const defaultLanguageOptions = [
    { id: 1, name: 'Svenska' },
    { id: 2, name: 'English' },
    { id: 3, name: 'Deutsch' },
];

const createNewSupplierModel = () => ({
    id: 0,
    name: '',
    sortName: '',
    reference: '',
    contactPerson: '',
    address: '',
    address2: '',
    postalNr: '',
    postalAddress: '',
    visitingAddress: '',
    country: 'SVERIGE',
    telephone1: '',
    telephone2: '',
    telephone3: '',
    fax: '',
    note: '',
    active: true,
    languageId: 1,
    termsOfDelivery: '',
    termsOfPayment: '',
    email: '',
    inquiryCommunicationTypeId: 1,
    supplierOrderCommunicationTypeId: 1,
    costCenter: '',
    currencyId: null,
    econopackTransportResponsible: false,
    printForDocumentScanning: false,
    pricePerEurPallet: null,
    fscDefault: false,
    supplierOrderTemplateNr: null,
    contactPersons: [],
    factories: [],
});

const makeTempId = () => `temp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
const toArray = (value) => (Array.isArray(value) ? value : []);
const getItemIdentity = (item) => item?.id ?? item?.tempId;

const Supplier = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewSupplier = id === 'new';

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);

    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [deliveryTermOptions, setDeliveryTermOptions] = useState([]);
    const [paymentTermOptions, setPaymentTermOptions] = useState([]);
    const [languageOptions, setLanguageOptions] = useState(defaultLanguageOptions);
    const [inquiryCommunicationOptions, setInquiryCommunicationOptions] = useState([{ id: 1, name: 'Epost' }]);
    const [supplierOrderCommunicationOptions, setSupplierOrderCommunicationOptions] = useState([{ id: 1, name: 'Epost' }]);

    const [supplier, setSupplier] = useState(null);
    const [originalSupplier, setOriginalSupplier] = useState(null);
    const [activeTab, setActiveTab] = useState('contacts');
    const [selectedContactIdentity, setSelectedContactIdentity] = useState(null);
    const [selectedFactoryIdentity, setSelectedFactoryIdentity] = useState(null);
    const skipUnsavedCheckRef = useRef(false);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!supplier || !originalSupplier) return false;
        return JSON.stringify(supplier) !== JSON.stringify(originalSupplier);
    }, [supplier, originalSupplier]);

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
                const response = await getSharedRequest('suppliers:form-options', () => apiClient.get('/suppliers/form-options'));
                const payload = response?.data ?? {};

                if (!isActive) return;

                setCurrencyOptions(toArray(payload.currencies));
                setDeliveryTermOptions(toArray(payload.termsOfDelivery));
                setPaymentTermOptions(toArray(payload.termsOfPayment));
                setLanguageOptions(toArray(payload.languages).length > 0 ? toArray(payload.languages) : defaultLanguageOptions);
                setInquiryCommunicationOptions(toArray(payload.inquiryCommunicationTypes).length > 0 ? toArray(payload.inquiryCommunicationTypes) : [{ id: 1, name: 'Epost' }]);
                setSupplierOrderCommunicationOptions(toArray(payload.supplierOrderCommunicationTypes).length > 0 ? toArray(payload.supplierOrderCommunicationTypes) : [{ id: 1, name: 'Epost' }]);
            } catch (error) {
                console.error('Failed to load supplier form options:', error);
                if (!isActive) return;
                setCurrencyOptions([]);
                setDeliveryTermOptions([]);
                setPaymentTermOptions([]);
                setLanguageOptions(defaultLanguageOptions);
                setInquiryCommunicationOptions([{ id: 1, name: 'Epost' }]);
                setSupplierOrderCommunicationOptions([{ id: 1, name: 'Epost' }]);
            }
        };

        void loadFormOptions();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const loadSupplier = async () => {
            setLoading(true);

            try {
                if (isNewSupplier) {
                    const nextSupplier = createNewSupplierModel();
                    if (!isActive) return;
                    setSupplier(nextSupplier);
                    setOriginalSupplier(structuredClone(nextSupplier));
                    return;
                }

                const supplierId = parseNullableInt(id);
                if (!supplierId || supplierId <= 0) {
                    if (!isActive) return;
                    setSupplier(null);
                    setOriginalSupplier(null);
                    return;
                }

                const response = await getSharedRequest(`suppliers:${supplierId}:details`, () => apiClient.get(`/suppliers/${supplierId}/details`));
                if (!isActive) return;

                const nextSupplier = {
                    ...(response?.data?.supplier ?? createNewSupplierModel()),
                    contactPersons: toArray(response?.data?.contactPersons),
                    factories: toArray(response?.data?.factories),
                };

                setSupplier(nextSupplier);
                setOriginalSupplier(structuredClone(nextSupplier));
            } catch (error) {
                console.error('Failed to load supplier view data:', error);
                if (!isActive) return;
                setMessages([{ type: 'error', text: 'Kunde inte lasa leverantorsdata.' }]);
                setSupplier((prev) => prev ?? (isNewSupplier ? createNewSupplierModel() : null));
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        void loadSupplier();

        return () => {
            isActive = false;
        };
    }, [id, isNewSupplier]);

    useEffect(() => {
        if (!isNewSupplier || !supplier || supplier.id !== 0) return;

        const sekOption = currencyOptions.find((item) => String(item.name).toUpperCase() === 'SEK');
        const defaultCurrencyId = sekOption?.id ?? currencyOptions[0]?.id ?? null;
        const swedishOption = languageOptions.find((item) => String(item.name).toLowerCase().includes('svensk'));
        const defaultLanguageId = swedishOption?.id ?? languageOptions[0]?.id ?? null;

        setSupplier((prev) => {
            if (!prev || prev.id !== 0) return prev;

            const shouldUpdate = prev.currencyId == null || prev.languageId == null;
            if (!shouldUpdate) return prev;

            return {
                ...prev,
                ...(prev.currencyId == null && defaultCurrencyId != null ? { currencyId: defaultCurrencyId } : {}),
                ...(prev.languageId == null && defaultLanguageId != null ? { languageId: defaultLanguageId } : {}),
            };
        });

        setOriginalSupplier((prev) => {
            if (!prev || prev.id !== 0) return prev;

            const shouldUpdate = prev.currencyId == null || prev.languageId == null;
            if (!shouldUpdate) return prev;

            return {
                ...prev,
                ...(prev.currencyId == null && defaultCurrencyId != null ? { currencyId: defaultCurrencyId } : {}),
                ...(prev.languageId == null && defaultLanguageId != null ? { languageId: defaultLanguageId } : {}),
            };
        });
    }, [currencyOptions, isNewSupplier, languageOptions, supplier]);

    useEffect(() => {
        const items = toArray(supplier?.contactPersons);
        if (items.length === 0) {
            setSelectedContactIdentity(null);
            return;
        }

        if (!items.some((item) => getItemIdentity(item) === selectedContactIdentity)) {
            setSelectedContactIdentity(getItemIdentity(items[0]));
        }
    }, [selectedContactIdentity, supplier?.contactPersons]);

    useEffect(() => {
        const items = toArray(supplier?.factories);
        if (items.length === 0) {
            setSelectedFactoryIdentity(null);
            return;
        }

        if (!items.some((item) => getItemIdentity(item) === selectedFactoryIdentity)) {
            setSelectedFactoryIdentity(getItemIdentity(items[0]));
        }
    }, [selectedFactoryIdentity, supplier?.factories]);

    const selectedContact = useMemo(
        () => toArray(supplier?.contactPersons).find((item) => getItemIdentity(item) === selectedContactIdentity) ?? null,
        [selectedContactIdentity, supplier?.contactPersons]
    );

    const selectedFactory = useMemo(
        () => toArray(supplier?.factories).find((item) => getItemIdentity(item) === selectedFactoryIdentity) ?? null,
        [selectedFactoryIdentity, supplier?.factories]
    );

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/order/suppliers');
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
        setSupplier((prev) => ({
            ...prev,
            [field]: resolvedValue,
        }));
    };

    const handleContactChange = (identity, field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setSupplier((prev) => ({
            ...prev,
            contactPersons: toArray(prev?.contactPersons).map((item) => (
                getItemIdentity(item) === identity
                    ? { ...item, [field]: resolvedValue }
                    : item
            )),
        }));
    };

    const handleFactoryChange = (identity, field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setSupplier((prev) => ({
            ...prev,
            factories: toArray(prev?.factories).map((item) => (
                getItemIdentity(item) === identity
                    ? { ...item, [field]: resolvedValue }
                    : item
            )),
        }));
    };

    const addContact = () => {
        const tempId = makeTempId();
        const nextContact = {
            tempId,
            supplierContactPersonName: 'Ny kontakt',
            contactPerson: '',
            email: '',
            telephone: '',
            cellphone: '',
            mailInquiry: false,
            mailSupplierOrder: false,
            doMailTransportOrder: false,
            title: '',
        };

        setSupplier((prev) => ({
            ...prev,
            contactPersons: [...toArray(prev?.contactPersons), nextContact],
        }));
        setSelectedContactIdentity(tempId);
        setActiveTab('contacts');
    };

    const addFactory = () => {
        const tempId = makeTempId();
        const nextFactory = {
            tempId,
            name: 'Ny fabrik',
            address: '',
            postalNr: '',
            city: '',
            country: '',
            countryCode: '',
            addressExtra: '',
            isDefault: false,
            positionId: null,
            viaInventoryId: null,
        };

        setSupplier((prev) => ({
            ...prev,
            factories: [...toArray(prev?.factories), nextFactory],
        }));
        setSelectedFactoryIdentity(tempId);
        setActiveTab('factories');
    };

    const deleteContact = (identity) => {
        setSupplier((prev) => ({
            ...prev,
            contactPersons: toArray(prev?.contactPersons).filter((item) => getItemIdentity(item) !== identity),
        }));
        setSelectedContactIdentity(null);
    };

    const deleteFactory = (identity) => {
        setSupplier((prev) => ({
            ...prev,
            factories: toArray(prev?.factories).filter((item) => getItemIdentity(item) !== identity),
        }));
        setSelectedFactoryIdentity(null);
    };

    const buildPayload = (source) => ({
        name: source?.name ?? '',
        sortName: source?.sortName ?? '',
        reference: source?.reference ?? '',
        contactPerson: source?.contactPerson ?? '',
        address: source?.address ?? '',
        address2: source?.address2 ?? '',
        postalNr: source?.postalNr ?? '',
        postalAddress: source?.postalAddress ?? '',
        visitingAddress: source?.visitingAddress ?? '',
        country: source?.country ?? '',
        telephone1: source?.telephone1 ?? '',
        telephone2: source?.telephone2 ?? '',
        telephone3: source?.telephone3 ?? '',
        fax: source?.fax ?? '',
        note: source?.note ?? '',
        active: Boolean(source?.active),
        languageId: parseNullableInt(source?.languageId),
        termsOfDelivery: source?.termsOfDelivery ?? '',
        termsOfPayment: source?.termsOfPayment ?? '',
        email: source?.email ?? '',
        inquiryCommunicationTypeId: parseNullableInt(source?.inquiryCommunicationTypeId),
        supplierOrderCommunicationTypeId: parseNullableInt(source?.supplierOrderCommunicationTypeId),
        costCenter: source?.costCenter ?? '',
        currencyId: parseNullableInt(source?.currencyId),
        econopackTransportResponsible: Boolean(source?.econopackTransportResponsible),
        printForDocumentScanning: Boolean(source?.printForDocumentScanning),
        pricePerEurPallet: source?.pricePerEurPallet === '' ? null : source?.pricePerEurPallet ?? null,
        fscDefault: Boolean(source?.fscDefault),
        supplierOrderTemplateNr: parseNullableInt(source?.supplierOrderTemplateNr),
        contactPersons: toArray(source?.contactPersons).map((item) => ({
            id: parseNullableInt(item?.id),
            supplierContactPersonName: item?.supplierContactPersonName ?? '',
            contactPerson: item?.contactPerson ?? '',
            email: item?.email ?? '',
            telephone: item?.telephone ?? '',
            cellphone: item?.cellphone ?? '',
            mailInquiry: Boolean(item?.mailInquiry),
            mailSupplierOrder: Boolean(item?.mailSupplierOrder),
            doMailTransportOrder: Boolean(item?.doMailTransportOrder),
            title: item?.title ?? '',
        })),
        factories: toArray(source?.factories).map((item) => ({
            id: parseNullableInt(item?.id),
            name: item?.name ?? '',
            address: item?.address ?? '',
            postalNr: item?.postalNr ?? '',
            city: item?.city ?? '',
            country: item?.country ?? '',
            countryCode: item?.countryCode ?? '',
            addressExtra: item?.addressExtra ?? '',
            isDefault: Boolean(item?.isDefault),
            positionId: parseNullableInt(item?.positionId),
            viaInventoryId: parseNullableInt(item?.viaInventoryId),
        })),
    });

    const handleSave = async () => {
        if (!supplier) return;

        try {
            const payload = buildPayload(supplier);
            const supplierId = parseNullableInt(supplier.id);
            const isCreatingNew = !(supplierId && supplierId > 0);

            const response = isCreatingNew
                ? await apiClient.post('/suppliers', payload)
                : await apiClient.put(`/suppliers/${supplierId}`, payload);

            const savedSupplier = response?.data ?? null;
            const mergedSupplier = {
                ...supplier,
                ...savedSupplier,
            };

            setSupplier(mergedSupplier);
            setOriginalSupplier(structuredClone(mergedSupplier));
            setMessages([{ type: 'success', text: 'Leverantoren sparades.' }]);

            if (isCreatingNew && savedSupplier?.id) {
                skipUnsavedCheckRef.current = true;
                navigate(`/order/suppliers/${savedSupplier.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save supplier:', error);
            setMessages([{ type: 'error', text: 'Kunde inte spara leverantoren.' }]);
        }
    };

    const handleDelete = async () => {
        if (!supplier?.id) return;

        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/suppliers/${supplier.id}`);
            navigate('/order/suppliers');
        } catch (error) {
            console.error('Failed to delete supplier:', error);
            setMessages([{ type: 'error', text: 'Kunde inte radera leverantoren.' }]);
        }
    };

    if (loading) {
        return (
            <div className="h-full px-6 py-4">
                <Skeleton height={26} width={260} />
                <div className="mt-6 grid grid-cols-[1fr_1fr_320px] gap-6">
                    <Skeleton height={420} />
                    <Skeleton height={420} />
                    <Skeleton height={420} />
                </div>
            </div>
        );
    }

    if (!supplier) {
        return (
            <div className="h-full px-6 py-10 text-sm text-gray-600">
                Leverantoren kunde inte hittas.
            </div>
        );
    }

    return (
        <div className="relative flex h-full flex-col px-8 py-3">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA LEVERANTOR"
                message={`Ar du saker pa att du vill radera leverantor ${supplier.name || supplier.id}? Atgarden kan inte angra.`}
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

            <div className="mb-5 flex items-center justify-between">
                <div className="flex items-center space-x-4">
                    <button
                        type="button"
                        onClick={handleBackClick}
                        className="bg-gray-500 px-5 py-[5px] text-xs text-white shadow-md/30 hover:bg-gray-700"
                    >
                        Tillbaka
                    </button>
                    <button
                        type="button"
                        onClick={handleSave}
                        className="bg-lime-700 px-5 py-[5px] text-xs text-white shadow-md/30 hover:bg-lime-900"
                    >
                        Spara
                    </button>
                </div>

                {!isNewSupplier && supplier?.id !== 0 && (
                    <button
                        type="button"
                        onClick={() => setShowDeleteConfirm(true)}
                        className="bg-red-700 px-5 py-[5px] text-xs text-white shadow-md/30 hover:bg-red-800"
                    >
                        Radera
                    </button>
                )}
            </div>

            {messages.length > 0 && (
                <div className="mb-4 flex flex-col gap-2">
                    {messages.map((message, index) => (
                        <div
                            key={index}
                            className={`w-fit min-w-80 border border-gray-200 px-4 py-2 text-xs ${message.type === 'error'
                                ? 'bg-red-100 text-red-700'
                                : message.type === 'warning'
                                    ? 'bg-yellow-100 text-yellow-800'
                                    : 'bg-green-100 text-green-700'
                                }`}
                        >
                            {message.text}
                        </div>
                    ))}
                </div>
            )}

            <div className="grid grid-cols-[430px_580px_320px] gap-x-14">
                <div>
                    <LabeledInput label="Nr" value={supplier?.id || ''} disabled labelWidth="w-22" margintop="0" />
                    <LabeledInput label="Namn" value={supplier?.name || ''} onChange={(value) => handleChange('name', value)} labelWidth="w-22" />
                    <LabeledInput label="Sortering" value={supplier?.sortName || ''} onChange={(value) => handleChange('sortName', value)} labelWidth="w-22" />
                    <LabeledInput label="Adress" value={supplier?.address || ''} onChange={(value) => handleChange('address', value)} labelWidth="w-22" margintop="1" />
                    <LabeledInput label="" value={supplier?.address2 || ''} onChange={(value) => handleChange('address2', value)} labelWidth="w-22" />
                    <div className="flex items-center pb-[1px] text-xs">
                        <label className="w-22 flex-none text-xs text-gray-700" />
                        <input
                            type="text"
                            value={supplier?.postalNr || ''}
                            onChange={(event) => handleChange('postalNr', event.target.value)}
                            className="w-26 rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                        />
                        <input
                            type="text"
                            value={supplier?.postalAddress || ''}
                            onChange={(event) => handleChange('postalAddress', event.target.value)}
                            className="ml-1 flex-1 rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                        />
                    </div>
                    <LabeledInput label="Besoksadr." value={supplier?.visitingAddress || ''} onChange={(value) => handleChange('visitingAddress', value)} labelWidth="w-22" margintop="1" />
                    <LabeledInput label="Land" value={supplier?.country || ''} onChange={(value) => handleChange('country', value)} labelWidth="w-22" />
                    <LabeledInput label="Referens" value={supplier?.reference || ''} onChange={(value) => handleChange('reference', value)} labelWidth="w-22" />
                    <LabeledInput label="Vaxeltel." value={supplier?.telephone1 || ''} onChange={(value) => handleChange('telephone1', value)} labelWidth="w-22" />
                    <LabeledInput label="Direkttel" value={supplier?.telephone2 || ''} onChange={(value) => handleChange('telephone2', value)} labelWidth="w-22" />
                    <LabeledInput label="Mobiltel" value={supplier?.telephone3 || ''} onChange={(value) => handleChange('telephone3', value)} labelWidth="w-22" />
                    <LabeledInput label="Epost" value={supplier?.email || ''} onChange={(value) => handleChange('email', value)} labelWidth="w-22" />
                </div>

                <div>
                    <LabeledTextArea label="Notering" value={supplier?.note || ''} onChange={(value) => handleChange('note', value)} labelWidth="w-28" margintop="0" rows={4} />
                    <LabeledReactSelect
                        name="termsOfDelivery"
                        label="Leveransvillkor"
                        value={supplier?.termsOfDelivery || ''}
                        items={deliveryTermOptions}
                        onChange={(value) => handleChange('termsOfDelivery', value)}
                        labelWidth="w-28"
                        margintop="1"
                        allowRawValueLabel
                    />
                    <LabeledReactSelect
                        name="termsOfPayment"
                        label="Betalningsvillkor"
                        value={supplier?.termsOfPayment || ''}
                        items={paymentTermOptions}
                        onChange={(value) => handleChange('termsOfPayment', value)}
                        labelWidth="w-28"
                        margintop="1"
                        allowRawValueLabel
                    />
                </div>

                <div>
                    <LabeledInput label="Resultatenhet" value={supplier?.costCenter || ''} onChange={(value) => handleChange('costCenter', value)} labelWidth="w-38" margintop="0" />
                    <LabeledReactSelect name="currencyId" label="Valuta" value={supplier?.currencyId || ''} items={currencyOptions} onChange={(value) => handleChange('currencyId', value)} labelWidth="w-38" margintop="1" />
                    <LabeledReactSelect name="languageId" label="Sprak" value={supplier?.languageId || ''} items={languageOptions} onChange={(value) => handleChange('languageId', value)} labelWidth="w-38" margintop="1" allowRawValueLabel />
                    <LabeledReactSelect name="inquiryCommunicationTypeId" label="Forfragan" value={supplier?.inquiryCommunicationTypeId || ''} items={inquiryCommunicationOptions} onChange={(value) => handleChange('inquiryCommunicationTypeId', value)} labelWidth="w-38" margintop="1" allowRawValueLabel />
                    <LabeledReactSelect name="supplierOrderCommunicationTypeId" label="Bestallning" value={supplier?.supplierOrderCommunicationTypeId || ''} items={supplierOrderCommunicationOptions} onChange={(value) => handleChange('supplierOrderCommunicationTypeId', value)} labelWidth="w-38" margintop="1" allowRawValueLabel />

                    <LabeledSwitch field="active" name="active" label="Ar aktiv" value={Boolean(supplier?.active)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-38" marginTop={2} />
                    <LabeledSwitch field="econopackTransportResponsible" name="econopackTransportResponsible" label="Econopac trp.ansv." value={Boolean(supplier?.econopackTransportResponsible)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-38" />
                    <LabeledSwitch field="printForDocumentScanning" name="printForDocumentScanning" label="Utskr.scanning" value={Boolean(supplier?.printForDocumentScanning)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-38" />
                    <LabeledSwitch field="fscDefault" name="fscDefault" label="FSC" value={Boolean(supplier?.fscDefault)} onChange={(_rowId, field, checked) => handleChange(field, checked)} labelWidth="w-38" />
                    <LabeledInput label="Bestallningsmall" value={supplier?.supplierOrderTemplateNr ?? ''} onChange={(value) => handleChange('supplierOrderTemplateNr', value)} labelWidth="w-38" margintop="1" type="number" integerOnly />
                </div>
            </div>

            <div className="mt-10 flex gap-0 text-xs">
                <button
                    type="button"
                    onClick={() => setActiveTab('contacts')}
                    className={`min-w-44 border border-gray-300 px-6 py-2 ${activeTab === 'contacts' ? 'bg-sky-100 text-gray-800' : 'bg-white text-gray-700'}`}
                >
                    Kontaktpersoner
                </button>
                <button
                    type="button"
                    onClick={() => setActiveTab('factories')}
                    className={`min-w-36 border border-l-0 border-gray-300 px-6 py-2 ${activeTab === 'factories' ? 'bg-sky-100 text-gray-800' : 'bg-white text-gray-700'}`}
                >
                    Fabriker
                </button>
            </div>

            {activeTab === 'contacts' ? (
                <div className="mt-8 grid grid-cols-[420px_720px] gap-20">
                    <div>
                        <div className="border-t border-gray-300 pt-2 text-[10px] text-gray-600">
                            <div className="grid grid-cols-[1fr_1fr] px-4 pb-2">
                                <span>KONTAKTPERSON</span>
                                <span>BEFATTNING</span>
                            </div>
                            <div className="space-y-1 px-2">
                                {toArray(supplier?.contactPersons).length === 0 ? (
                                    <p className="px-2 py-4 text-xs text-gray-400">Inga kontaktpersoner.</p>
                                ) : (
                                    toArray(supplier?.contactPersons).map((contact) => {
                                        const identity = getItemIdentity(contact);
                                        const isSelected = selectedContactIdentity === identity;

                                        return (
                                            <button
                                                key={identity}
                                                type="button"
                                                onClick={() => setSelectedContactIdentity(identity)}
                                                className={`grid w-full grid-cols-[1fr_1fr] px-2 py-1 text-left text-sm ${isSelected ? 'bg-amber-100' : 'hover:bg-amber-50'}`}
                                            >
                                                <span className="truncate">{contact?.supplierContactPersonName || contact?.contactPerson || ''}</span>
                                                <span className="truncate">{contact?.title || ''}</span>
                                            </button>
                                        );
                                    })
                                )}
                            </div>
                        </div>

                        <button type="button" onClick={addContact} className="mt-16 w-28 bg-blue-800 px-4 py-[5px] text-xs text-white shadow-md/30 hover:bg-blue-900">
                            NY
                        </button>
                    </div>

                    <div className={selectedContact ? '' : 'pointer-events-none opacity-50'}>
                        <div className="flex justify-end">
                            <button
                                type="button"
                                onClick={() => selectedContact && deleteContact(getItemIdentity(selectedContact))}
                                className="mb-4 w-36 bg-blue-800 px-4 py-[5px] text-xs text-white shadow-md/30 hover:bg-blue-900"
                                disabled={!selectedContact}
                            >
                                RADERA
                            </button>
                        </div>

                        <div className="w-[600px]">
                            <LabeledInput label="Namn" value={selectedContact?.supplierContactPersonName || ''} onChange={(value) => selectedContact && handleContactChange(getItemIdentity(selectedContact), 'supplierContactPersonName', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Telefon" value={selectedContact?.telephone || ''} onChange={(value) => selectedContact && handleContactChange(getItemIdentity(selectedContact), 'telephone', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Mobiltelefon" value={selectedContact?.cellphone || ''} onChange={(value) => selectedContact && handleContactChange(getItemIdentity(selectedContact), 'cellphone', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Epostadress" value={selectedContact?.email || ''} onChange={(value) => selectedContact && handleContactChange(getItemIdentity(selectedContact), 'email', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Befattning" value={selectedContact?.title || ''} onChange={(value) => selectedContact && handleContactChange(getItemIdentity(selectedContact), 'title', value)} labelWidth="w-32" margintop="0" />
                            <LabeledSwitch field="mailInquiry" name="mailInquiry" label="Maila forfragan" value={Boolean(selectedContact?.mailInquiry)} onChange={(_rowId, field, checked) => selectedContact && handleContactChange(getItemIdentity(selectedContact), field, checked)} labelWidth="w-32" marginTop={2} />
                            <LabeledSwitch field="mailSupplierOrder" name="mailSupplierOrder" label="Maila bestallning" value={Boolean(selectedContact?.mailSupplierOrder)} onChange={(_rowId, field, checked) => selectedContact && handleContactChange(getItemIdentity(selectedContact), field, checked)} labelWidth="w-32" />
                            <LabeledSwitch field="doMailTransportOrder" name="doMailTransportOrder" label="Maila transportorder" value={Boolean(selectedContact?.doMailTransportOrder)} onChange={(_rowId, field, checked) => selectedContact && handleContactChange(getItemIdentity(selectedContact), field, checked)} labelWidth="w-32" />
                        </div>
                    </div>
                </div>
            ) : (
                <div className="mt-8 grid grid-cols-[420px_720px] gap-20">
                    <div>
                        <div className="border-t border-gray-300 pt-2 text-[10px] text-gray-600">
                            <div className="grid grid-cols-[1fr_120px] px-4 pb-2">
                                <span>FABRIK</span>
                                <span>LAND</span>
                            </div>
                            <div className="space-y-1 px-2">
                                {toArray(supplier?.factories).length === 0 ? (
                                    <p className="px-2 py-4 text-xs text-gray-400">Inga fabriker.</p>
                                ) : (
                                    toArray(supplier?.factories).map((factory) => {
                                        const identity = getItemIdentity(factory);
                                        const isSelected = selectedFactoryIdentity === identity;

                                        return (
                                            <button
                                                key={identity}
                                                type="button"
                                                onClick={() => setSelectedFactoryIdentity(identity)}
                                                className={`grid w-full grid-cols-[1fr_120px] px-2 py-1 text-left text-sm ${isSelected ? 'bg-amber-100' : 'hover:bg-amber-50'}`}
                                            >
                                                <span className="truncate">{factory?.name || ''}</span>
                                                <span className="truncate">{factory?.country || ''}</span>
                                            </button>
                                        );
                                    })
                                )}
                            </div>
                        </div>

                        <button type="button" onClick={addFactory} className="mt-16 w-28 bg-blue-800 px-4 py-[5px] text-xs text-white shadow-md/30 hover:bg-blue-900">
                            NY
                        </button>
                    </div>

                    <div className={selectedFactory ? '' : 'pointer-events-none opacity-50'}>
                        <div className="flex justify-end">
                            <button
                                type="button"
                                onClick={() => selectedFactory && deleteFactory(getItemIdentity(selectedFactory))}
                                className="mb-4 w-36 bg-blue-800 px-4 py-[5px] text-xs text-white shadow-md/30 hover:bg-blue-900"
                                disabled={!selectedFactory}
                            >
                                RADERA
                            </button>
                        </div>

                        <div className="w-[600px]">
                            <LabeledInput label="Namn" value={selectedFactory?.name || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'name', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Adress" value={selectedFactory?.address || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'address', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Postnr" value={selectedFactory?.postalNr || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'postalNr', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Ort" value={selectedFactory?.city || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'city', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Land" value={selectedFactory?.country || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'country', value)} labelWidth="w-32" margintop="0" />
                            <LabeledInput label="Adress extra" value={selectedFactory?.addressExtra || ''} onChange={(value) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), 'addressExtra', value)} labelWidth="w-32" margintop="0" />
                            <LabeledSwitch field="isDefault" name="isDefault" label="Huvudfabrik" value={Boolean(selectedFactory?.isDefault)} onChange={(_rowId, field, checked) => selectedFactory && handleFactoryChange(getItemIdentity(selectedFactory), field, checked)} labelWidth="w-32" marginTop={2} />
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default Supplier;