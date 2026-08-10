import { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';

const InventorySelectModal = ({
    isOpen,
    onClose,
    onSelect,
    inventories,
    isLoading,
    title = 'VALJ LAGER / OMLASTNINGSPLATS',
}) => {
    const [search, setSearch] = useState('');
    const [selectedInventoryId, setSelectedInventoryId] = useState(null);

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        setSearch('');
        setSelectedInventoryId(null);
    }, [isOpen]);

    const filteredInventories = useMemo(() => {
        const term = search.trim().toLowerCase();
        if (!term) {
            return Array.isArray(inventories) ? inventories : [];
        }

        return (Array.isArray(inventories) ? inventories : []).filter((item) => {
            const idText = String(item?.id ?? '').toLowerCase();
            const nameText = String(item?.name ?? '').toLowerCase();
            const addressText = String(item?.address ?? '').toLowerCase();
            const postalAddressText = String(item?.postalAddress ?? '').toLowerCase();
            return idText.includes(term)
                || nameText.includes(term)
                || addressText.includes(term)
                || postalAddressText.includes(term);
        });
    }, [inventories, search]);

    const selectedInventory = filteredInventories.find((item) => item?.id === selectedInventoryId)
        ?? (Array.isArray(inventories) ? inventories : []).find((item) => item?.id === selectedInventoryId)
        ?? null;

    if (!isOpen) {
        return null;
    }

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={onClose}>
                <div
                    className="relative bg-white rounded-sm shadow-xl w-full max-w-xl mx-4 p-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(event) => event.stopPropagation()}
                >
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold text-center">{title}</h2>
                        <button
                            type="button"
                            onClick={onClose}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
                        >
                            x
                        </button>
                    </div>

                    <div className="mx-2">
                        <input
                            type="text"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                            placeholder="Sok lager, id eller adress"
                            className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-xs focus:outline-none"
                        />

                        <div className="mt-3 border border-gray-300 rounded-sm bg-white max-h-80 overflow-y-auto">
                            {isLoading ? (
                                <p className="text-xs text-gray-600 p-3">Laddar lager...</p>
                            ) : filteredInventories.length === 0 ? (
                                <p className="text-xs text-gray-600 p-3">Inga lager / omlastningsplatser hittades.</p>
                            ) : (
                                <ul className="divide-y divide-gray-100">
                                    {filteredInventories.map((item) => {
                                        const isSelected = item?.id === selectedInventoryId;
                                        return (
                                            <li
                                                key={String(item?.id ?? '')}
                                                className={`px-3 py-2 cursor-pointer ${isSelected ? 'bg-yellow-200' : 'hover:bg-yellow-50'}`}
                                                onClick={() => setSelectedInventoryId(item?.id ?? null)}
                                                onDoubleClick={() => onSelect(item)}
                                            >
                                                <div className="flex items-center justify-between gap-3">
                                                    <div className="min-w-0">
                                                        <div className="text-xs font-medium text-gray-800 truncate">
                                                            {item?.name || `Lager ${item?.id ?? ''}`}
                                                        </div>
                                                        <div className="text-[11px] text-gray-500 truncate">
                                                            {item?.postalAddress ? `${item.postalAddress}` : ''}
                                                            {item?.address ? ` | ${item.address}` : ''}
                                                        </div>
                                                    </div>
                                                    <div className="shrink-0 text-[10px] text-gray-600">
                                                        {item?.isInventory ? 'Lager' : ''}
                                                        {item?.isInventory && item?.isOmlast ? ' / ' : ''}
                                                        {item?.isOmlast ? 'Omlast' : ''}
                                                    </div>
                                                </div>
                                            </li>
                                        );
                                    })}
                                </ul>
                            )}
                        </div>

                        <div className="flex gap-4 mt-6 mb-1 justify-end">
                            <button
                                type="button"
                                onClick={onClose}
                                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px]"
                            >
                                Avbryt
                            </button>
                            <button
                                type="button"
                                onClick={() => selectedInventory && onSelect(selectedInventory)}
                                disabled={!selectedInventory || isLoading}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:bg-gray-400 disabled:cursor-not-allowed px-10 p-[5px]"
                            >
                                Valj
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default InventorySelectModal;
