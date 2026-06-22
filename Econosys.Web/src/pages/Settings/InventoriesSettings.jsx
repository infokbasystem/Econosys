import { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSwitch from '../../components/LabeledSwitch';

const defaultForm = {
    id: null,
    name: '',
    accountNr: null,
    isInventory: true,
    email: '',
    isOmlast: false,
    address: '',
    postalNr: null,
    postalAddress: '',
    countryCode: '',
    country: '',
    postalNrText: '',
    noInventoryValue: false,
    positionId: null,
    addressExtra: '',
};

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: String(dto?.name ?? ''),
    accountNr: parseNullableInt(dto?.accountNr),
    isInventory: Boolean(dto?.isInventory),
    email: String(dto?.email ?? ''),
    isOmlast: Boolean(dto?.isOmlast),
    address: String(dto?.address ?? ''),
    postalNr: parseNullableInt(dto?.postalNr),
    postalAddress: String(dto?.postalAddress ?? ''),
    countryCode: String(dto?.countryCode ?? ''),
    country: String(dto?.country ?? ''),
    postalNrText: String(dto?.postalNrText ?? ''),
    noInventoryValue: Boolean(dto?.noInventoryValue),
    positionId: parseNullableInt(dto?.positionId),
    addressExtra: String(dto?.addressExtra ?? ''),
});

const mapFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    accountNr: parseNullableInt(form.accountNr),
    isInventory: Boolean(form.isInventory),
    email: String(form.email ?? '').trim() || null,
    isOmlast: Boolean(form.isOmlast),
    address: String(form.address ?? '').trim() || null,
    postalNr: parseNullableInt(form.postalNr),
    postalAddress: String(form.postalAddress ?? '').trim() || null,
    countryCode: String(form.countryCode ?? '').trim() || null,
    country: String(form.country ?? '').trim() || null,
    postalNrText: String(form.postalNrText ?? '').trim() || null,
    noInventoryValue: Boolean(form.noInventoryValue),
    positionId: parseNullableInt(form.positionId),
    addressExtra: String(form.addressExtra ?? '').trim() || null,
});

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? '').trim(),
    accountNr: parseNullableInt(form?.accountNr),
    isInventory: Boolean(form?.isInventory),
    email: String(form?.email ?? '').trim(),
    isOmlast: Boolean(form?.isOmlast),
    address: String(form?.address ?? '').trim(),
    postalNr: parseNullableInt(form?.postalNr),
    postalAddress: String(form?.postalAddress ?? '').trim(),
    countryCode: String(form?.countryCode ?? '').trim(),
    country: String(form?.country ?? '').trim(),
    postalNrText: String(form?.postalNrText ?? '').trim(),
    noInventoryValue: Boolean(form?.noInventoryValue),
    positionId: parseNullableInt(form?.positionId),
    addressExtra: String(form?.addressExtra ?? '').trim(),
});

const buildApiErrorMessage = (error, fallbackMessage) => {
    const data = error?.response?.data;

    if (typeof data === 'string' && data.trim()) {
        return data.trim();
    }

    if (typeof data?.message === 'string' && data.message.trim()) {
        return data.message.trim();
    }

    if (data?.errors && typeof data.errors === 'object') {
        const messages = Object.values(data.errors)
            .flatMap((entry) => (Array.isArray(entry) ? entry : []))
            .filter((entry) => typeof entry === 'string' && entry.trim())
            .map((entry) => entry.trim());

        if (messages.length > 0) {
            return messages.join(' ');
        }
    }

    return fallbackMessage;
};

const InventoriesSettings = () => {
    const [items, setItems] = useState([]);
    const [search, setSearch] = useState('');
    const [form, setForm] = useState(defaultForm);
    const [originalForm, setOriginalForm] = useState(defaultForm);
    const [isCreateMode, setIsCreateMode] = useState(false);
    const [isLoadingList, setIsLoadingList] = useState(true);
    const [isLoadingDetails, setIsLoadingDetails] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);
    const [message, setMessage] = useState(null);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);

    const isEditDisabled = isLoadingDetails || (!isCreateMode && !form.id);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.post('/inventories/search', {
                filter: {
                    conditions: [
                        {
                            field: 'isinventory',
                            operator: 'eq',
                            value: true,
                        },
                    ],
                },
                pagination: { pageNumber: 1, pageSize: 500 },
                orderBy: [{ field: 'name', direction: 'asc' }],
            });
            setItems(Array.isArray(response?.data?.items) ? response.data.items : []);
        } catch (error) {
            console.error('Failed to load inventories list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta lagerlistan.' });
        } finally {
            setIsLoadingList(false);
        }
    };

    useEffect(() => {
        loadList();
    }, []);

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
            setUnsavedAction({ type: 'navigate' });
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        if (!term) return items;

        return items.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            const accountNrText = String(item?.accountNr ?? '').toLowerCase();
            const emailText = String(item?.email ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term) || accountNrText.includes(term) || emailText.includes(term);
        });
    }, [items, search]);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const selectItem = async (id) => {
        if (!id) return;

        setIsCreateMode(false);
        setIsLoadingDetails(true);
        setMessage(null);
        try {
            const response = await apiClient.get(`/inventories/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
        } catch (error) {
            console.error('Failed to load inventory details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta lagerdetaljer.' });
        } finally {
            setIsLoadingDetails(false);
        }
    };

    const applyCreateNew = () => {
        setMessage(null);
        setIsCreateMode(true);
        setForm(defaultForm);
        setOriginalForm(defaultForm);
    };

    const handleCreateNew = () => {
        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'create' });
            setShowUnsavedWarning(true);
            return;
        }

        applyCreateNew();
    };

    const handleSelect = (id) => {
        if (form.id === id && !isCreateMode) return;

        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'select', id });
            setShowUnsavedWarning(true);
            return;
        }

        selectItem(id);
    };

    const handleSave = async () => {
        if (!String(form.name ?? '').trim()) {
            setMessage({ type: 'error', text: 'Namn maste anges.' });
            return;
        }

        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = isCreateMode
                ? await apiClient.post('/inventories', payload)
                : await apiClient.put(`/inventories/${form.id}`, payload);
            const mappedSaved = mapDtoToForm(response.data);
            setForm(mappedSaved);
            setOriginalForm(mappedSaved);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: isCreateMode ? 'Lager skapades.' : 'Lager uppdaterades.' });
        } catch (error) {
            console.error('Failed to save inventory:', error);
            setMessage({
                type: 'error',
                text: buildApiErrorMessage(error, isCreateMode ? 'Kunde inte skapa lager.' : 'Kunde inte uppdatera lager.'),
            });
        } finally {
            setIsSaving(false);
        }
    };

    const handleUnsavedWarningConfirm = () => {
        const action = unsavedAction;
        setShowUnsavedWarning(false);
        setUnsavedAction(null);

        if (action?.type === 'select' && action.id) {
            selectItem(action.id);
            return;
        }

        if (action?.type === 'create') {
            applyCreateNew();
            return;
        }

        if (blocker.state === 'blocked') {
            blocker.proceed();
        }
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        setUnsavedAction(null);

        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade andringar"
                message="Det finns osparade andringar, vill du fortsatta anda?"
                confirmText="Fortsatt anda"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Lager</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[450px] shrink-0 mb-5 px-4 py-2 border-r border-gray-300">
                    <div className="flex items-center gap-1">
                        <div className="mr-5 flex-grow">
                            <LabeledInput
                                label="Sok"
                                labelWidth="w-8"
                                inputWidth="w-30"
                                margintop="0"
                                value={search}
                                onChange={(value) => setSearch(value ?? '')}
                            />
                        </div>

                        <button
                            type="button"
                            onClick={handleCreateNew}
                            className="shadow-md/30 text-xs text-gray-900 bg-blue-200 hover:bg-blue-300 px-6 p-[5px] rounded-sm"
                        >
                            Skapa ny
                        </button>
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga lager</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;
                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelect(item.id)}
                                        className={`w-full text-left text-xs px-2 py-0.5 rounded-sm grid grid-cols-[1fr_80px] gap-2 ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="truncate">{item.name}</span>
                                        <span className="truncate text-right">{item.accountNr ?? ''}</span>
                                    </button>
                                );
                            })
                        )}
                    </div>
                </div>

                <div className="flex-1 min-w-0 px-10 py-2 overflow-x-auto">
                    <div className="flex items-center gap-5 mb-6 mt-1">
                        <button
                            type="button"
                            onClick={handleSave}
                            disabled={isSaving || isEditDisabled}
                            className="w-20 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isSaving ? 'Sparar...' : 'Spara'}
                        </button>
                    </div>

                    <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>

                        <div className="w-140">
                            <LabeledInput
                                label="Namn"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.name}
                                onChange={(value) => updateField('name', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={100}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Kontonummer"
                                labelWidth="w-28"
                                inputWidth="w-30"
                                margintop="0"
                                type="number"
                                integerOnly
                                value={form.accountNr}
                                onChange={(value) => updateField('accountNr', value)}
                                disabled={isEditDisabled}
                            />

                            <LabeledInput
                                label="Epost"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.email}
                                onChange={(value) => updateField('email', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Adress"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.address}
                                onChange={(value) => updateField('address', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Adressrad 2"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.addressExtra}
                                onChange={(value) => updateField('addressExtra', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Postnummer"
                                labelWidth="w-28"
                                inputWidth="w-30"
                                margintop="0"
                                value={form.postalNr}
                                onChange={(value) => updateField('postalNr', value)}
                                disabled={isEditDisabled}
                            />

                            <LabeledInput
                                label="Postnummer utskrift"
                                labelWidth="w-28"
                                inputWidth="w-30"
                                margintop="0"
                                value={form.postalNrText}
                                onChange={(value) => updateField('postalNrText', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Postadress"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.postalAddress}
                                onChange={(value) => updateField('postalAddress', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Landkod"
                                labelWidth="w-28"
                                inputWidth="w-20"
                                margintop="0"
                                value={form.countryCode}
                                onChange={(value) => updateField('countryCode', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={10}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Land"
                                labelWidth="w-28"
                                margintop="0"
                                value={form.country}
                                onChange={(value) => updateField('country', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledSwitch
                                id="inventory-is-inventory"
                                name="inventory-is-inventory"
                                label="Ar lager"
                                labelWidth="w-28"
                                marginTop={18}
                                value={form.isInventory}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('isInventory', checked)}
                            />

                            <LabeledSwitch
                                id="inventory-is-omlast"
                                name="inventory-is-omlast"
                                label="Ar omlast"
                                labelWidth="w-28"
                                marginTop={4}
                                value={form.isOmlast}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('isOmlast', checked)}
                            />

                            <LabeledSwitch
                                id="inventory-no-value"
                                name="inventory-no-value"
                                label="Inget lagervarde"
                                labelWidth="w-28"
                                marginTop={4}
                                value={form.noInventoryValue}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('noInventoryValue', checked)}
                            />
                        </div>
                    </div>
                </div>

                <div className="flex flex-col w-80 shrink-0 border-l border-gray-300 pl-4 py-2 mb-5">
                    <h2 className="text-sm text-center text-gray-700 mt-1">Meddelanden</h2>
                    {!message ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            <li className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' : message.type === 'success' ? 'bg-green-100 text-green-700' : 'bg-blue-100 text-blue-700'}`}>
                                {message.text}
                            </li>
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
};

export default InventoriesSettings;
