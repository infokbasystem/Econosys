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
    isActive: true,
    isNrOf: false,
    isCalculation: false,
    isSupplier: false,
    accountDomestic: null,
    accountEU: null,
    accountExport: null,
    translationCodeSupplierOrderUnknown: 0,
    translationCodeSupplierOrderKnown: 0,
    translationCodeQuotationUnknown: 0,
    translationCodeQuotationKnown: 0,
    translationCodeCustomerOrderUnknown: 0,
    translationCodeCustomerOrderKnown: 0,
    translationCodeInvoiceRow: 0,
    isDebitDefault: false,
    doPrintSupplierOrderDefault: false,
    doPrintQuotationDefault: false,
    doPrintCustomerOrderDefault: false,
    isCustomerDefault: false,
    doPrintScrapToolsTextOnCustomerOrder: false,
    addAutomaicIfEconopackIsTransportReponsible: false,
    dmtPercent: null,
    dmtFixed: null,
    provisionPercent: null,
    costTypeText: '',
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

const parseIntOrDefault = (value, defaultValue = 0) => {
    const parsed = parseNullableInt(value);
    return parsed ?? defaultValue;
};

const defaultTranslationLangs = ['EN', 'SW'];

const translationSections = [
    {
        key: 'customerOrderUnknown',
        codeField: 'translationCodeCustomerOrderUnknown',
        title: 'Ordererkannande, kostnad okand',
    },
    {
        key: 'customerOrderKnown',
        codeField: 'translationCodeCustomerOrderKnown',
        title: 'Ordererkannande, kostnad kand',
    },
    {
        key: 'quotationUnknown',
        codeField: 'translationCodeQuotationUnknown',
        title: 'Affarsforslag, kostnad okand',
    },
    {
        key: 'quotationKnown',
        codeField: 'translationCodeQuotationKnown',
        title: 'Affarsforslag, kostnad kand',
    },
    {
        key: 'supplierOrderUnknown',
        codeField: 'translationCodeSupplierOrderUnknown',
        title: 'Bestallning, kostnad okand',
    },
    {
        key: 'supplierOrderKnown',
        codeField: 'translationCodeSupplierOrderKnown',
        title: 'Bestallning, kostnad kand',
    },
    {
        key: 'invoiceRow',
        codeField: 'translationCodeInvoiceRow',
        title: 'Fakturarad',
    },
];

const createEmptyTranslationGroups = () =>
    translationSections.reduce((acc, section) => {
        acc[section.key] = defaultTranslationLangs.map((langCode) => ({
            id: null,
            langCode,
            translation: '',
        }));
        return acc;
    }, {});

const normalizeLangCode = (langCode) => String(langCode ?? '').trim().toUpperCase();

const normalizeTranslationGroups = (groups) =>
    translationSections.reduce((acc, section) => {
        const rows = Array.isArray(groups?.[section.key]) ? groups[section.key] : [];
        acc[section.key] = rows
            .map((row) => ({
                langCode: normalizeLangCode(row?.langCode),
                translation: String(row?.translation ?? ''),
            }))
            .filter((row) => row.langCode !== '')
            .sort((a, b) => a.langCode.localeCompare(b.langCode));
        return acc;
    }, {});

const mapTranslationRows = (rows) => {
    const mapped = Array.isArray(rows)
        ? rows
            .map((row) => ({
                id: row?.id ?? null,
                langCode: normalizeLangCode(row?.langCode),
                translation: String(row?.translation ?? ''),
            }))
            .filter((row) => row.langCode !== '')
        : [];

    const byLang = new Map(mapped.map((row) => [row.langCode, row]));

    for (const langCode of defaultTranslationLangs) {
        if (!byLang.has(langCode)) {
            byLang.set(langCode, { id: null, langCode, translation: '' });
        }
    }

    return Array.from(byLang.values()).sort((a, b) => a.langCode.localeCompare(b.langCode));
};

const CostsSettings = () => {
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
    const [originalForm, setOriginalForm] = useState(defaultForm);
    const [translationGroups, setTranslationGroups] = useState(createEmptyTranslationGroups());
    const [originalTranslationGroups, setOriginalTranslationGroups] = useState(createEmptyTranslationGroups());
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

    const isEditDisabled = isLoadingDetails || (!form.id && !isCreateMode);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        const isFormChanged = JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
        const isTranslationsChanged =
            JSON.stringify(normalizeTranslationGroups(translationGroups)) !==
            JSON.stringify(normalizeTranslationGroups(originalTranslationGroups));

        return isFormChanged || isTranslationsChanged;
    }, [form, originalForm, translationGroups, originalTranslationGroups, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.post('/costs/search');
            setItems(Array.isArray(response?.data) ? response.data : []);
        } catch (error) {
            console.error('Failed to load costs list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta kostnader.' });
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
            ? items.filter((item) => !Boolean(item?.isActive))
            : items.filter((item) => Boolean(item?.isActive));

        if (!term) return activeFilteredItems;

        return activeFilteredItems.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term);
        });
    }, [items, search, showInactive]);

    const loadTranslationGroups = useCallback(async (nextForm) => {
        const codeToSection = new Map();

        for (const section of translationSections) {
            const code = parseIntOrDefault(nextForm?.[section.codeField], 0);
            if (code > 0) {
                codeToSection.set(code, section.key);
            }
        }

        if (codeToSection.size === 0) {
            const emptyGroups = createEmptyTranslationGroups();
            setTranslationGroups(emptyGroups);
            setOriginalTranslationGroups(emptyGroups);
            return;
        }

        const response = await apiClient.post('/translationitems/search', {
            filter: {
                conditions: [
                    {
                        field: 'translationCode',
                        operator: 'in',
                        values: Array.from(codeToSection.keys()),
                    },
                ],
            },
            pagination: {
                pageNumber: 1,
                pageSize: 1000,
            },
            orderBy: [
                { field: 'langCode', direction: 'asc' },
                { field: 'id', direction: 'asc' },
            ],
        });

        const items = Array.isArray(response?.data?.items) ? response.data.items : [];
        const groupedBySection = createEmptyTranslationGroups();

        for (const item of items) {
            const code = parseIntOrDefault(item?.translationCode, 0);
            const sectionKey = codeToSection.get(code);

            if (!sectionKey) continue;
            groupedBySection[sectionKey].push(item);
        }

        const mappedGroups = translationSections.reduce((acc, section) => {
            acc[section.key] = mapTranslationRows(groupedBySection[section.key]);
            return acc;
        }, {});

        setTranslationGroups(mappedGroups);
        setOriginalTranslationGroups(mappedGroups);
    }, []);

    const updateTranslation = (sectionKey, langCode, value) => {
        setTranslationGroups((prev) => {
            const existingRows = Array.isArray(prev?.[sectionKey]) ? prev[sectionKey] : [];
            const normalizedLangCode = normalizeLangCode(langCode);
            const hasLang = existingRows.some((row) => normalizeLangCode(row?.langCode) === normalizedLangCode);

            const nextRows = hasLang
                ? existingRows.map((row) =>
                    normalizeLangCode(row?.langCode) === normalizedLangCode
                        ? { ...row, translation: String(value ?? '') }
                        : row,
                )
                : [...existingRows, { id: null, langCode: normalizedLangCode, translation: String(value ?? '') }];

            return {
                ...prev,
                [sectionKey]: nextRows,
            };
        });
    };

    const saveTranslationGroups = useCallback(async (nextForm) => {
        const batchItems = [];

        for (const section of translationSections) {
            const translationCode = parseIntOrDefault(nextForm?.[section.codeField], 0);
            if (translationCode <= 0) continue;

            const rows = Array.isArray(translationGroups?.[section.key]) ? translationGroups[section.key] : [];

            for (const row of rows) {
                const langCode = normalizeLangCode(row?.langCode);
                if (!langCode) continue;

                const translation = String(row?.translation ?? '');

                const payload = {
                    id: row?.id ?? null,
                    translationCode,
                    langCode,
                    translation,
                    translationHtml: null,
                };
                batchItems.push(payload);
            }
        }

        if (batchItems.length === 0) return false;
        await apiClient.post('/translationitems/batch-upsert', { items: batchItems });
        return true;
    }, [translationGroups]);

    const selectItem = async (id) => {
        if (!id) return;

        setIsCreateMode(false);
        setIsLoadingDetails(true);
        setMessage(null);
        try {
            const response = await apiClient.get(`/costs/${id}`);
            const dto = response?.data;
            const mapped = {
                id: dto?.id ?? null,
                name: String(dto?.name ?? ''),
                isActive: Boolean(dto?.isActive),
                isNrOf: Boolean(dto?.isNrOf),
                isCalculation: Boolean(dto?.isCalculation),
                isSupplier: Boolean(dto?.isSupplier),
                accountDomestic: parseNullableInt(dto?.accountDomestic),
                accountEU: parseNullableInt(dto?.accountEU),
                accountExport: parseNullableInt(dto?.accountExport),
                translationCodeSupplierOrderUnknown: parseIntOrDefault(dto?.translationCodeSupplierOrderUnknown),
                translationCodeSupplierOrderKnown: parseIntOrDefault(dto?.translationCodeSupplierOrderKnown),
                translationCodeQuotationUnknown: parseIntOrDefault(dto?.translationCodeQuotationUnknown),
                translationCodeQuotationKnown: parseIntOrDefault(dto?.translationCodeQuotationKnown),
                translationCodeCustomerOrderUnknown: parseIntOrDefault(dto?.translationCodeCustomerOrderUnknown),
                translationCodeCustomerOrderKnown: parseIntOrDefault(dto?.translationCodeCustomerOrderKnown),
                translationCodeInvoiceRow: parseIntOrDefault(dto?.translationCodeInvoiceRow),
                isDebitDefault: Boolean(dto?.isDebitDefault),
                doPrintSupplierOrderDefault: Boolean(dto?.doPrintSupplierOrderDefault),
                doPrintQuotationDefault: Boolean(dto?.doPrintQuotationDefault),
                doPrintCustomerOrderDefault: Boolean(dto?.doPrintCustomerOrderDefault),
                isCustomerDefault: Boolean(dto?.isCustomerDefault),
                doPrintScrapToolsTextOnCustomerOrder: Boolean(dto?.doPrintScrapToolsTextOnCustomerOrder),
                addAutomaicIfEconopackIsTransportReponsible: Boolean(dto?.addAutomaicIfEconopackIsTransportReponsible),
                dmtPercent: parseNullableNumber(dto?.dmtPercent),
                dmtFixed: parseNullableNumber(dto?.dmtFixed),
                provisionPercent: parseNullableNumber(dto?.provisionPercent),
                costTypeText: String(dto?.costTypeText ?? ''),
            };
            setForm(mapped);
            setOriginalForm(mapped);
            await loadTranslationGroups(mapped);
        } catch (error) {
            console.error('Failed to load cost details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta kostnad.' });
        } finally {
            setIsLoadingDetails(false);
        }
    };

    const applyCreateNew = () => {
        setMessage(null);
        setIsCreateMode(true);
        setForm(defaultForm);
        setOriginalForm(defaultForm);
        const emptyGroups = createEmptyTranslationGroups();
        setTranslationGroups(emptyGroups);
        setOriginalTranslationGroups(emptyGroups);
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
        if (!form.name.trim()) {
            setMessage({ type: 'error', text: 'Namn maste anges.' });
            return;
        }

        setIsSaving(true);
        setMessage(null);

        try {
            const payload = {
                name: form.name.trim(),
                isActive: Boolean(form.isActive),
                isNrOf: Boolean(form.isNrOf),
                isCalculation: Boolean(form.isCalculation),
                isSupplier: Boolean(form.isSupplier),
                accountDomestic: parseNullableInt(form.accountDomestic),
                accountEU: parseNullableInt(form.accountEU),
                accountExport: parseNullableInt(form.accountExport),
                translationCodeSupplierOrderUnknown: parseIntOrDefault(form.translationCodeSupplierOrderUnknown),
                translationCodeSupplierOrderKnown: parseIntOrDefault(form.translationCodeSupplierOrderKnown),
                translationCodeQuotationUnknown: parseIntOrDefault(form.translationCodeQuotationUnknown),
                translationCodeQuotationKnown: parseIntOrDefault(form.translationCodeQuotationKnown),
                translationCodeCustomerOrderUnknown: parseIntOrDefault(form.translationCodeCustomerOrderUnknown),
                translationCodeCustomerOrderKnown: parseIntOrDefault(form.translationCodeCustomerOrderKnown),
                translationCodeInvoiceRow: parseIntOrDefault(form.translationCodeInvoiceRow),
                isDebitDefault: Boolean(form.isDebitDefault),
                doPrintSupplierOrderDefault: Boolean(form.doPrintSupplierOrderDefault),
                doPrintQuotationDefault: Boolean(form.doPrintQuotationDefault),
                doPrintCustomerOrderDefault: Boolean(form.doPrintCustomerOrderDefault),
                isCustomerDefault: Boolean(form.isCustomerDefault),
                doPrintScrapToolsTextOnCustomerOrder: Boolean(form.doPrintScrapToolsTextOnCustomerOrder),
                addAutomaicIfEconopackIsTransportReponsible: Boolean(form.addAutomaicIfEconopackIsTransportReponsible),
                dmtPercent: parseNullableNumber(form.dmtPercent),
                dmtFixed: parseNullableNumber(form.dmtFixed),
                provisionPercent: parseNullableNumber(form.provisionPercent),
                costTypeText: String(form.costTypeText ?? '').trim(),
            };

            const response = form.id
                ? await apiClient.put(`/costs/${form.id}`, payload)
                : await apiClient.post('/costs', payload);

            const dto = response?.data;
            const mapped = {
                id: dto?.id ?? null,
                name: String(dto?.name ?? ''),
                isActive: Boolean(dto?.isActive),
                isNrOf: Boolean(dto?.isNrOf),
                isCalculation: Boolean(dto?.isCalculation),
                isSupplier: Boolean(dto?.isSupplier),
                accountDomestic: parseNullableInt(dto?.accountDomestic),
                accountEU: parseNullableInt(dto?.accountEU),
                accountExport: parseNullableInt(dto?.accountExport),
                translationCodeSupplierOrderUnknown: parseIntOrDefault(dto?.translationCodeSupplierOrderUnknown),
                translationCodeSupplierOrderKnown: parseIntOrDefault(dto?.translationCodeSupplierOrderKnown),
                translationCodeQuotationUnknown: parseIntOrDefault(dto?.translationCodeQuotationUnknown),
                translationCodeQuotationKnown: parseIntOrDefault(dto?.translationCodeQuotationKnown),
                translationCodeCustomerOrderUnknown: parseIntOrDefault(dto?.translationCodeCustomerOrderUnknown),
                translationCodeCustomerOrderKnown: parseIntOrDefault(dto?.translationCodeCustomerOrderKnown),
                translationCodeInvoiceRow: parseIntOrDefault(dto?.translationCodeInvoiceRow),
                isDebitDefault: Boolean(dto?.isDebitDefault),
                doPrintSupplierOrderDefault: Boolean(dto?.doPrintSupplierOrderDefault),
                doPrintQuotationDefault: Boolean(dto?.doPrintQuotationDefault),
                doPrintCustomerOrderDefault: Boolean(dto?.doPrintCustomerOrderDefault),
                isCustomerDefault: Boolean(dto?.isCustomerDefault),
                doPrintScrapToolsTextOnCustomerOrder: Boolean(dto?.doPrintScrapToolsTextOnCustomerOrder),
                addAutomaicIfEconopackIsTransportReponsible: Boolean(dto?.addAutomaicIfEconopackIsTransportReponsible),
                dmtPercent: parseNullableNumber(dto?.dmtPercent),
                dmtFixed: parseNullableNumber(dto?.dmtFixed),
                provisionPercent: parseNullableNumber(dto?.provisionPercent),
                costTypeText: String(dto?.costTypeText ?? ''),
            };

            const didSaveTranslations = await saveTranslationGroups(mapped);
            if (didSaveTranslations) {
                await loadTranslationGroups(mapped);
            }

            setForm(mapped);
            setOriginalForm(mapped);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: 'Kostnad sparades.' });
        } catch (error) {
            console.error('Failed to save cost:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara kostnad.' });
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
            await apiClient.delete(`/costs/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            const emptyGroups = createEmptyTranslationGroups();
            setTranslationGroups(emptyGroups);
            setOriginalTranslationGroups(emptyGroups);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Kostnad raderades.' });
        } catch (error) {
            console.error('Failed to delete cost:', error);
            const conflictMessage =
                typeof error?.response?.data?.message === 'string' && error.response.data.message.trim()
                    ? error.response.data.message.trim()
                    : 'Kunde inte radera kostnad.';
            setMessage({ type: 'error', text: conflictMessage });
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
                title="RADERA KOSTNAD"
                message={`Ar du saker pa att du vill radera kostnad ${form.name || form.id}? Atgarden kan inte angrras.`}
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

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Kostnader</h2>

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
                                name="costsVisibility"
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
                            <p className="text-xs text-gray-500 py-2">Inga kostnader</p>
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

                    <div className={`min-w-0 mt-3 ${isEditDisabled ? 'opacity-70' : ''}`}>
                        <div className="w-200 grid grid-cols-[400px_1fr] gap-x-20 gap-y-4">
                            <span>
                                <LabeledInput
                                    label="Namn"
                                    labelWidth="w-20"
                                    margintop="0"
                                    value={form.name}
                                    onChange={(value) => setForm((prev) => ({ ...prev, name: value ?? '' }))}
                                    disabled={isEditDisabled}
                                    maxLength={50}
                                    showCharCounter
                                />

                                <LabeledInput
                                    label="Kont inrikes"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.accountDomestic}
                                    onChange={(value) => setForm((prev) => ({ ...prev, accountDomestic: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Kont EU"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.accountEU}
                                    onChange={(value) => setForm((prev) => ({ ...prev, accountEU: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Kont export"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    integerOnly
                                    value={form.accountExport}
                                    onChange={(value) => setForm((prev) => ({ ...prev, accountExport: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Provision %"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    value={form.provisionPercent}
                                    onChange={(value) => setForm((prev) => ({ ...prev, provisionPercent: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="DMT %"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    value={form.dmtPercent}
                                    onChange={(value) => setForm((prev) => ({ ...prev, dmtPercent: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="DMT fast"
                                    labelWidth="w-20"
                                    inputWidth="w-24"
                                    margintop="0"
                                    type="number"
                                    value={form.dmtFixed}
                                    onChange={(value) => setForm((prev) => ({ ...prev, dmtFixed: value }))}
                                    disabled={isEditDisabled}
                                />

                                <LabeledInput
                                    label="Kosttyptext"
                                    labelWidth="w-20"
                                    margintop="0"
                                    value={form.costTypeText}
                                    onChange={(value) => setForm((prev) => ({ ...prev, costTypeText: value ?? '' }))}
                                    disabled={isEditDisabled}
                                    maxLength={50}
                                    showCharCounter
                                />
                                <LabeledSwitch
                                    id="cost-active"
                                    name="cost-active"
                                    label="Aktiv"
                                    labelWidth="w-20"
                                    marginTop={12}
                                    value={form.isActive}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isActive: checked }))}
                                />
                            </span>
                            <span>
                                <LabeledSwitch
                                    id="cost-is-nrof"
                                    name="cost-is-nrof"
                                    label="Är antal"
                                    labelWidth="w-40"
                                    marginTop={0}
                                    value={form.isNrOf}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isNrOf: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-is-calculation"
                                    name="cost-is-calculation"
                                    label="Kalkylerbar"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.isCalculation}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isCalculation: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-is-supplier"
                                    name="cost-is-supplier"
                                    label="Leverantör"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.isSupplier}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isSupplier: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-is-debit-default"
                                    name="cost-is-debit-default"
                                    label="Debitera"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.isDebitDefault}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isDebitDefault: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-print-supplier-default"
                                    name="cost-print-supplier-default"
                                    label="Utskrift beställning"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.doPrintSupplierOrderDefault}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, doPrintSupplierOrderDefault: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-print-quotation-default"
                                    name="cost-print-quotation-default"
                                    label="Utskrift offert"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.doPrintQuotationDefault}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, doPrintQuotationDefault: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-print-customer-order-default"
                                    name="cost-print-customer-order-default"
                                    label="Utskrift ordererkännande"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.doPrintCustomerOrderDefault}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, doPrintCustomerOrderDefault: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-is-customer-default"
                                    name="cost-is-customer-default"
                                    label="Lägg till på ny kund"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.isCustomerDefault}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, isCustomerDefault: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-print-scrap-tools-text"
                                    name="cost-print-scrap-tools-text"
                                    label="Visa disclaimer verktyg på oe"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.doPrintScrapToolsTextOnCustomerOrder}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, doPrintScrapToolsTextOnCustomerOrder: checked }))}
                                />

                                <LabeledSwitch
                                    id="cost-auto-add-if-econopack-transport-responsible"
                                    name="cost-auto-add-if-econopack-transport-responsible"
                                    label="Lägg till auto vid Econopack transport"
                                    labelWidth="w-40"
                                    marginTop={4}
                                    value={form.addAutomaicIfEconopackIsTransportReponsible}
                                    disabled={isEditDisabled}
                                    onChange={(_, __, checked) => setForm((prev) => ({ ...prev, addAutomaicIfEconopackIsTransportReponsible: checked }))}
                                />
                            </span>
                        </div>

                        <div className="mt-12">
                            {translationSections.map((section) => (
                                <div key={section.key} className="mb-6">
                                    <h3 className="text-xs text-gray-700 mb-1">{section.title}</h3>
                                    <div className="border-t border-gray-300 pt-1">
                                        <div className="grid grid-cols-[56px_1fr] gap-2 text-[10px] text-gray-500 pb-1">
                                            <span>SPRAK</span>
                                            <span>OVERSATTNING</span>
                                        </div>

                                        {(translationGroups?.[section.key] ?? []).map((row) => (
                                            <div key={`${section.key}-${row.langCode}`} className="grid grid-cols-[56px_1fr] gap-2 mb-1">
                                                <input
                                                    type="text"
                                                    value={row.langCode}
                                                    disabled
                                                    className="text-xs border border-gray-300 rounded-sm px-2 py-1 bg-gray-100 text-gray-700"
                                                />
                                                <input
                                                    type="text"
                                                    value={row.translation ?? ''}
                                                    disabled={isEditDisabled}
                                                    onChange={(event) => updateTranslation(section.key, row.langCode, event.target.value)}
                                                    className="text-xs border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                                                    maxLength={255}
                                                />
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            ))}
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

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? ''),
    isActive: Boolean(form?.isActive),
    isNrOf: Boolean(form?.isNrOf),
    isCalculation: Boolean(form?.isCalculation),
    isSupplier: Boolean(form?.isSupplier),
    accountDomestic: parseNullableInt(form?.accountDomestic),
    accountEU: parseNullableInt(form?.accountEU),
    accountExport: parseNullableInt(form?.accountExport),
    translationCodeSupplierOrderUnknown: parseIntOrDefault(form?.translationCodeSupplierOrderUnknown),
    translationCodeSupplierOrderKnown: parseIntOrDefault(form?.translationCodeSupplierOrderKnown),
    translationCodeQuotationUnknown: parseIntOrDefault(form?.translationCodeQuotationUnknown),
    translationCodeQuotationKnown: parseIntOrDefault(form?.translationCodeQuotationKnown),
    translationCodeCustomerOrderUnknown: parseIntOrDefault(form?.translationCodeCustomerOrderUnknown),
    translationCodeCustomerOrderKnown: parseIntOrDefault(form?.translationCodeCustomerOrderKnown),
    translationCodeInvoiceRow: parseIntOrDefault(form?.translationCodeInvoiceRow),
    isDebitDefault: Boolean(form?.isDebitDefault),
    doPrintSupplierOrderDefault: Boolean(form?.doPrintSupplierOrderDefault),
    doPrintQuotationDefault: Boolean(form?.doPrintQuotationDefault),
    doPrintCustomerOrderDefault: Boolean(form?.doPrintCustomerOrderDefault),
    isCustomerDefault: Boolean(form?.isCustomerDefault),
    doPrintScrapToolsTextOnCustomerOrder: Boolean(form?.doPrintScrapToolsTextOnCustomerOrder),
    addAutomaicIfEconopackIsTransportReponsible: Boolean(form?.addAutomaicIfEconopackIsTransportReponsible),
    dmtPercent: parseNullableNumber(form?.dmtPercent),
    dmtFixed: parseNullableNumber(form?.dmtFixed),
    provisionPercent: parseNullableNumber(form?.provisionPercent),
    costTypeText: String(form?.costTypeText ?? ''),
});

export default CostsSettings;
