import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import { ArrowLeft, PanelLeftClose, PanelLeftOpen, Save, Trash2 } from 'lucide-react';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledTextArea from '../../components/LabeledTextArea';
import { parseNullableInt } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { useOrderMenu } from '../../layouts/OrderLayout';

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
    const orderMenu = useOrderMenu();

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
        <div className="relative flex flex-col h-full md:px-[clamp(4px,3vw,6vw)]">
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

            <div className="ml-83 mt-8 pb-3 flex items-center gap-3">
                <button
                    type="button"
                    onClick={() => orderMenu?.toggleMenu?.()}
                    className="inline-flex h-8 w-8 items-center justify-center text-gray-600 hover:bg-gray-50"
                    title={orderMenu?.isMenuOpen ? 'Dolj meny' : 'Visa meny'}
                    aria-label={orderMenu?.isMenuOpen ? 'Dolj meny' : 'Visa meny'}
                >
                    {orderMenu?.isMenuOpen ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
                </button>
                <h2 className="text-sm font-semibold uppercase tracking-[0.10em] text-gray-500">
                    {supplier?.id ? (
                        <>{supplier.name}</>
                    ) : 'Ny leverantor'}
                </h2>
            </div>

            <div className="flex h-full items-stretch pr-30">
                <div className="flex flex-col w-75 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            <div className="grid grid-cols-5 gap-4 mx-2">
                                {supplier?.createdAt && (
                                    <>
                                        <div className="col-span-1">
                                            <span className="font-medium">Skapad:</span>
                                        </div>
                                        <div className="col-span-2">
                                            {new Date(supplier.createdAt).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                        </div>
                                        <div className="col-span-2 text-gray-500">
                                            {supplier?.createdByUserName && `av ${supplier.createdByUserName}`}
                                        </div>
                                    </>
                                )}
                            </div>
                            <div className="grid grid-cols-5 gap-4 mx-2">
                                {supplier?.editedAt && (
                                    <>
                                        <div className="col-span-1">
                                            <span className="font-medium">Redigerad:</span>
                                        </div>
                                        <div className="col-span-2">
                                            {new Date(supplier.editedAt).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                        </div>
                                        <div className="col-span-2 text-gray-500">
                                            {supplier?.editedByUserName && `av ${supplier.editedByUserName}`}
                                        </div>
                                    </>
                                )}
                            </div>
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

                <div className="flex-grow ps-10 pe-10 py-2">
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
                        </div>
                        <div className="flex items-center gap-6">
                            {!isNewSupplier && supplier?.id !== 0 && (
                                <ActionButton
                                    label="Radera"
                                    icon={Trash2}
                                    onClick={() => setShowDeleteConfirm(true)}
                                    accent="rose"
                                />
                            )}
                        </div>
                    </div>

                    <div className="grid grid-cols-[3fr_3fr_2fr] gap-x-14">
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

                    <div className="grid grid-cols-2 gap-15 mt-15">
                        <span>
                            <p className="text-xs text-center border-b border-gray-300 pb-2">Kontaktpersoner</p>
                            <div className="grid grid-cols-2 gap-10 mt-2">
                                <div className="border-r border-gray-300 mt-1 px-2 h-full overflow-y-auto">
                                    {toArray(supplier?.contactPersons).length > 0 ? (
                                        <ul className="mt-2 space-y-2">
                                            {toArray(supplier?.contactPersons).map((contact) => {
                                                const itemIdentity = getItemIdentity(contact);
                                                const isSelected = selectedContactIdentity === itemIdentity;

                                                return (
                                                    <li
                                                        key={itemIdentity}
                                                        className={`cursor-pointer text-xs p-1 m-0 rounded-sm hover:bg-lime-50 ${isSelected ? 'bg-lime-200/50' : ''}`}
                                                        onClick={() => setSelectedContactIdentity(itemIdentity)}
                                                    >
                                                        <div className="flex justify-between items-center h-5 px-3">
                                                            <div className="">{contact?.supplierContactPersonName || contact?.contactPerson || ''}</div>
                                                            <div className="">
                                                                {contact?.title ? (
                                                                    <span className="inline-flex max-w-full items-center rounded-full bg-lime-600 px-3 py-[4px] text-[9px] font-medium text-slate-50">
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
                                        <button
                                            type="button"
                                            onClick={addContact}
                                            className="text-xs text-blue-600 hover:bg-blue-50 px-2 py-1 rounded border border-transparent hover:border-blue-200"
                                        >
                                            + Lägg till kontakt
                                        </button>
                                    </div>
                                </div>
                                <div className={(selectedContact ? '' : 'opacity-50 pointer-events-none') + ' mt-1'}>
                                    <LabeledInput label="Namn" value={selectedContact?.supplierContactPersonName || ''} onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'supplierContactPersonName', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Telefon" value={selectedContact?.telephone || ''} onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'telephone', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Mobil" value={selectedContact?.cellphone || ''} onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'cellphone', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Email" value={selectedContact?.email || ''} onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'email', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Titel" value={selectedContact?.title || ''} onChange={(value) => handleContactChange(getItemIdentity(selectedContact), 'title', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledSwitch
                                        field="mailInquiry"
                                        name="mailInquiry"
                                        label="Forfragan"
                                        value={Boolean(selectedContact?.mailInquiry)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="mailSupplierOrder"
                                        name="mailSupplierOrder"
                                        label="Bestallning"
                                        value={Boolean(selectedContact?.mailSupplierOrder)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <LabeledSwitch
                                        field="doMailTransportOrder"
                                        name="doMailTransportOrder"
                                        label="Transport"
                                        value={Boolean(selectedContact?.doMailTransportOrder)}
                                        onChange={(_rowId, field, checked) => handleContactChange(getItemIdentity(selectedContact), field, checked)}
                                        labelWidth="w-20"
                                        margintop="1"
                                    />
                                    <button
                                        type="button"
                                        onClick={() => deleteContact(getItemIdentity(selectedContact))}
                                        className="text-xs text-red-500 hover:text-red-700 mt-5"
                                    >
                                        Radera kontakt
                                    </button>
                                </div>
                            </div>
                        </span>

                        <span>
                            <p className="text-xs text-center border-b border-gray-300 pb-2">Fabriker</p>
                            <div className="grid grid-cols-2 gap-10 mt-2">
                                <div className="border-r border-gray-300 mt-1 px-2 h-full overflow-y-auto">
                                    {toArray(supplier?.factories).length > 0 ? (
                                        <ul className="mt-2 space-y-2">
                                            {toArray(supplier?.factories).map((factory) => {
                                                const itemIdentity = getItemIdentity(factory);
                                                const isSelected = selectedFactoryIdentity === itemIdentity;

                                                return (
                                                    <li
                                                        key={itemIdentity}
                                                        className={`cursor-pointer text-xs p-1 m-0 rounded-sm hover:bg-lime-50 ${isSelected ? 'bg-lime-200/50' : ''}`}
                                                        onClick={() => setSelectedFactoryIdentity(itemIdentity)}
                                                    >
                                                        <div className="flex justify-between items-center h-5 px-3">
                                                            <div className="">{factory?.name || ''}</div>
                                                            {/* <div className="text-gray-500">{factory?.country || ''}</div> */}
                                                        </div>
                                                    </li>
                                                );
                                            })}
                                        </ul>
                                    ) : (
                                        <p className="text-xs ms-1 font-light mt-5">Inga fabriker</p>
                                    )}
                                    <div className="mt-3">
                                        <button type="button" onClick={addFactory} className="text-xs text-blue-600 hover:bg-blue-50 px-2 py-1 rounded border border-transparent hover:border-blue-200">Lagg till fabrik</button>
                                    </div>
                                </div>
                                <div className={(selectedFactory ? '' : 'opacity-50 pointer-events-none') + ' mt-1'}>
                                    <LabeledInput label="Namn" value={selectedFactory?.name || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'name', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Adress" value={selectedFactory?.address || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'address', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Postnr" value={selectedFactory?.postalNr || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'postalNr', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Ort" value={selectedFactory?.city || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'city', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Land" value={selectedFactory?.country || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'country', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledInput label="Adr. extra" value={selectedFactory?.addressExtra || ''} onChange={(value) => handleFactoryChange(getItemIdentity(selectedFactory), 'addressExtra', value)} labelWidth="w-20" margintop="0" placeholder="" />
                                    <LabeledSwitch
                                        field="isDefault"
                                        name="isDefault"
                                        label="Huvudfabrik"
                                        value={Boolean(selectedFactory?.isDefault)}
                                        onChange={(_rowId, field, checked) => handleFactoryChange(getItemIdentity(selectedFactory), field, checked)}
                                        labelWidth="w-20"
                                        marginTop={4}
                                    />
                                    <button
                                        type="button"
                                        onClick={() => deleteFactory(getItemIdentity(selectedFactory))}
                                        className="text-xs text-red-500 hover:text-red-700 mt-5"
                                    >
                                        Radera fabrik
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

export default Supplier;