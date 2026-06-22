import { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import RichTextEditor from '../../components/RichTextEditor';

const defaultForm = {
    id: null,
    item: '',
    subjectTranslations: [],
    bodyTranslations: [],
};

const mapTranslations = (translations, useHtml) => {
    if (!Array.isArray(translations)) return [];

    return translations
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item?.langCode ?? '').trim(),
            text: useHtml
                ? String(item?.translationHtml ?? item?.translation ?? '')
                : String(item?.translation ?? ''),
        }));
};

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    item: dto?.item ?? '',
    subjectTranslations: mapTranslations(dto?.subjectTranslations, false),
    bodyTranslations: mapTranslations(dto?.bodyTranslations, true),
});

const stripHtmlToPlainText = (html) =>
    String(html ?? '')
        .replace(/<[^>]+>/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();

const toMaxLength = (value, maxLength) => String(value ?? '').slice(0, maxLength);

const mapFormToPayload = (form) => ({
    item: String(form?.item ?? '').trim(),
    subjectTranslations: (form?.subjectTranslations ?? []).map((item) => ({
        langCode: String(item?.langCode ?? ''),
        translation: toMaxLength(String(item?.text ?? ''), 255),
        translationHtml: null,
    })),
    bodyTranslations: (form?.bodyTranslations ?? []).map((item) => ({
        langCode: String(item?.langCode ?? ''),
        translation: toMaxLength(stripHtmlToPlainText(item?.text), 255),
        translationHtml: toMaxLength(String(item?.text ?? ''), 8000),
    })),
});

const buildApiErrorMessage = (error, fallbackMessage) => {
    const data = error?.response?.data;

    if (typeof data === 'string' && data.trim()) {
        return data.trim();
    }

    if (Array.isArray(data?.errors)) {
        const all = data.errors
            .filter((item) => typeof item === 'string' && item.trim())
            .map((item) => item.trim());
        if (all.length > 0) {
            return all.join(' ');
        }
    }

    if (data?.errors && typeof data.errors === 'object') {
        const values = Object.values(data.errors)
            .flat()
            .filter((item) => typeof item === 'string' && item.trim())
            .map((item) => item.trim());
        if (values.length > 0) {
            return values.join(' ');
        }
    }

    const message = typeof data?.message === 'string' ? data.message.trim() : '';
    return message || fallbackMessage;
};

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    item: String(form?.item ?? '').trim(),
    subjectTranslations: (form?.subjectTranslations ?? [])
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item.langCode).trim(),
            text: String(item.text ?? ''),
        })),
    bodyTranslations: (form?.bodyTranslations ?? [])
        .filter((item) => String(item?.langCode ?? '').trim() !== '')
        .map((item) => ({
            langCode: String(item.langCode).trim(),
            text: String(item.text ?? ''),
        })),
});

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

const EmailTextsSettings = () => {
    const [items, setItems] = useState([]);
    const [search, setSearch] = useState('');
    const [form, setForm] = useState(defaultForm);
    const [originalForm, setOriginalForm] = useState(defaultForm);
    const [isLoadingList, setIsLoadingList] = useState(true);
    const [isLoadingDetails, setIsLoadingDetails] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState(null);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedAction, setUnsavedAction] = useState(null);

    const hasUnsavedChanges = useCallback(() => {
        if (isLoadingDetails) return false;
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);

    const isEditDisabled = isLoadingDetails || !form.id;

    const loadList = async () => {
        setIsLoadingList(true);

        try {
            const response = await apiClient.get('/mailtexts/search');
            setItems(Array.isArray(response?.data) ? response.data : []);
        } catch (error) {
            console.error('Failed to load e-posttexter list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta e-posttexter.' });
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

        return items.filter((item) => String(item?.item ?? '').toLowerCase().includes(term));
    }, [items, search]);

    const selectItem = async (id) => {
        if (!id) return;

        setIsLoadingDetails(true);
        setMessage(null);

        try {
            const response = await apiClient.get(`/mailtexts/${id}`);
            const mapped = mapDtoToForm(response?.data);
            setForm(mapped);
            setOriginalForm(mapped);
        } catch (error) {
            console.error('Failed to load e-posttext details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta e-posttext.' });
        } finally {
            setIsLoadingDetails(false);
        }
    };

    const handleSelectItem = (id) => {
        if (form.id === id) return;

        if (hasUnsavedChanges()) {
            setUnsavedAction({ type: 'select', id });
            setShowUnsavedWarning(true);
            return;
        }

        selectItem(id);
    };

    const updateTranslation = (section, langCode, text) => {
        const key = section === 'subject' ? 'subjectTranslations' : 'bodyTranslations';

        setForm((prev) => {
            const current = Array.isArray(prev?.[key]) ? prev[key] : [];
            const next = current.some((item) => item.langCode === langCode)
                ? current.map((item) => (item.langCode === langCode ? { ...item, text: text ?? '' } : item))
                : [...current, { langCode, text: text ?? '' }];

            return {
                ...prev,
                [key]: next,
            };
        });
    };

    const handleSave = async () => {
        if (!form.id) return;

        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = await apiClient.put(`/mailtexts/${form.id}`, payload);
            const mapped = mapDtoToForm(response?.data);

            setForm(mapped);
            setOriginalForm(mapped);
            await loadList();
            setMessage({ type: 'success', text: 'E-posttext sparades.' });
        } catch (error) {
            console.error('Failed to save e-posttext:', error);
            setMessage({ type: 'error', text: buildApiErrorMessage(error, 'Kunde inte spara e-posttext.') });
        } finally {
            setIsSaving(false);
        }
    };

    const handleUnsavedWarningAbort = () => {
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
        setShowUnsavedWarning(false);
        setUnsavedAction(null);
    };

    const handleUnsavedWarningConfirm = () => {
        const action = unsavedAction;

        if (blocker.state === 'blocked') {
            blocker.proceed();
        }

        setShowUnsavedWarning(false);
        setUnsavedAction(null);

        if (action?.type === 'select' && action.id) {
            selectItem(action.id);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
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

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">E-posttexter</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[350px] shrink-0 mb-5 px-4 py-2 border-r border-gray-300">
                    <div className="mr-5">
                        <LabeledInput
                            label="Sok"
                            labelWidth="w-8"
                            inputWidth="w-30"
                            margintop="0"
                            value={search}
                            onChange={(value) => setSearch(value ?? '')}
                        />
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga e-posttexter</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;

                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelectItem(item.id)}
                                        className={`w-full text-left text-xs px-2 py-1 rounded-sm grid grid-cols-[72px_1fr] gap-2 items-center ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="text-gray-600">{item.id}</span>
                                        <span className="truncate">{item.item}</span>
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
                            disabled={isEditDisabled || isSaving}
                            className="w-24 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isSaving ? 'Sparar...' : 'Spara'}
                        </button>
                    </div>

                    {!form.id ? (
                        <h2 className="text-sm text-red-500 mb-4 text-center">Valj en e-posttext att redigera</h2>
                    ) : (
                        <>
                            <div className={`items-start grid grid-cols-1 md:grid-cols-[400px_1fr] gap-10 ${isEditDisabled ? 'opacity-70' : ''}`}>
                                <LabeledInput
                                    label="Item"
                                    labelWidth="w-20"
                                    margintop="0"
                                    value={form.item}
                                    disabled={true}
                                />

                                <div>
                                    <div>
                                        <p className="text-xs font-semibold text-gray-700 mt-2 mb-1">Du kan använda följande koder</p>
                                    </div>
                                    <div className="flex flex-col md:flex-row md:items-center gap-2">
                                        <div>
                                            <p className="mt-1 text-xs text-gray-700">%NR%</p>
                                            <p className="mt-1 text-xs text-gray-700">%PRODUKTNAMN%</p>
                                            <p className="mt-1 text-xs text-gray-700">%ERTORDERNR%</p>
                                            <p className="mt-1 text-xs text-gray-700">%COMPANYDOMESTICPHONE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%COMPANYINTERNATIONALPHONE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%COMPANYEMIAL%</p>
                                            <p className="mt-1 text-xs text-gray-700">%COMPANYWEB%</p>
                                        </div>
                                        <div>
                                            <p className="mt-1 text-xs text-gray-700">%LOGO%</p>
                                            <p className="mt-1 text-xs text-gray-700">%SIGNATURE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%EMPLOYEENAME%</p>
                                            <p className="mt-1 text-xs text-gray-700">%EMPLOYETITLE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%EMPLOYEDIRECTPHONE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%EMPLOYEMOBILEPHONE%</p>
                                            <p className="mt-1 text-xs text-gray-700">%LEVERANS%</p>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div className={`grid grid-cols-1 md:grid-cols-[400px_1fr] gap-10 mt-10 ${isEditDisabled ? 'opacity-70' : ''}`}>
                                <div>
                                    <h3 className="text-sm text-gray-700 mb-2">Amnesoversattningar</h3>
                                    <div className="border-t border-gray-300 pt-2 space-y-1">
                                        {(form.subjectTranslations ?? []).length === 0 ? (
                                            <p className="text-xs text-gray-500">Inga sprak tillgangliga.</p>
                                        ) : (
                                            (form.subjectTranslations ?? []).map((item) => (
                                                <LabeledInput
                                                    key={`subject-${item.langCode}`}
                                                    label={formatLangLabel(item.langCode)}
                                                    labelWidth="w-20"
                                                    margintop="0"
                                                    value={item.text ?? ''}
                                                    onChange={(value) => updateTranslation('subject', item.langCode, value ?? '')}
                                                    disabled={isEditDisabled}
                                                    maxLength={255}
                                                    showCharCounter
                                                />
                                            ))
                                        )}
                                    </div>
                                </div>

                                <div>
                                    <h3 className="text-sm text-gray-700 mb-2">Brodtextoversattningar (HTML)</h3>
                                    <div className="border-t border-gray-300 pt-2 space-y-3">
                                        {(form.bodyTranslations ?? []).length === 0 ? (
                                            <p className="text-xs text-gray-500">Inga sprak tillgangliga.</p>
                                        ) : (
                                            (form.bodyTranslations ?? []).map((item) => (
                                                <div key={`body-${item.langCode}`} className="space-y-1">
                                                    <label className="text-xs text-gray-700">{formatLangLabel(item.langCode)}</label>
                                                    <RichTextEditor
                                                        value={item.text ?? ''}
                                                        onChange={(html) => updateTranslation('body', item.langCode, html ?? '')}
                                                        placeholder="Skriv e-postens brodtext har..."
                                                    />
                                                </div>
                                            ))
                                        )}
                                    </div>
                                </div>
                            </div>
                        </>
                    )}
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

export default EmailTextsSettings;
