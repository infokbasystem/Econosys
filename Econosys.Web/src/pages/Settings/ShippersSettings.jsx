import { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';

const defaultForm = {
    id: null,
    name: '',
    contactPerson: '',
    telephone: '',
    mail: '',
};

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    name: String(dto?.name ?? ''),
    contactPerson: String(dto?.contactPerson ?? ''),
    telephone: String(dto?.telephone ?? ''),
    mail: String(dto?.mail ?? ''),
});

const mapFormToPayload = (form) => ({
    name: String(form.name ?? '').trim(),
    contactPerson: String(form.contactPerson ?? '').trim() || null,
    telephone: String(form.telephone ?? '').trim() || null,
    mail: String(form.mail ?? '').trim() || null,
});

const normalizeFormForCompare = (form) => ({
    id: form?.id ?? null,
    name: String(form?.name ?? '').trim(),
    contactPerson: String(form?.contactPerson ?? '').trim(),
    telephone: String(form?.telephone ?? '').trim(),
    mail: String(form?.mail ?? '').trim(),
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

const buildDeleteConflictMessage = (error) => {
    const data = error?.response?.data;

    if (typeof data === 'string' && data.trim()) {
        return data.trim();
    }

    const message = typeof data?.message === 'string' ? data.message.trim() : '';
    if (message) {
        return message;
    }

    return 'Kunde inte radera transportoren.';
};

const ShippersSettings = () => {
    const [items, setItems] = useState([]);
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
        return JSON.stringify(normalizeFormForCompare(form)) !== JSON.stringify(normalizeFormForCompare(originalForm));
    }, [form, originalForm, isLoadingDetails]);

    const blocker = useBlocker(hasUnsavedChanges);
    const isEditDisabled = isLoadingDetails || (!isCreateMode && !form.id);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.get('/shippers/search');
            setItems(Array.isArray(response?.data) ? response.data : []);
        } catch (error) {
            console.error('Failed to load shippers list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta transportorer.' });
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
            const contactText = String(item?.contactPerson ?? '').toLowerCase();
            const telephoneText = String(item?.telephone ?? '').toLowerCase();
            const mailText = String(item?.mail ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term) || contactText.includes(term) || telephoneText.includes(term) || mailText.includes(term);
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
            const response = await apiClient.get(`/shippers/${id}`);
            const mapped = mapDtoToForm(response.data);
            setForm(mapped);
            setOriginalForm(mapped);
        } catch (error) {
            console.error('Failed to load shipper details:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta transportor.' });
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
                ? await apiClient.post('/shippers', payload)
                : await apiClient.put(`/shippers/${form.id}`, payload);

            const saved = mapDtoToForm(response.data);
            setForm(saved);
            setOriginalForm(saved);
            setIsCreateMode(false);
            await loadList();
            setMessage({ type: 'success', text: isCreateMode ? 'Transportor skapades.' : 'Transportor uppdaterades.' });
        } catch (error) {
            console.error('Failed to save shipper:', error);
            setMessage({
                type: 'error',
                text: buildApiErrorMessage(error, isCreateMode ? 'Kunde inte skapa transportor.' : 'Kunde inte uppdatera transportor.'),
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
            await apiClient.delete(`/shippers/${form.id}`);
            await loadList();
            setForm(defaultForm);
            setOriginalForm(defaultForm);
            setIsCreateMode(false);
            setMessage({ type: 'success', text: 'Transportor raderades.' });
        } catch (error) {
            console.error('Failed to delete shipper:', error);
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
                title="RADERA TRANSPORTOR"
                message={`Ar du saker pa att du vill radera transportor ${form.name || form.id}? Atgarden kan inte angras.`}
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

            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Transportörer</h2>

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
                            <p className="text-xs text-gray-500 py-2">Inga transportorer</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;
                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => handleSelect(item.id)}
                                        className={`w-full text-left text-xs px-2 py-0.5 rounded-sm grid grid-cols-[1fr_110px] gap-2 ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="truncate">{item.name}</span>
                                        <span className="truncate text-right">{item.telephone ?? ''}</span>
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
                        <div className="w-140">
                            <LabeledInput
                                label="Namn"
                                labelWidth="w-24"
                                margintop="0"
                                value={form.name}
                                onChange={(value) => updateField('name', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={100}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Kontaktperson"
                                labelWidth="w-24"
                                margintop="0"
                                value={form.contactPerson}
                                onChange={(value) => updateField('contactPerson', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={100}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Telefon"
                                labelWidth="w-24"
                                margintop="0"
                                value={form.telephone}
                                onChange={(value) => updateField('telephone', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={200}
                                showCharCounter
                            />

                            <LabeledInput
                                label="Epost"
                                labelWidth="w-24"
                                margintop="0"
                                value={form.mail}
                                onChange={(value) => updateField('mail', value ?? '')}
                                disabled={isEditDisabled}
                                maxLength={200}
                                showCharCounter
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

export default ShippersSettings;
