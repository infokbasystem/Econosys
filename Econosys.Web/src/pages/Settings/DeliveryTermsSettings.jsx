import { useEffect, useMemo, useState } from 'react';
import SwitchSelector from 'react-switch-selector';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSwitch from '../../components/LabeledSwitch';

const defaultForm = {
    id: null,
    name: '',
    translationCode: null,
    translations: [],
    active: true,
    isDefault: false,
};

const visibilityOptions = [
    {
        label: <span className="text-tiny pt-[1px] pb-[1px]">Aktiva</span>,
        value: false,
        selectedBackgroundColor: '#16a34a',
        fontColor: '#374151',
    },
    {
        label: <span className="text-tiny pt-[1px] pb-[1px]">Inaktiva</span>,
        value: true,
        selectedBackgroundColor: '#ef4444',
    },
];

const mapTranslations = (translations) => {
    if (!Array.isArray(translations)) return [];

    return translations
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item?.langCode ?? '').trim(),
            translation: String(item?.translation ?? ''),
        }));
};

const createEmptyTranslations = (template) =>
    (Array.isArray(template) ? template : [])
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item.langCode).trim(),
            translation: '',
        }));

const formatLangLabel = (langCode) => {
    const normalized = String(langCode ?? '')
        .replaceAll('_', ' ')
        .replaceAll('-', ' ')
        .trim();

    if (!normalized) return '';

    return normalized
        .split(/\s+/)
        .map((part) => {
            if (part.length <= 3) return part.toUpperCase();
            return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
        })
        .join(' ');
};

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;

    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: dto?.name ?? '',
    translationCode: dto?.translationCode ?? null,
    translations: mapTranslations(dto?.translations),
    active: Boolean(dto?.active),
    isDefault: Boolean(dto?.isDefault),
});

const mapFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    translationCode: parseNullableInt(form.translationCode),
    translations: (form.translations ?? []).map((item) => ({
        langCode: item.langCode,
        translation: String(item.translation ?? ''),
    })),
    active: Boolean(form.active),
    isDefault: Boolean(form.isDefault),
});

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? '').trim(),
    translationCode: parseNullableInt(form?.translationCode),
    active: Boolean(form?.active),
    isDefault: Boolean(form?.isDefault),
    translations: (form?.translations ?? [])
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item.langCode).trim(),
            translation: String(item.translation ?? ''),
        })),
});

const buildDeleteConflictMessage = (error) => {
    const data = error?.response?.data;

    if (typeof data === 'string' && data.trim()) {
        return data.trim();
    }

    const message = typeof data?.message === 'string' ? data.message.trim() : '';
    const details = Array.isArray(data?.details)
        ? data.details.filter((item) => typeof item === 'string' && item.trim()).map((item) => item.trim())
        : [];

    if (message && details.length > 0) {
        return `${message} ${details.join(' ')}`;
    }

    if (message) {
        return message;
    }

    if (details.length > 0) {
        return details.join(' ');
    }

    return 'Kunde inte radera leveransvillkor.';
};

const DeliveryTermsSettings = () => {
    const [items, setItems] = useState([]);
    const [search, setSearch] = useState('');
    const [showInactive, setShowInactive] = useState(false);
    const [form, setForm] = useState(defaultForm);
    const [isCreateMode, setIsCreateMode] = useState(false);
    const [isLoadingList, setIsLoadingList] = useState(true);
    const [isLoadingDetails, setIsLoadingDetails] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [message, setMessage] = useState(null);
    const [translationTemplate, setTranslationTemplate] = useState([]);
    const [originalForm, setOriginalForm] = useState(defaultForm);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

    const isEditDisabled = isLoadingDetails || (!form.id && !isCreateMode);

    const hasUnsavedChanges = useMemo(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const loadList = async () => {
        setIsLoadingList(true);

        try {
            const response = await apiClient.get('/termsofdelivery/search');
            setItems(response?.data ?? []);
            setMessage({ type: 'info', text: 'Leveransvillkor hamtas fran API:t.' });
        } catch (error) {
            console.error('Failed to load delivery terms list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta leveransvillkor.' });
        } finally {
            setIsLoadingList(false);
        }
    };

    useEffect(() => {
        loadList();
    }, []);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        const activeFilteredItems = showInactive
            ? items.filter((item) => !Boolean(item?.active))
            : items.filter((item) => Boolean(item?.active));

        if (!term) return activeFilteredItems;

        return activeFilteredItems.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term);
        });
    }, [items, search, showInactive]);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const selectItem = async (id) => {
        if (!id) return;

        setIsCreateMode(false);
        setIsLoadingDetails(true);
        setMessage(null);

        try {
            const response = await apiClient.get(`/termsofdelivery/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
            setTranslationTemplate(mapped.translations);
        } catch (error) {
            console.error('Failed to load delivery term details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta leveransvillkor.' });
        } finally {
            setIsLoadingDetails(false);
        }
    };

    const applyCreateNew = () => {
        setMessage(null);
        setIsCreateMode(true);

        const nextForm = {
            ...defaultForm,
            translations: createEmptyTranslations(translationTemplate),
        };

        setForm(nextForm);
        setOriginalForm(nextForm);
    };

    const handleCreateNew = () => {
        if (hasUnsavedChanges) {
            setMessage({ type: 'error', text: 'Spara eller radera innan du skapar ett nytt leveransvillkor.' });
            return;
        }

        applyCreateNew();
    };

    const updateTranslation = (langCode, value) => {
        setForm((prev) => {
            const current = Array.isArray(prev.translations) ? prev.translations : [];
            const next = current.some((item) => item.langCode === langCode)
                ? current.map((item) => (item.langCode === langCode ? { ...item, translation: value ?? '' } : item))
                : [...current, { langCode, translation: value ?? '' }];

            return {
                ...prev,
                translations: next,
            };
        });
    };

    const handleSave = async () => {
        if (!form.name.trim()) {
            setMessage({ type: 'error', text: 'Namn maste anges.' });
            return;
        }

        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = form.id
                ? await apiClient.put(`/termsofdelivery/${form.id}`, payload)
                : await apiClient.post('/termsofdelivery', payload);

            const saved = mapDtoToForm(response.data);
            setForm(saved);
            setOriginalForm(saved);
            setTranslationTemplate(saved.translations);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: 'Leveransvillkor sparades.' });
        } catch (error) {
            console.error('Failed to save delivery term:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara leveransvillkor.' });
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
            await apiClient.delete(`/termsofdelivery/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            setTranslationTemplate([]);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Leveransvillkor raderades.' });
        } catch (error) {
            console.error('Failed to delete delivery term:', error);
            setMessage({ type: 'error', text: buildDeleteConflictMessage(error) });
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteConfirm}
                title="RADERA LEVERANSVILLKOR"
                message={`Ar du saker pa att du vill radera leveransvillkor ${form.name || form.id}? Atgarden kan inte angras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Leveransvillkor</h2>

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
                        <div className="w-30 mr-2">
                            <SwitchSelector
                                name="deliveryTermsVisibility"
                                options={visibilityOptions}
                                forcedSelectedIndex={showInactive ? 1 : 0}
                                onChange={(value) => setShowInactive(Boolean(value))}
                                backgroundColor="#353b48"
                                fontColor="#374151"
                            />
                        </div>
                        <button
                            type="button"
                            onClick={handleCreateNew}
                            disabled={isLoadingList || isSaving || isDeleting}
                            className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            Ny
                        </button>
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga leveransvillkor</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;

                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => selectItem(item.id)}
                                        className={`w-full text-left text-xs px-2 py-1 rounded-sm grid grid-cols-[1fr_auto] gap-2 items-center ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="truncate">{item.name}</span>
                                        {item.isDefault ? (
                                            <span className="inline-flex items-center rounded-full border border-amber-300 bg-amber-100 px-2 py-[1px] text-[10px] font-medium uppercase tracking-wide text-amber-800">
                                                Standard
                                            </span>
                                        ) : null}
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
                            disabled={isEditDisabled || isSaving || isDeleting}
                            className="w-24 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isSaving ? 'Sparar...' : 'Spara'}
                        </button>
                        <button
                            type="button"
                            onClick={handleDeleteClick}
                            disabled={isEditDisabled || isSaving || isDeleting || !form.id}
                            className="w-24 shadow-md/30 text-xs text-white bg-red-600 hover:bg-red-800 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isDeleting ? 'Raderar...' : 'Radera'}
                        </button>
                    </div>

                    <div className={`w-80 min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <LabeledInput
                            label="Namn"
                            labelWidth="w-20"
                            margintop="0"
                            value={form.name}
                            onChange={(value) => updateField('name', value ?? '')}
                            disabled={isEditDisabled}
                            maxLength={255}
                            showCharCounter
                        />

                        <LabeledSwitch
                            id="delivery-term-default"
                            name="delivery-term-default"
                            label="Standard"
                            labelWidth="w-20"
                            marginTop={12}
                            value={form.isDefault}
                            disabled={isEditDisabled}
                            onChange={(_, __, checked) => updateField('isDefault', checked)}
                        />

                        <LabeledSwitch
                            id="delivery-term-active"
                            name="delivery-term-active"
                            label="Aktiv"
                            labelWidth="w-20"
                            marginTop={4}
                            value={form.active}
                            disabled={isEditDisabled}
                            onChange={(_, __, checked) => updateField('active', checked)}
                        />
                    </div>

                    <div className={`min-w-0 mt-15 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <h3 className="text-sm text-gray-700 mb-2">Oversattningar</h3>
                        <div className="border-t border-gray-300 pt-2 space-y-1">
                            {(form.translations ?? []).length === 0 ? (
                                <p className="text-xs text-gray-500">Inga sprak tillgangliga.</p>
                            ) : (
                                (form.translations ?? []).map((item) => (
                                    <LabeledInput
                                        key={item.langCode}
                                        label={formatLangLabel(item.langCode)}
                                        labelWidth="w-12"
                                        margintop="0"
                                        value={item.translation ?? ''}
                                        onChange={(value) => updateTranslation(item.langCode, value ?? '')}
                                        disabled={isEditDisabled}
                                        maxLength={255}
                                        showCharCounter
                                    />
                                ))
                            )}
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

export default DeliveryTermsSettings;
