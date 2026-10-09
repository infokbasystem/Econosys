import { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import { Plus, Save, Trash2 } from 'lucide-react';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSelect from '../../components/LabeledSelect';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import ActionButton from '../../components/ActionButton';
import NumberInput from '../../components/NumberInput';

// Stored as fractions (0.12) in the DB, edited as percent (12) in the UI.
const PERCENT_FIELDS = [
    'dmtPercentPallet',
    'additionCurrencyPercentPallet',
    'otherPercentPallet',
    'additionTotalPercentPallet',
    'dmtPercentFtl',
    'additionCurrencyPercentFtl',
    'otherPercentFtl',
    'additionTotalPercentFtl',
];

const AMOUNT_FIELDS = [
    'secaMarpolPallet',
    'otherPallet',
    'secaMarpolFtl',
    'otherFtl',
    'loadingCost',
    'unloadingCost',
    'preCalcAdditionPercent',
];

const PALLET_FTL_ROWS = [
    { label: 'SECA/Marpol', pallet: 'secaMarpolPallet', ftl: 'secaMarpolFtl', decimals: 2 },
    { label: 'DMT/bränsle (%)', pallet: 'dmtPercentPallet', ftl: 'dmtPercentFtl', decimals: 3 },
    { label: 'Valutatillägg (%)', pallet: 'additionCurrencyPercentPallet', ftl: 'additionCurrencyPercentFtl', decimals: 3 },
    { label: 'Övrigt', pallet: 'otherPallet', ftl: 'otherFtl', decimals: 2 },
    { label: 'Övrigt (%)', pallet: 'otherPercentPallet', ftl: 'otherPercentFtl', decimals: 3 },
    { label: 'Påslag totalt (%)', pallet: 'additionTotalPercentPallet', ftl: 'additionTotalPercentFtl', decimals: 3 },
];

const PRICE_FIELDS = [...Array.from({ length: 32 }, (_, index) => `p${index + 1}`), 'pftl'];

const ROW_FIELDS = ['countryCode', 'postalNrFrom', 'postalNrTo', 'transhipment', 'additionSekPerPallet', ...PRICE_FIELDS];

const EMPTY_ROWS = [];
const EMPTY_IDS = [];

const defaultForm = {
    id: null,
    name: '',
    importName: '',
    lastImportDateTime: null,
    currencyId: '',
    isStafflad: false,
    ...Object.fromEntries([...PERCENT_FIELDS, ...AMOUNT_FIELDS].map((field) => [field, null])),
    rows: EMPTY_ROWS,
    supplierFactoryIds: EMPTY_IDS,
    inventoryIds: EMPTY_IDS,
};

let newRowCounter = 0;
const nextNewRowKey = () => {
    newRowCounter += 1;
    return `new-${newRowCounter}`;
};

const toNullableNumber = (value) => {
    if (value === null || value === undefined || value === '') return null;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
};

const fractionToPercent = (value) => {
    const parsed = toNullableNumber(value);
    return parsed === null ? null : Math.round(parsed * 100 * 1000) / 1000;
};

const percentToFraction = (value) => {
    const parsed = toNullableNumber(value);
    return parsed === null ? null : Math.round(parsed * 1000) / 100000;
};

const formatLegacyDateTime = (value) => {
    if (!value) return '';
    // Legacy value is Swedish wall-clock time without timezone; show it as stored.
    return String(value).replace('T', ' ').slice(0, 16);
};

const mapRowFromDto = (row) => ({
    rowKey: `id-${row.id}`,
    id: row.id,
    countryCode: String(row.countryCode ?? ''),
    postalNrFrom: toNullableNumber(row.postalNrFrom),
    postalNrTo: toNullableNumber(row.postalNrTo),
    transhipment: String(row.transhipment ?? ''),
    additionSekPerPallet: toNullableNumber(row.additionSekPerPallet),
    ...Object.fromEntries(PRICE_FIELDS.map((field) => [field, toNullableNumber(row[field])])),
});

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: String(dto?.name ?? ''),
    importName: String(dto?.importName ?? ''),
    lastImportDateTime: dto?.lastImportDateTime ?? null,
    currencyId: dto?.currencyId != null ? String(dto.currencyId) : '',
    isStafflad: Boolean(dto?.isStafflad),
    ...Object.fromEntries(PERCENT_FIELDS.map((field) => [field, fractionToPercent(dto?.[field])])),
    ...Object.fromEntries(AMOUNT_FIELDS.map((field) => [field, toNullableNumber(dto?.[field])])),
    rows: Array.isArray(dto?.priceListData) ? dto.priceListData.map(mapRowFromDto) : [],
    supplierFactoryIds: Array.isArray(dto?.supplierFactories)
        ? dto.supplierFactories.map((link) => link.supplierFactoryId)
        : [],
    inventoryIds: Array.isArray(dto?.inventories)
        ? dto.inventories.map((link) => link.inventoryId)
        : [],
});

const mapRowToPayload = (row) => ({
    id: row.id ?? null,
    countryCode: String(row.countryCode ?? '').trim() || null,
    postalNrFrom: toNullableNumber(row.postalNrFrom),
    postalNrTo: toNullableNumber(row.postalNrTo),
    transhipment: String(row.transhipment ?? '').trim() || null,
    additionSekPerPallet: toNullableNumber(row.additionSekPerPallet),
    ...Object.fromEntries(PRICE_FIELDS.map((field) => [field, toNullableNumber(row[field])])),
});

const mapFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    currencyId: form.currencyId === '' ? null : Number(form.currencyId),
    isStafflad: Boolean(form.isStafflad),
    ...Object.fromEntries(PERCENT_FIELDS.map((field) => [field, percentToFraction(form[field])])),
    ...Object.fromEntries(AMOUNT_FIELDS.map((field) => [field, toNullableNumber(form[field])])),
    rows: form.rows.map(mapRowToPayload),
    supplierFactoryIds: [...form.supplierFactoryIds].sort((a, b) => a - b),
    inventoryIds: [...form.inventoryIds].sort((a, b) => a - b),
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

const LinkList = ({
    title,
    availableTitle,
    options,
    selectedIds,
    getLabel,
    onAdd,
    onRemove,
    disabled,
}) => {
    const [filter, setFilter] = useState('');

    const selectedSet = useMemo(() => new Set(selectedIds), [selectedIds]);

    const connected = useMemo(
        () => options.filter((option) => selectedSet.has(option.id)),
        [options, selectedSet],
    );

    const available = useMemo(() => {
        const term = filter.trim().toLowerCase();
        return options.filter((option) => {
            if (selectedSet.has(option.id)) return false;
            if (!term) return true;
            return getLabel(option).toLowerCase().includes(term);
        });
    }, [options, selectedSet, filter, getLabel]);

    return (
        <div className="w-60 shrink-0">
            <div className="flex items-center justify-between mb-1 pl-1 gap-5">
                <h3 className="text-xs text-gray-700 mb-1">{availableTitle}</h3>
                <LabeledInput
                    inputWidth="w-20"
                    margintop="0"
                    value={filter}
                    onChange={(value) => setFilter(value ?? '')}
                    disabled={disabled}
                />
            </div>
            <div className="mt-1 h-28 overflow-y-auto border border-gray-200 rounded-sm bg-white/70">
                {available.length === 0 ? (
                    <p className="text-xs text-gray-500 px-2 py-1">Inga</p>
                ) : (
                    available.map((option) => (
                        <div
                            key={option.id}
                            className="flex items-center justify-between gap-2 px-2 h-6 text-xs hover:bg-gray-100"
                        >
                            <span className="truncate">{getLabel(option)}</span>
                            <button
                                type="button"
                                onClick={() => onAdd(option.id)}
                                disabled={disabled}
                                className="shrink-0 text-sky-700 hover:underline disabled:opacity-50 disabled:no-underline"
                            >
                                Lägg till
                            </button>
                        </div>
                    ))
                )}
            </div>

            <h3 className="pl-1 text-xs text-gray-700 mt-3 mt-2 mb-1">{title}</h3>
            <div className="h-24 overflow-y-auto border border-gray-200 rounded-sm bg-white/70">
                {connected.length === 0 ? (
                    <p className="text-xs text-gray-500 px-2 py-1">Inga kopplade</p>
                ) : (
                    connected.map((option) => (
                        <div
                            key={option.id}
                            className="flex items-center justify-between gap-2 px-2 h-6 text-xs hover:bg-gray-100"
                        >
                            <span className="truncate">{getLabel(option)}</span>
                            <button
                                type="button"
                                onClick={() => onRemove(option.id)}
                                disabled={disabled}
                                className="shrink-0 text-red-700 hover:underline disabled:opacity-50 disabled:no-underline"
                            >
                                Ta bort
                            </button>
                        </div>
                    ))
                )}
            </div>
        </div>
    );
};

const getSupplierFactoryLabel = (option) => [option.supplierName, option.factoryName].filter(Boolean).join(', ');
const getInventoryLabel = (option) => option.name ?? '';

const TransportPricelists = () => {
    const [items, setItems] = useState([]);
    const [formOptions, setFormOptions] = useState({ currencies: [], supplierFactories: [], inventories: [] });
    const [search, setSearch] = useState('');
    const [form, setForm] = useState(defaultForm);
    const [originalForm, setOriginalForm] = useState(defaultForm);
    const [isCreateMode, setIsCreateMode] = useState(false);
    const [isLoadingList, setIsLoadingList] = useState(true);
    const [isLoadingDetails, setIsLoadingDetails] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [message, setMessage] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        if (!isCreateMode && !form.id) return false;
        return JSON.stringify(mapFormToPayload(form)) !== JSON.stringify(mapFormToPayload(originalForm));
    }, [form, originalForm, isLoadingDetails, isCreateMode]);

    const blocker = useBlocker(hasUnsavedChanges);
    const isEditDisabled = isLoadingDetails || (!isCreateMode && !form.id);

    const loadList = useCallback(async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.get('/transportcostpricelist');
            setItems(Array.isArray(response?.data) ? response.data : []);
        } catch (error) {
            console.error('Failed to load transport price lists:', error);
            setMessage({ type: 'error', text: 'Kunde inte hämta transportprislistor.' });
        } finally {
            setIsLoadingList(false);
        }
    }, []);

    useEffect(() => {
        let isActive = true;

        const loadInitial = async () => {
            try {
                const [listResponse, optionsResponse] = await Promise.all([
                    getSharedRequest('transportpricelists:list', () => apiClient.get('/transportcostpricelist')),
                    getSharedRequest('transportpricelists:form-options', () => apiClient.get('/transportcostpricelist/form-options')),
                ]);

                if (!isActive) return;

                setItems(Array.isArray(listResponse?.data) ? listResponse.data : []);
                setFormOptions({
                    currencies: optionsResponse?.data?.currencies ?? [],
                    supplierFactories: optionsResponse?.data?.supplierFactories ?? [],
                    inventories: optionsResponse?.data?.inventories ?? [],
                });
            } catch (error) {
                console.error('Failed to load transport price list page data:', error);
                if (isActive) {
                    setMessage({ type: 'error', text: 'Kunde inte hämta transportprislistor.' });
                }
            } finally {
                if (isActive) {
                    setIsLoadingList(false);
                }
            }
        };

        loadInitial();

        return () => {
            isActive = false;
        };
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

        return items.filter((item) => String(item?.name ?? '').toLowerCase().includes(term));
    }, [items, search]);

    const currencyItems = useMemo(
        () => [{ id: '', name: 'Ej vald' }, ...formOptions.currencies],
        [formOptions.currencies],
    );

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const updateRow = (rowKey, field, value) => {
        setForm((prev) => ({
            ...prev,
            rows: prev.rows.map((row) => (row.rowKey === rowKey ? { ...row, [field]: value } : row)),
        }));
    };

    const addRow = () => {
        setForm((prev) => ({
            ...prev,
            rows: [
                ...prev.rows,
                {
                    rowKey: nextNewRowKey(),
                    id: null,
                    ...Object.fromEntries(ROW_FIELDS.map((field) => [field, null])),
                    countryCode: '',
                    transhipment: '',
                },
            ],
        }));
    };

    const removeRow = (rowKey) => {
        setForm((prev) => ({
            ...prev,
            rows: prev.rows.filter((row) => row.rowKey !== rowKey),
        }));
    };

    const addLink = (field, id) => {
        setForm((prev) => (prev[field].includes(id) ? prev : { ...prev, [field]: [...prev[field], id] }));
    };

    const removeLink = (field, id) => {
        setForm((prev) => ({ ...prev, [field]: prev[field].filter((value) => value !== id) }));
    };

    const selectItem = async (id) => {
        if (!id) return;

        setIsCreateMode(false);
        setIsLoadingDetails(true);
        setMessage(null);

        try {
            const response = await apiClient.get(`/transportcostpricelist/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
        } catch (error) {
            console.error('Failed to load transport price list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hämta prislistan.' });
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
            setMessage({ type: 'error', text: 'Namn måste anges.' });
            return;
        }

        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = isCreateMode
                ? await apiClient.post('/transportcostpricelist', payload)
                : await apiClient.put(`/transportcostpricelist/${form.id}`, payload);

            const saved = mapDtoToForm(response.data);
            setForm(saved);
            setOriginalForm(saved);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: isCreateMode ? 'Prislistan skapades.' : 'Prislistan uppdaterades.' });
        } catch (error) {
            console.error('Failed to save transport price list:', error);
            setMessage({
                type: 'error',
                text: buildApiErrorMessage(error, isCreateMode ? 'Kunde inte skapa prislistan.' : 'Kunde inte uppdatera prislistan.'),
            });
        } finally {
            setIsSaving(false);
        }
    };

    const handleDeleteClick = () => {
        if (!form.id || isDeleting || isSaving || isEditDisabled) return;
        setShowDeleteConfirm(true);
    };

    const handleDeleteConfirm = async () => {
        if (!form.id) return;

        setShowDeleteConfirm(false);
        setIsDeleting(true);
        setMessage(null);

        try {
            await apiClient.delete(`/transportcostpricelist/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Prislistan raderades.' });
        } catch (error) {
            console.error('Failed to delete transport price list:', error);
            setMessage({ type: 'error', text: buildApiErrorMessage(error, 'Kunde inte radera prislistan.') });
        } finally {
            setIsDeleting(false);
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

    const cellInputClassName = 'w-full text-xs bg-transparent border border-transparent rounded-sm py-0.5 hover:border-gray-300 focus:border-gray-400 focus:bg-white disabled:text-gray-500';

    return (
        <div className="relative flex flex-col h-full min-w-0">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteConfirm}
                title="RADERA PRISLISTA"
                message={`Är du säker på att du vill radera prislistan ${form.name || form.id}? Kopplingar till fabriker och omlastningsplatser tas också bort. Åtgärden kan inte ångras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade ändringar"
                message="Det finns osparade ändringar, vill du fortsätta ändå?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Transportprislistor</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[250px] shrink-0 mb-5 px-4 py-2 border-r border-gray-300">
                    <div className="flex items-center gap-1">
                        <div className="mr-5 flex-grow">
                            <LabeledInput
                                label="Sök"
                                labelWidth="w-8"
                                inputWidth="w-30"
                                margintop="0"
                                value={search}
                                onChange={(value) => setSearch(value ?? '')}
                            />
                        </div>

                        <ActionButton
                            label="Ny"
                            icon={Plus}
                            onClick={handleCreateNew}
                        />
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga prislistor</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;
                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelect(item.id)}
                                        className={`w-full h-6 items-center text-left text-xs px-2 py-0.5 rounded-sm ${isSelected ? 'bg-purple-200/50 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="block truncate">{item.name}</span>
                                    </button>
                                );
                            })
                        )}
                    </div>
                </div>

                <div className="flex-1 min-w-0 px-10 py-1">
                    <div className="flex items-center gap-5 mb-6 mt-1">
                        <ActionButton
                            label={isSaving ? 'Sparar...' : 'Spara'}
                            icon={Save}
                            onClick={handleSave}
                            disabled={isSaving || isEditDisabled}
                            accent="lime"
                        />

                        <ActionButton
                            label={isDeleting ? 'Raderar...' : 'Radera'}
                            icon={Trash2}
                            onClick={handleDeleteClick}
                            disabled={!form.id || isDeleting || isSaving || isEditDisabled}
                            accent="rose"
                        />
                    </div>

                    <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <div className="flex flex-wrap items-start gap-x-10 gap-y-6">
                            <div className="w-50 shrink-0">
                                <LabeledInput
                                    label="Namn"
                                    labelWidth="w-16"
                                    margintop="0"
                                    value={form.name}
                                    onChange={(value) => updateField('name', value ?? '')}
                                    disabled={isEditDisabled}
                                    maxLength={100}
                                />

                                {/* <LabeledInput
                                    label="Importnamn"
                                    labelWidth="w-16"
                                    margintop="0"
                                    value={form.importName}
                                    disabled
                                />

                                <LabeledInput
                                    label="Senast importerad"
                                    labelWidth="w-16"
                                    margintop="0"
                                    value={formatLegacyDateTime(form.lastImportDateTime)}
                                    disabled
                                /> */}

                                <LabeledSelect
                                    label="Valuta"
                                    labelWidth="w-16"
                                    margintop="0"
                                    value={form.currencyId}
                                    items={currencyItems}
                                    onChange={(value) => updateField('currencyId', value)}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Loss"
                                    labelWidth="w-28"
                                    margintop="0"
                                    type="number"
                                    value={form.unloadingCost}
                                    onChange={(value) => updateField('unloadingCost', value)}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Påslag f-kalkyl (%)"
                                    labelWidth="w-28"
                                    margintop="0"
                                    type="number"
                                    value={form.preCalcAdditionPercent}
                                    onChange={(value) => updateField('preCalcAdditionPercent', value)}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Lass"
                                    labelWidth="w-28"
                                    margintop="0"
                                    type="number"
                                    value={form.loadingCost}
                                    onChange={(value) => updateField('loadingCost', value)}
                                    disabled={isEditDisabled}
                                />

                                <LabeledCheckbox
                                    name="isStafflad"
                                    label="Stafflad"
                                    labelWidth="w-26"
                                    checked={form.isStafflad}
                                    onChange={(value) => updateField('isStafflad', Boolean(value))}
                                    disabled={isEditDisabled}
                                    ariaLabel="Stafflad"
                                    labelPosition='left'
                                />
                            </div>

                            <div className="shrink-0">
                                <div className="grid grid-cols-[6rem_4rem_4rem] items-center gap-x-2 gap-y-[1px]">
                                    <span />
                                    <span className="text-xs text-gray-700 text-right pr-2">Pall</span>
                                    <span className="text-xs text-gray-700 text-right pr-2">Ftl</span>

                                    {PALLET_FTL_ROWS.map((row) => (
                                        <div key={row.label} className="contents">
                                            <label className="text-xs text-gray-700">{row.label}</label>
                                            <NumberInput
                                                value={form[row.pallet]}
                                                decimals={row.decimals}
                                                disabled={isEditDisabled}
                                                onChange={(_, __, value) => updateField(row.pallet, value)}
                                                className="text-xs border border-gray-300 rounded-sm py-1 bg-white disabled:bg-transparent disabled:text-gray-500"
                                            />
                                            <NumberInput
                                                value={form[row.ftl]}
                                                decimals={row.decimals}
                                                disabled={isEditDisabled}
                                                onChange={(_, __, value) => updateField(row.ftl, value)}
                                                className="text-xs border border-gray-300 rounded-sm py-1 bg-white disabled:bg-transparent disabled:text-gray-500"
                                            />
                                        </div>
                                    ))}
                                </div>
                            </div>

                            <LinkList
                                availableTitle="Ej kopplade fabriker"
                                title="Kopplade fabriker"
                                options={formOptions.supplierFactories}
                                selectedIds={form.supplierFactoryIds}
                                getLabel={getSupplierFactoryLabel}
                                onAdd={(id) => addLink('supplierFactoryIds', id)}
                                onRemove={(id) => removeLink('supplierFactoryIds', id)}
                                disabled={isEditDisabled}
                            />

                            <LinkList
                                availableTitle="Ej kopplade omlastningsplatser"
                                title="Kopplade omlastningsplatser"
                                options={formOptions.inventories}
                                selectedIds={form.inventoryIds}
                                getLabel={getInventoryLabel}
                                onAdd={(id) => addLink('inventoryIds', id)}
                                onRemove={(id) => removeLink('inventoryIds', id)}
                                disabled={isEditDisabled}
                            />
                        </div>

                        <div className="mt-8 flex items-center justify-between">
                            <h3 className="pl-1 text-xs text-gray-700 font-medium tracking-wider">PRISER ({form.rows.length} rader)</h3>
                            <ActionButton
                                label="Lägg till rad"
                                icon={Plus}
                                onClick={addRow}
                                disabled={isEditDisabled}
                            />
                        </div>

                        <div className="mt-2 overflow-auto max-h-[calc(100vh-460px)] min-h-40 border border-gray-200 rounded-sm bg-white/70">
                            <table className="text-xs border-collapse table-fixed" style={{ width: `${40 + 56 + 64 + 64 + 110 + 72 + PRICE_FIELDS.length * 64}px` }}>
                                <colgroup>
                                    <col style={{ width: 40 }} />
                                    <col style={{ width: 56 }} />
                                    <col style={{ width: 64 }} />
                                    <col style={{ width: 64 }} />
                                    <col style={{ width: 110 }} />
                                    <col style={{ width: 72 }} />
                                    {PRICE_FIELDS.map((field) => (
                                        <col key={field} style={{ width: 64 }} />
                                    ))}
                                </colgroup>
                                <thead className="sticky top-0 z-10 bg-gray-100 text-gray-700">
                                    <tr>
                                        <th className="px-1 py-1" />
                                        <th className="px-1 py-1 text-left font-medium">LAND</th>
                                        <th className="px-1 py-1 text-right font-medium">PNR FRÅN</th>
                                        <th className="px-1 py-1 text-right font-medium">PNR TILL</th>
                                        <th className="px-1 py-1 text-left font-medium">OMLAST</th>
                                        <th className="px-1 py-1 text-right font-medium">P. SEK/PALL</th>
                                        {PRICE_FIELDS.map((field) => (
                                            <th key={field} className="px-1 py-1 text-right font-medium">
                                                {field === 'pftl' ? 'FTL' : field.slice(1)}
                                            </th>
                                        ))}
                                    </tr>
                                </thead>
                                <tbody>
                                    {form.rows.length === 0 ? (
                                        <tr>
                                            <td colSpan={6 + PRICE_FIELDS.length} className="px-2 py-2 text-gray-500">
                                                Inga prisrader
                                            </td>
                                        </tr>
                                    ) : (
                                        form.rows.map((row) => (
                                            <tr key={row.rowKey} className="border-t border-gray-100 hover:bg-lime-200/40">
                                                <td className="px-1 text-center">
                                                    <button
                                                        type="button"
                                                        onClick={() => removeRow(row.rowKey)}
                                                        disabled={isEditDisabled}
                                                        className="text-gray-500 hover:text-red-700 disabled:opacity-50"
                                                        aria-label="Ta bort rad"
                                                    >
                                                        <Trash2 className="h-3.5 w-3.5" />
                                                    </button>
                                                </td>
                                                <td className="px-0.5">
                                                    <input
                                                        type="text"
                                                        value={row.countryCode}
                                                        maxLength={10}
                                                        disabled={isEditDisabled}
                                                        onChange={(event) => updateRow(row.rowKey, 'countryCode', event.target.value)}
                                                        className={`${cellInputClassName} px-1`}
                                                    />
                                                </td>
                                                <td className="px-0.5">
                                                    <NumberInput
                                                        rowId={row.rowKey}
                                                        field="postalNrFrom"
                                                        value={row.postalNrFrom}
                                                        decimals={0}
                                                        disabled={isEditDisabled}
                                                        onChange={updateRow}
                                                        className={cellInputClassName}
                                                    />
                                                </td>
                                                <td className="px-0.5">
                                                    <NumberInput
                                                        rowId={row.rowKey}
                                                        field="postalNrTo"
                                                        value={row.postalNrTo}
                                                        decimals={0}
                                                        disabled={isEditDisabled}
                                                        onChange={updateRow}
                                                        className={cellInputClassName}
                                                    />
                                                </td>
                                                <td className="px-0.5">
                                                    <input
                                                        type="text"
                                                        value={row.transhipment}
                                                        maxLength={100}
                                                        disabled={isEditDisabled}
                                                        onChange={(event) => updateRow(row.rowKey, 'transhipment', event.target.value)}
                                                        className={`${cellInputClassName} px-1`}
                                                    />
                                                </td>
                                                <td className="px-0.5">
                                                    <NumberInput
                                                        rowId={row.rowKey}
                                                        field="additionSekPerPallet"
                                                        value={row.additionSekPerPallet}
                                                        hideZeroDecimals
                                                        disabled={isEditDisabled}
                                                        onChange={updateRow}
                                                        className={cellInputClassName}
                                                    />
                                                </td>
                                                {PRICE_FIELDS.map((field) => (
                                                    <td key={field} className="px-0.5">
                                                        <NumberInput
                                                            rowId={row.rowKey}
                                                            field={field}
                                                            value={row[field]}
                                                            hideZeroDecimals
                                                            disabled={isEditDisabled}
                                                            onChange={updateRow}
                                                            className={cellInputClassName}
                                                        />
                                                    </td>
                                                ))}
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>

                {/* <div className="flex flex-col w-80 shrink-0 border-l border-gray-300 pl-4 py-2 mb-5">
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
                </div> */}
            </div>
        </div>
    );
};

export default TransportPricelists;
