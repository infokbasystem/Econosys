import { useEffect, useMemo, useState } from 'react';
import LabeledInput from '../../components/LabeledInput';
import apiClient from '../../config/apiClient';

const defaultForm = {
    id: null,
    name: '',
};

const UsersSettings = () => {
    const [items, setItems] = useState([]);
    const [search, setSearch] = useState('');
    const [form, setForm] = useState(defaultForm);
    const [isLoadingList, setIsLoadingList] = useState(true);
    const [message, setMessage] = useState(null);

    const loadList = async () => {
        setIsLoadingList(true);
        try {
            const response = await apiClient.get('/deviations/form-options');
            const responseUsers = response?.data?.users ?? [];
            const users = responseUsers
                .filter((item) => item?.id != null)
                .map((item) => ({
                    id: item.id,
                    name: String(item.name ?? ''),
                }));

            setItems(users);
            setMessage({ type: 'info', text: 'Anvandare visas read-only fran befintlig options-endpoint.' });
        } catch (error) {
            console.error('Failed to load users list:', error);
            setMessage({ type: 'error', text: 'Kunde inte hamta anvandare.' });
        } finally {
            setIsLoadingList(false);
        }
    };

    useEffect(() => {
        loadList();
    }, []);

    const filteredItems = useMemo(() => {
        const term = search.trim().toLowerCase();
        if (!term) return items;

        return items.filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            return idText.includes(term) || nameText.includes(term);
        });
    }, [items, search]);

    const selectItem = (item) => {
        setForm({
            id: item?.id ?? null,
            name: String(item?.name ?? ''),
        });
    };

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Anvandare</h2>

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
                    </div>

                    <div className="mt-5 border-t border-gray-200 pt-2 space-y-0.5 max-h-[calc(100vh-230px)] overflow-y-auto">
                        {isLoadingList ? (
                            <p className="text-xs text-gray-500 py-2">Laddar...</p>
                        ) : filteredItems.length === 0 ? (
                            <p className="text-xs text-gray-500 py-2">Inga anvandare</p>
                        ) : (
                            filteredItems.map((item) => {
                                const isSelected = form.id === item.id;
                                return (
                                    <button
                                        key={item.id}
                                        type="button"
                                        onClick={() => selectItem(item)}
                                        className={`w-full text-left text-xs px-2 py-0.5 rounded-sm grid grid-cols-[70px_1fr] gap-2 ${isSelected ? 'bg-yellow-300 text-black' : 'hover:bg-gray-100 text-gray-900'}`}
                                    >
                                        <span>{item.id}</span>
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
                            disabled
                            className="w-24 shadow-md/30 text-xs text-white bg-gray-500 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px] rounded-sm"
                        >
                            Read-only
                        </button>
                    </div>

                    <div className="min-w-0 mt-3">
                        <LabeledInput
                            label="ID"
                            labelWidth="w-20"
                            margintop="0"
                            value={form.id ?? ''}
                            disabled
                        />
                        <LabeledInput
                            label="Namn"
                            labelWidth="w-20"
                            margintop="0"
                            value={form.name}
                            disabled
                        />
                    </div>
                </div>

                <div className="flex flex-col w-80 shrink-0 border-l border-gray-300 pl-4 py-2 mb-5">
                    <h2 className="text-sm text-center text-gray-700 mt-1">Meddelanden</h2>
                    {!message ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            <li className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' : 'bg-blue-100 text-blue-700'}`}>
                                {message.text}
                            </li>
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
};

export default UsersSettings;
