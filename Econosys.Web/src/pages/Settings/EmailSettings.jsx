import { useEffect, useMemo, useState } from 'react';
import apiClient from '../../config/apiClient';
import LabeledInput from '../../components/LabeledInput';
import LabeledTextArea from '../../components/LabeledTextArea';

const COMPANY_ID = 1;

const defaultForm = {
    mailWrapper: '',
    settings: [],
};

const mapDtoToForm = (dto) => ({
    mailWrapper: dto?.mailWrapper ?? '',
    settings: Array.isArray(dto?.settings)
        ? dto.settings.map((item) => ({
            id: item?.id ?? null,
            setting: item?.setting ?? '',
            value: item?.value ?? '',
        }))
        : [],
});

const mapFormToPayload = (form) => ({
    mailWrapper: String(form?.mailWrapper ?? ''),
    settings: (form?.settings ?? [])
        .map((item) => ({
            id: item?.id ?? null,
            setting: String(item?.setting ?? '').trim(),
            value: String(item?.value ?? ''),
        }))
        .filter((item) => item.setting !== ''),
});

const buildApiErrorMessage = (error, fallbackMessage) => {
    const data = error?.response?.data;

    if (typeof data === 'string' && data.trim()) {
        return data.trim();
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

const EmailSettings = () => {
    const [form, setForm] = useState(defaultForm);
    const [selectedId, setSelectedId] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState(null);
    const [search, setSearch] = useState('');

    const load = async () => {
        setIsLoading(true);
        setMessage(null);

        try {
            const response = await apiClient.get(`/mailsettings/${COMPANY_ID}`);
            const mapped = mapDtoToForm(response?.data);
            setForm(mapped);
            setSelectedId(mapped.settings[0]?.id ?? null);
        } catch (error) {
            if (error?.response?.status === 404) {
                setForm(defaultForm);
                setSelectedId(null);
            } else {
                setMessage({ type: 'error', text: buildApiErrorMessage(error, 'Kunde inte hamta epostinstallningar.') });
            }
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        load();
    }, []);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        if (!term) return form.settings;

        return form.settings.filter((item) => {
            const setting = String(item?.setting ?? '').toLowerCase();
            const value = String(item?.value ?? '').toLowerCase();
            return setting.includes(term) || value.includes(term);
        });
    }, [form.settings, search]);

    const selectedIndex = form.settings.findIndex((x) => x.id === selectedId);
    const selected = selectedIndex >= 0 ? form.settings[selectedIndex] : null;

    const handleCreateNew = () => {
        const nextId = Date.now() * -1;
        const next = [...form.settings, { id: nextId, setting: '', value: '' }];
        setForm((prev) => ({ ...prev, settings: next }));
        setSelectedId(nextId);
        setMessage(null);
    };

    const handleDeleteSelected = () => {
        if (!selected) return;

        const next = form.settings.filter((x) => x.id !== selected.id);
        setForm((prev) => ({ ...prev, settings: next }));
        setSelectedId(next[0]?.id ?? null);
        setMessage(null);
    };

    const updateSelected = (field, value) => {
        if (!selected) return;

        const next = form.settings.map((item) => (
            item.id === selected.id
                ? { ...item, [field]: value ?? '' }
                : item
        ));

        setForm((prev) => ({ ...prev, settings: next }));
    };

    const handleSave = async () => {
        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = await apiClient.put(`/mailsettings/${COMPANY_ID}`, payload);
            const mapped = mapDtoToForm(response?.data);
            setForm(mapped);
            setSelectedId(mapped.settings[0]?.id ?? null);
            setMessage({ type: 'success', text: 'Epostinstallningar sparades.' });
        } catch (error) {
            console.error('Failed to save epostinstallningar:', error);
            setMessage({ type: 'error', text: buildApiErrorMessage(error, 'Kunde inte spara epostinstallningar.') });
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Epostinställningar</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="w-[450px] shrink-0 mb-5 px-4 py-2 border-r border-gray-300">
                    <div className="flex items-center gap-2">
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
                            disabled={isLoading || isSaving}
                            className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            Ny
                        </button>
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoading ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga installningar</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = selectedId === item.id;

                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => setSelectedId(item.id)}
                                        className={`w-full text-left text-xs px-2 py-1 rounded-sm grid grid-cols-[100px_1fr] gap-2 items-center ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span className="text-gray-600 truncate">{item.setting || '(tom)'}</span>
                                        <span className="truncate">{item.value || ''}</span>
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
                            disabled={isLoading || isSaving}
                            className="w-24 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            {isSaving ? 'Sparar...' : 'Spara'}
                        </button>
                        <button
                            type="button"
                            onClick={handleDeleteSelected}
                            disabled={!selected || isSaving || isLoading}
                            className="w-24 shadow-md/30 text-xs text-white bg-red-600 hover:bg-red-800 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            Radera
                        </button>
                    </div>

                    <div className={`${isLoading ? 'opacity-70' : ''}`}>
                        {!selected ? (
                            <p className="text-xs text-gray-600 mb-6">Valj en installning eller skapa en ny.</p>
                        ) : (
                            <div className="w-[600px]">
                                <LabeledInput
                                    label="Nyckel"
                                    labelWidth="w-20"
                                    margintop="0"
                                    value={selected.setting}
                                    onChange={(value) => updateSelected('setting', value ?? '')}
                                    maxLength={50}
                                />
                                <LabeledInput
                                    label="Varde"
                                    labelWidth="w-20"
                                    margintop="0"
                                    value={selected.value}
                                    onChange={(value) => updateSelected('value', value ?? '')}
                                    maxLength={255}
                                />
                            </div>
                        )}

                        <div className="mt-10 w-[900px]">
                            <LabeledTextArea
                                label="Mail wrapper"
                                labelWidth="w-20"
                                margintop="0"
                                inputWidth="w-full"
                                height="h-56"
                                value={form.mailWrapper}
                                onChange={(value) => setForm((prev) => ({ ...prev, mailWrapper: value ?? '' }))}
                                maxLength={5000}
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

export default EmailSettings;
