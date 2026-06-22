import { useCallback, useEffect, useMemo, useState } from 'react';
import SwitchSelector from 'react-switch-selector';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSwitch from '../../components/LabeledSwitch';
import LabeledSelect from '../../components/LabeledSelect';

const defaultForm = {
    id: null,
    name: '',
    translationCode: null,
    translations: [],
    active: true,
    materialGroup: null,
    thicknessMm: null,
    weightGr: null,
    materialText: '',
    packagingFeeCategoryId: null,
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

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const parseNullableNumber = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = Number.parseFloat(value);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: dto?.name ?? '',
    translationCode: dto?.translationCode ?? null,
    translations: mapTranslations(dto?.translations),
    active: Boolean(dto?.active),
    materialGroup: parseNullableInt(dto?.materialGroup),
    thicknessMm: parseNullableNumber(dto?.thicknessMm),
    weightGr: parseNullableInt(dto?.weightGr),
    materialText: dto?.materialText ?? '',
    packagingFeeCategoryId: parseNullableInt(dto?.packagingFeeCategoryId),
});

const mapFormToPayload = (form) => ({
    name: form.name.trim(),
    translationCode: parseNullableInt(form.translationCode),
    translations: (form.translations ?? []).map((item) => ({
        langCode: item.langCode,
        translation: String(item.translation ?? ''),
    })),
    active: Boolean(form.active),
    materialGroup: parseNullableInt(form.materialGroup),
    thicknessMm: parseNullableNumber(form.thicknessMm),
    weightGr: parseNullableInt(form.weightGr),
    materialText: String(form.materialText ?? ''),
    packagingFeeCategoryId: parseNullableInt(form.packagingFeeCategoryId),
});

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? ''),
    translationCode: parseNullableInt(form?.translationCode),
    active: Boolean(form?.active),
    materialGroup: parseNullableInt(form?.materialGroup),
    thicknessMm: parseNullableNumber(form?.thicknessMm),
    weightGr: parseNullableInt(form?.weightGr),
    materialText: String(form?.materialText ?? ''),
    packagingFeeCategoryId: parseNullableInt(form?.packagingFeeCategoryId),
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

    return 'Kunde inte radera material.';
};

const materialGroupOptions = [
    { value: 1, label: 'Well' },
    { value: 2, label: 'Papper, kartong' },
    { value: 3, label: 'Plast' },
    { value: 4, label: 'Övrigt' },
];

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

const MaterialsSettings = () => {
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
    const [packagingFeeCategories, setPackagingFeeCategories] = useState([]);

    const isEditDisabled = isLoadingDetails || (!form.id && !isCreateMode);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.post('/materials/search', {
                pagination: { pageNumber: 1, pageSize: 500 },
                orderBy: [{ field: 'id', direction: 'asc' }],
            });
            setItems(response?.data?.items ?? []);
        } catch (error) {
            console.error('Failed to load materials list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta material.' });
        } finally {
            setIsLoadingList(false);
        }
    };

    useEffect(() => {
        loadList();
    }, []);

    useEffect(() => {
        apiClient.get('/packagingfeecategories/search')
            .then((res) => {
                const items = res?.data ?? [];
                setPackagingFeeCategories([
                    { id: '', name: '' },
                    ...items.map((c) => ({
                        id: c.id,
                        name: [c.categoryCode, c.description].filter(Boolean).join(' '),
                    })),
                ]);
            })
            .catch((err) => console.error('Failed to load packaging fee categories:', err));
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
            const response = await apiClient.get(`/materials/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
            setTranslationTemplate(mapped.translations);
        } catch (error) {
            console.error('Failed to load material details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta material.' });
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
                ? await apiClient.put(`/materials/${form.id}`, payload)
                : await apiClient.post('/materials', payload);

            const saved = response.data;
            const mappedSaved = mapDtoToForm(saved);
            setForm(mappedSaved);
            setOriginalForm(mappedSaved);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: 'Material sparades.' });
        } catch (error) {
            console.error('Failed to save material:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara material.' });
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
            await apiClient.delete(`/materials/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Material raderades.' });
        } catch (error) {
            console.error('Failed to delete material:', error);
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
                title="RADERA MATERIAL"
                message={`Ar du saker pa att du vill radera material ${form.name || form.id}? Atgarden kan inte angras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="OSPARADE ANDRINGAR"
                message="Det finns ej sparade andringar, vill du anda fortsatta?"
                confirmText="Fortsatt anda"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Material</h2>

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
                                name="materialsVisibility"
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

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga material</p>
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

                    <div className="w-150">
                        <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                            <LabeledInput
                                label="Namn"
                                labelWidth="w-20"
                                margintop="0"
                                value={form.name}
                                onChange={(value) => updateField('name', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={200}
                                showCharCounter
                            />

                            <div className="flex items-start mt-2">
                                <span className="w-20 flex-none text-xs text-gray-700 pt-1">Materialgrupp</span>
                                <div className="space-y-1.5">
                                    {materialGroupOptions.map((opt) => (
                                        <label key={opt.value} className="flex items-center gap-2 text-xs text-gray-800">
                                            <input
                                                type="radio"
                                                name="materialGroup"
                                                value={opt.value}
                                                checked={form.materialGroup === opt.value}
                                                onChange={() => updateField('materialGroup', opt.value)}
                                                disabled={isEditDisabled}
                                                className="h-4 w-4"
                                            />
                                            <span>{opt.label}</span>
                                        </label>
                                    ))}
                                </div>
                            </div>

                            <LabeledInput
                                label="Tjocklek"
                                labelWidth="w-20"
                                inputWidth="w-20"
                                margintop="5"
                                type="number"
                                value={form.thicknessMm}
                                onChange={(value) => updateField('thicknessMm', value)}
                                disabled={isEditDisabled}
                            />

                            <LabeledInput
                                label="Vikt"
                                labelWidth="w-20"
                                inputWidth="w-20"
                                type="number"
                                integerOnly={true}
                                value={form.weightGr}
                                onChange={(value) => updateField('weightGr', value)}
                                disabled={isEditDisabled}
                            />

                            <LabeledInput
                                label="Materialtext"
                                labelWidth="w-20"
                                value={form.materialText}
                                onChange={(value) => updateField('materialText', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={200}
                                showCharCounter
                            />

                            <LabeledSelect
                                label="Förp.avg.kat."
                                labelWidth="w-20"
                                name="packagingFeeCategoryId"
                                value={form.packagingFeeCategoryId ?? ''}
                                items={packagingFeeCategories}
                                onChange={(value) => updateField('packagingFeeCategoryId', value === '' ? null : Number(value))}
                                disabled={isEditDisabled || packagingFeeCategories.length <= 1}
                            />

                            <LabeledSwitch
                                id="material-active"
                                name="material-active"
                                label="Aktiv"
                                labelWidth="w-20"
                                marginTop={10}
                                value={form.active}
                                disabled={isEditDisabled}
                                onChange={(_, __, checked) => updateField('active', checked)}
                            />
                        </div>
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

export default MaterialsSettings;
