import { useCallback, useEffect, useMemo, useState } from 'react';
import SwitchSelector from 'react-switch-selector';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSwitch from '../../components/LabeledSwitch';

const defaultForm = {
    id: null,
    name: '',
    translationCode: null,
    translations: [],
    oldDbId: null,
    active: true,
    isPackaging: false,
};

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

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: dto?.name ?? '',
    translationCode: dto?.translationCode ?? null,
    translations: mapTranslations(dto?.translations),
    oldDbId: dto?.oldDbId ?? null,
    active: Boolean(dto?.active),
    isPackaging: Boolean(dto?.isPackaging),
});

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapFormToPayload = (form) => ({
    name: form.name.trim(),
    translationCode: parseNullableInt(form.translationCode),
    translations: (form.translations ?? []).map((item) => ({
        langCode: item.langCode,
        translation: String(item.translation ?? ''),
    })),
    oldDbId: parseNullableInt(form.oldDbId),
    active: form.active,
    isPackaging: form.isPackaging,
});

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? ''),
    translationCode: parseNullableInt(form?.translationCode),
    oldDbId: parseNullableInt(form?.oldDbId),
    active: Boolean(form?.active),
    isPackaging: Boolean(form?.isPackaging),
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

    return 'Kunde inte radera konstruktion.';
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

const ConstructionsSettings = () => {
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
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

    const isEditDisabled = isLoadingDetails || (!form.id && !isCreateMode);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.post('/constructions/search', {
                pagination: { pageNumber: 1, pageSize: 500 },
                orderBy: [{ field: 'id', direction: 'asc' }],
            });
            setItems(response?.data?.items ?? []);
        } catch (error) {
            console.error('Failed to load constructions list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta konstruktioner.' });
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
            const response = await apiClient.get(`/constructions/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
            setTranslationTemplate(mapped.translations);
        } catch (error) {
            console.error('Failed to load construction details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta konstruktion.' });
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
                ? await apiClient.put(`/constructions/${form.id}`, payload)
                : await apiClient.post('/constructions', payload);

            const saved = response.data;
            const mappedSaved = mapDtoToForm(saved);
            setForm(mappedSaved);
            setOriginalForm(mappedSaved);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: 'Konstruktion sparades.' });
        } catch (error) {
            console.error('Failed to save construction:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara konstruktion.' });
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
            await apiClient.delete(`/constructions/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Konstruktion raderades.' });
        } catch (error) {
            console.error('Failed to delete construction:', error);
            setMessage({ type: 'error', text: buildDeleteConflictMessage(error) });
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

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteConfirm}
                title="RADERA KONSTRUKTION"
                message={`Ar du säker på att du vill radera konstruktion ${form.name || form.id}? Åtgarden kan inte ångras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparande ändringar"
                message="Det finns ej sparande ändringar, vill du anda fortsatta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Konstruktioner</h2>

            <div className="flex min-w-0 items-stretch">
                <div className="w-[450px] shrink-0 mb-5 px-4 py-2 border-r border-gray-300">
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
                        <div className="w-30 mr-2">
                            <SwitchSelector
                                name="constructionsVisibility"
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
                            className="shadow-md/30 text-xs text-gray-900 bg-blue-200 hover:bg-blue-300 px-6 p-[5px] rounded-sm"
                        >
                            Skapa ny
                        </button>
                    </div>

                    <div className="mt-5 mb-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga konstruktioner</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;
                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelect(item.id)}
                                        className={`w-full text-left text-xs px-2 py-0.5 rounded-sm ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="truncate">{item.name}</span>
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

                        <button
                            type="button"
                            onClick={handleDeleteClick}
                            disabled={!form.id || isDeleting || isSaving || isEditDisabled}
                            className="w-20 shadow-md/30 text-xs text-white bg-red-700 hover:bg-red-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isDeleting ? 'Raderar...' : 'Radera'}
                        </button>
                    </div>

                    <div className="grid grid-cols-1 xl:grid-cols-2 gap-8 items-start">
                        <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                            <LabeledInput
                                label="Namn"
                                labelWidth="w-20"
                                margintop="0"
                                value={form.name}
                                onChange={(value) => updateField('name', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={50}
                                showCharCounter
                            />

                            <LabeledSwitch
                                id="construction-active"
                                name="construction-active"
                                label="Aktiv"
                                labelWidth="w-20"
                                marginTop={16}
                                value={form.active}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('active', checked)}
                            />

                            <LabeledSwitch
                                id="construction-is-packaging"
                                name="construction-is-packaging"
                                label="Emballage"
                                labelWidth="w-20"
                                marginTop={4}
                                value={form.isPackaging}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('isPackaging', checked)}
                            />
                        </div>

                    </div>

                    <div className={`min-w-0 mt-15 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <h3 className="text-sm text-gray-700 mb-2">Översättningar</h3>
                        <div className="border-t border-gray-300 pt-2 space-y-1">
                            {(form.translations ?? []).length === 0 ? (
                                <p className="text-xs text-gray-500">Inga sprak tillgangliga.</p>
                            ) : (
                                (form.translations ?? []).map((item) => (
                                    <LabeledInput
                                        key={item.langCode}
                                        label={formatLangLabel(item.langCode)}
                                        labelWidth="w-10"
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
                            <li className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                                {message.text}
                            </li>
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
};

export default ConstructionsSettings;