import { useCallback, useEffect, useMemo, useState } from 'react';
import SwitchSelector from 'react-switch-selector';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSelect from '../../components/LabeledSelect';
import LabeledSwitch from '../../components/LabeledSwitch';

const defaultTypeForm = {
    id: null,
    name: '',
    isEur: false,
    isPallet: false,
    isActive: true,
    sortNr: null,
};

const defaultFormatForm = {
    id: null,
    name: '',
    translationCode: null,
    translations: [],
    m2: null,
    active: true,
    width: null,
    height: null,
    palletTypeId: null,
    debitFactor: null,
    sortNr: null,
    copyTo: false,
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

const mapTypeDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: dto?.name ?? '',
    isEur: Boolean(dto?.isEur),
    isPallet: Boolean(dto?.isPallet),
    isActive: Boolean(dto?.isActive),
    sortNr: parseNullableInt(dto?.sortNr),
});

const mapTypeFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    isEur: Boolean(form.isEur),
    isPallet: Boolean(form.isPallet),
    isActive: Boolean(form.isActive),
    sortNr: parseNullableInt(form.sortNr),
});

const normalizeTypeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? ''),
    isEur: Boolean(form?.isEur),
    isPallet: Boolean(form?.isPallet),
    isActive: Boolean(form?.isActive),
    sortNr: parseNullableInt(form?.sortNr),
});

const mapFormatDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: dto?.name ?? '',
    translationCode: dto?.translationCode ?? null,
    translations: mapTranslations(dto?.translations),
    m2: parseNullableNumber(dto?.m2),
    active: Boolean(dto?.active),
    width: parseNullableInt(dto?.width),
    height: parseNullableInt(dto?.height),
    palletTypeId: parseNullableInt(dto?.palletTypeId),
    debitFactor: parseNullableNumber(dto?.debitFactor),
    sortNr: parseNullableInt(dto?.sortNr),
    copyTo: Boolean(dto?.copyTo),
});

const mapFormatFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    translationCode: parseNullableInt(form.translationCode),
    translations: (form.translations ?? []).map((item) => ({
        langCode: item.langCode,
        translation: String(item.translation ?? ''),
    })),
    m2: parseNullableNumber(form.m2),
    active: Boolean(form.active),
    width: parseNullableInt(form.width),
    height: parseNullableInt(form.height),
    palletTypeId: parseNullableInt(form.palletTypeId),
    debitFactor: parseNullableNumber(form.debitFactor),
    sortNr: parseNullableInt(form.sortNr),
    copyTo: Boolean(form.copyTo),
});

const normalizeFormatFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? ''),
    translationCode: parseNullableInt(form?.translationCode),
    m2: parseNullableNumber(form?.m2),
    active: Boolean(form?.active),
    width: parseNullableInt(form?.width),
    height: parseNullableInt(form?.height),
    palletTypeId: parseNullableInt(form?.palletTypeId),
    debitFactor: parseNullableNumber(form?.debitFactor),
    sortNr: parseNullableInt(form?.sortNr),
    copyTo: Boolean(form?.copyTo),
    translations: (form?.translations ?? [])
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item.langCode).trim(),
            translation: String(item.translation ?? ''),
        })),
});

const buildDeleteConflictMessage = (error, fallbackMessage) => {
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

    return fallbackMessage;
};

const TypeSection = ({ palletTypes, isLoadingList, onRefresh, selectedTypeId, onSelectType, notify }) => {
    const [search, setSearch] = useState('');
    const [showInactive, setShowInactive] = useState(false);
    const [form, setForm] = useState(defaultTypeForm);
    const [isCreateMode, setIsCreateMode] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [originalForm, setOriginalForm] = useState(defaultTypeForm);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

    const isEditDisabled = !form.id && !isCreateMode;

    const hasUnsavedChanges = useCallback(() => {
        return JSON.stringify(normalizeTypeFormForCompare(form)) !== JSON.stringify(normalizeTypeFormForCompare(originalForm));
    }, [form, originalForm]);

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
            setUnsavedAction({ type: 'navigate' });
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        const activeFilteredItems = showInactive
            ? palletTypes.filter((item) => !Boolean(item?.isActive))
            : palletTypes.filter((item) => Boolean(item?.isActive));

        if (!term) return activeFilteredItems;

        return activeFilteredItems.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term);
        });
    }, [palletTypes, search, showInactive]);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const applySelection = (item) => {
        const mapped = mapTypeDtoToForm(item);
        setForm(mapped);
        setOriginalForm(mapped);
        setIsCreateMode(false);
        onSelectType(item.id);
    };

    const handleSelect = (item) => {
        if (!item) return;
        if (form.id === item.id && !isCreateMode) return;

        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'select', item });
            setShowUnsavedWarning(true);
            return;
        }

        applySelection(item);
    };

    const handleCreateNew = () => {
        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'create' });
            setShowUnsavedWarning(true);
            return;
        }

        const nextForm = defaultTypeForm;
        setForm(nextForm);
        setOriginalForm(nextForm);
        setIsCreateMode(true);
        notify(null);
    };

    const handleSave = async () => {
        if (!String(form.name ?? '').trim()) {
            notify({ type: 'error', text: 'Namn maste anges.' });
            return;
        }

        setIsSaving(true);
        notify(null);

        try {
            const payload = mapTypeFormToPayload(form);
            const response = form.id
                ? await apiClient.put(`/pallettypes/${form.id}`, payload)
                : await apiClient.post('/pallettypes', payload);

            const saved = mapTypeDtoToForm(response.data);
            setForm(saved);
            setOriginalForm(saved);
            setIsCreateMode(false);
            await onRefresh();
            onSelectType(saved.id);
            notify({ type: 'success', text: 'Palltyp sparades.' });
        } catch (error) {
            console.error('Failed to save pallet type:', error);
            notify({ type: 'error', text: 'Kunde inte spara palltyp.' });
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
        notify(null);

        try {
            await apiClient.delete(`/pallettypes/${form.id}`);
            await onRefresh();
            setForm(defaultTypeForm);
            setOriginalForm(defaultTypeForm);
            setIsCreateMode(false);
            onSelectType(null);
            notify({ type: 'success', text: 'Palltyp raderades.' });
        } catch (error) {
            console.error('Failed to delete pallet type:', error);
            notify({ type: 'error', text: buildDeleteConflictMessage(error, 'Kunde inte radera palltyp.') });
        } finally {
            setIsDeleting(false);
        }
    };

    const handleUnsavedWarningConfirm = () => {
        const action = unsavedAction;
        setShowUnsavedWarning(false);
        setUnsavedAction(null);

        if (action?.type === 'select' && action.item) {
            applySelection(action.item);
            return;
        }

        if (action?.type === 'create') {
            const nextForm = defaultTypeForm;
            setForm(nextForm);
            setOriginalForm(nextForm);
            setIsCreateMode(true);
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
        <section className="min-w-0">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteConfirm}
                title="RADERA PALLTYP"
                message={`Ar du säker på att du vill radera palltyp ${form.name || form.id}? Åtgarden kan inte ångras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

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

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Palltyper</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[450px] shrink-0 px-4 py-2 border-r border-gray-300">
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
                                name="palletTypesVisibility"
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

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(50vh-210px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga palltyper</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;

                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelect(item)}
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

                    <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <div className="grid grid-cols-1 xl:grid-cols-2 gap-4 items-start">
                            <div>
                                <LabeledInput
                                    label="Namn"
                                    labelWidth="w-24"
                                    margintop="0"
                                    value={form.name}
                                    onChange={(value) => setForm((prev) => ({ ...prev, name: value ?? '' }))}
                                    disabled={isEditDisabled}
                                    maxLength={100}
                                    showCharCounter
                                />

                                <LabeledSwitch
                                    id="pallet-type-is-eur"
                                    name="pallet-type-is-eur"
                                    label="Eur pall"
                                    labelWidth="w-24"
                                    marginTop={12}
                                    value={form.isEur}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isEur: checked }))}
                                />

                                <LabeledSwitch
                                    id="pallet-type-is-pallet"
                                    name="pallet-type-is-pallet"
                                    label="Är pall"
                                    labelWidth="w-24"
                                    marginTop={4}
                                    value={form.isPallet}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isPallet: checked }))}
                                />

                                <LabeledSwitch
                                    id="pallet-type-active"
                                    name="pallet-type-active"
                                    label="Aktiv"
                                    labelWidth="w-24"
                                    marginTop={4}
                                    value={form.isActive}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isActive: checked }))}
                                />

                                <LabeledInput
                                    label="Sortering"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.sortNr}
                                    onChange={(value) => setForm((prev) => ({ ...prev, sortNr: value }))}
                                    disabled={isEditDisabled}
                                />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
};

const FormatSection = ({ palletFormats, isLoadingList, onRefresh, selectedTypeId, palletTypes, notify }) => {
    const [search, setSearch] = useState('');
    const [showInactive, setShowInactive] = useState(false);
    const [form, setForm] = useState(defaultFormatForm);
    const [isCreateMode, setIsCreateMode] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [isLoadingDetails, setIsLoadingDetails] = useState(false);
    const [translationTemplate, setTranslationTemplate] = useState([]);
    const [originalForm, setOriginalForm] = useState(defaultFormatForm);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

    const isEditDisabled = isLoadingDetails || (!form.id && !isCreateMode);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormatFormForCompare(form)) !== JSON.stringify(normalizeFormatFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

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
            setUnsavedAction({ type: 'navigate' });
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        const typeFilteredItems = selectedTypeId == null
            ? []
            : palletFormats.filter((item) => parseNullableInt(item?.palletTypeId) === parseNullableInt(selectedTypeId));
        const activeFilteredItems = showInactive
            ? typeFilteredItems.filter((item) => !Boolean(item?.active))
            : typeFilteredItems.filter((item) => Boolean(item?.active));

        if (!term) return activeFilteredItems;

        return activeFilteredItems.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term);
        });
    }, [palletFormats, search, showInactive, selectedTypeId]);

    const selectedType = palletTypes.find((item) => parseNullableInt(item?.id) === parseNullableInt(selectedTypeId));

    useEffect(() => {
        if (!selectedTypeId) return;
        if (hasUnsavedChanges()) return;

        if (isCreateMode) {
            setForm((prev) => ({ ...prev, palletTypeId: selectedTypeId }));
            return;
        }

        if (form.palletTypeId !== selectedTypeId) {
            if (filteredItems.length > 0) {
                void selectItem(filteredItems[0].id);
            } else {
                const nextForm = { ...defaultFormatForm, palletTypeId: selectedTypeId };
                setForm(nextForm);
                setOriginalForm(nextForm);
                setTranslationTemplate([]);
            }
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [selectedTypeId]);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const selectItem = async (id) => {
        if (!id) return;

        setIsCreateMode(false);
        setIsLoadingDetails(true);
        notify(null);

        try {
            const response = await apiClient.get(`/palletformats/${id}`);
            const mapped = mapFormatDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
            setTranslationTemplate(mapped.translations);
        } catch (error) {
            console.error('Failed to load pallet format details:', error);
            notify({ type: 'error', text: 'Kunde inte hamta pallformat.' });
        } finally {
            setIsLoadingDetails(false);
        }
    };

    const handleSelect = (id) => {
        if (form.id === id && !isCreateMode) return;

        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'select', id });
            setShowUnsavedWarning(true);
            return;
        }

        void selectItem(id);
    };

    const handleCreateNew = () => {
        if (selectedTypeId == null) {
            notify({ type: 'error', text: 'Välj en palltyp först.' });
            return;
        }

        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'create' });
            setShowUnsavedWarning(true);
            return;
        }

        const nextForm = {
            ...defaultFormatForm,
            palletTypeId: selectedTypeId,
            translations: createEmptyTranslations(translationTemplate),
        };
        setForm(nextForm);
        setOriginalForm(nextForm);
        setIsCreateMode(true);
        notify(null);
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
        if (!String(form.name ?? '').trim()) {
            notify({ type: 'error', text: 'Namn maste anges.' });
            return;
        }

        if (parseNullableInt(form.palletTypeId) == null) {
            notify({ type: 'error', text: 'Palltyp maste anges.' });
            return;
        }

        setIsSaving(true);
        notify(null);

        try {
            const payload = mapFormatFormToPayload(form);
            const response = form.id
                ? await apiClient.put(`/palletformats/${form.id}`, payload)
                : await apiClient.post('/palletformats', payload);

            const saved = mapFormatDtoToForm(response.data);
            setForm(saved);
            setOriginalForm(saved);
            setIsCreateMode(false);
            setTranslationTemplate(saved.translations);
            await onRefresh();
            notify({ type: 'success', text: 'Pallformat sparades.' });
        } catch (error) {
            console.error('Failed to save pallet format:', error);
            notify({ type: 'error', text: 'Kunde inte spara pallformat.' });
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
        notify(null);

        try {
            await apiClient.delete(`/palletformats/${form.id}`);
            await onRefresh();
            const nextForm = { ...defaultFormatForm, palletTypeId: selectedTypeId ?? null };
            setForm(nextForm);
            setOriginalForm(nextForm);
            setIsCreateMode(false);
            setTranslationTemplate([]);
            notify({ type: 'success', text: 'Pallformat raderades.' });
        } catch (error) {
            console.error('Failed to delete pallet format:', error);
            notify({ type: 'error', text: buildDeleteConflictMessage(error, 'Kunde inte radera pallformat.') });
        } finally {
            setIsDeleting(false);
        }
    };

    const handleUnsavedWarningConfirm = () => {
        const action = unsavedAction;
        setShowUnsavedWarning(false);
        setUnsavedAction(null);

        if (action?.type === 'select' && action.id) {
            void selectItem(action.id);
            return;
        }

        if (action?.type === 'create') {
            const nextForm = {
                ...defaultFormatForm,
                palletTypeId: selectedTypeId,
                translations: createEmptyTranslations(translationTemplate),
            };
            setForm(nextForm);
            setOriginalForm(nextForm);
            setIsCreateMode(true);
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
        <section className="min-w-0 mt-8">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteConfirm}
                title="RADERA PALLFORMAT"
                message={`Ar du säker på att du vill radera pallformat ${form.name || form.id}? Åtgarden kan inte ångras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

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

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">
                Pallformat{selectedType?.name ? ` - ${selectedType.name}` : ''}
            </h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[450px] shrink-0 px-4 py-2 border-r border-gray-300">
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
                                name="palletFormatsVisibility"
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
                            disabled={selectedTypeId == null}
                        >
                            Skapa ny
                        </button>
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(50vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : selectedTypeId == null ? (
                            <p className="text-xs text-gray-500 py-2">Välj en palltyp ovan.</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga pallformat</p>
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
                            disabled={isSaving || isEditDisabled || selectedTypeId == null}
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

                    <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <div className="grid grid-cols-1 xl:grid-cols-2 gap-4 items-start">
                            <div>
                                <LabeledInput
                                    label="Namn"
                                    labelWidth="w-24"
                                    margintop="0"
                                    value={form.name}
                                    onChange={(value) => setForm((prev) => ({ ...prev, name: value ?? '' }))}
                                    disabled={isEditDisabled}
                                    maxLength={50}
                                    showCharCounter
                                />

                                <LabeledInput
                                    label="M2"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    value={form.m2}
                                    onChange={(value) => setForm((prev) => ({ ...prev, m2: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Bredd"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.width}
                                    onChange={(value) => setForm((prev) => ({ ...prev, width: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Höjd"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.height}
                                    onChange={(value) => setForm((prev) => ({ ...prev, height: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Debiteringsfaktor"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    value={form.debitFactor}
                                    onChange={(value) => setForm((prev) => ({ ...prev, debitFactor: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Sorteringsnr"
                                    labelWidth="w-24"
                                    inputWidth="w-20"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.sortNr}
                                    onChange={(value) => setForm((prev) => ({ ...prev, sortNr: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledSelect
                                    label="Palltyp"
                                    labelWidth="w-24"
                                    inputWidth="w-56"
                                    margintop="0"
                                    name="palletTypeId"
                                    value={form.palletTypeId ?? ''}
                                    items={palletTypes}
                                    onChange={(value) => setForm((prev) => ({ ...prev, palletTypeId: value === '' ? null : Number(value) }))}
                                    disabled={isEditDisabled || palletTypes.length === 0}
                                />

                                <LabeledSwitch
                                    id="pallet-format-copy-to"
                                    name="pallet-format-copy-to"
                                    label="Kopiera"
                                    labelWidth="w-24"
                                    marginTop={12}
                                    value={form.copyTo}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, copyTo: checked }))}
                                />

                                <LabeledSwitch
                                    id="pallet-format-active"
                                    name="pallet-format-active"
                                    label="Aktiv"
                                    labelWidth="w-24"
                                    marginTop={4}
                                    value={form.active}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, active: checked }))}
                                />
                            </div>
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
            </div>
        </section>
    );
};

const PalletFormatsSettings = () => {
    const [palletTypes, setPalletTypes] = useState([]);
    const [palletFormats, setPalletFormats] = useState([]);
    const [selectedTypeId, setSelectedTypeId] = useState(null);
    const [message, setMessage] = useState(null);
    const [isLoadingTypes, setIsLoadingTypes] = useState(true);
    const [isLoadingFormats, setIsLoadingFormats] = useState(true);

    const loadTypes = useCallback(async () => {
        setIsLoadingTypes(true);
        try {
            const response = await apiClient.get('/pallettypes/search');
            const items = Array.isArray(response?.data) ? response.data : [];
            setPalletTypes(items);
            setSelectedTypeId((current) => {
                if (current != null && items.some((item) => parseNullableInt(item?.id) === parseNullableInt(current))) {
                    return current;
                }

                return null;
            });
        } catch (error) {
            console.error('Failed to load pallet types list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hämta palltyper.' });
        } finally {
            setIsLoadingTypes(false);
        }
    }, []);

    const loadFormats = useCallback(async () => {
        setIsLoadingFormats(true);
        try {
            const response = await apiClient.get('/palletformats/search');
            setPalletFormats(Array.isArray(response?.data) ? response.data : []);
        } catch (error) {
            console.error('Failed to load pallet formats list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hämta pallformat.' });
        } finally {
            setIsLoadingFormats(false);
        }
    }, []);

    useEffect(() => {
        void loadTypes();
        void loadFormats();
    }, [loadTypes, loadFormats]);

    return (
        <div className="relative flex h-full min-w-0">
            <div className="flex-1 min-w-0 pr-2 overflow-y-auto">
                <TypeSection
                    palletTypes={palletTypes}
                    isLoadingList={isLoadingTypes}
                    onRefresh={loadTypes}
                    selectedTypeId={selectedTypeId}
                    onSelectType={setSelectedTypeId}
                    notify={setMessage}
                />

                {selectedTypeId != null ? (
                    <FormatSection
                        palletFormats={palletFormats}
                        isLoadingList={isLoadingFormats}
                        onRefresh={loadFormats}
                        selectedTypeId={selectedTypeId}
                        palletTypes={palletTypes}
                        notify={setMessage}
                    />
                ) : null}
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
    );
};

export default PalletFormatsSettings;
