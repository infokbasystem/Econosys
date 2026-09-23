import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Search } from 'lucide-react';

import apiClient from '../config/apiClient';

// Legacy parity: Econosys.Wpf CreateDeliveryFromStockView / CreateDeliveryFromStockViewModel.
// Search a customer order by order number and pick a stock balance group to create a delivery from.
const SelectNewDeliveryInfo = ({
    isOpen,
    onClose,
    onSelect,
    isSubmitting = false,
    submitError = '',
    title = 'Skapa leverans från lager',
}) => {
    const [orderNrSearch, setOrderNrSearch] = useState('');
    const [candidate, setCandidate] = useState(null);
    const [isSearching, setIsSearching] = useState(false);
    const [searchError, setSearchError] = useState('');
    const [submittingEditionPerPallet, setSubmittingEditionPerPallet] = useState(undefined);
    const searchRequestRef = useRef(0);

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        setOrderNrSearch('');
        setCandidate(null);
        setIsSearching(false);
        setSearchError('');
        setSubmittingEditionPerPallet(undefined);
    }, [isOpen]);

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        const term = orderNrSearch.trim();
        if (term.length < 3) {
            setCandidate(null);
            setSearchError('');
            setIsSearching(false);
            return;
        }

        const requestId = searchRequestRef.current + 1;
        searchRequestRef.current = requestId;
        setIsSearching(true);
        setSearchError('');

        const timeoutId = setTimeout(async () => {
            try {
                const response = await apiClient.get('/calloff/delivery-candidates', { params: { customerOrderNr: term } });
                if (searchRequestRef.current !== requestId) {
                    return;
                }
                setCandidate(response?.data ?? null);
            } catch (error) {
                if (searchRequestRef.current !== requestId) {
                    return;
                }
                setCandidate(null);
                if (error.response?.status === 404) {
                    setSearchError('Ingen order hittades med det ordernumret.');
                } else {
                    console.error('Failed to search delivery candidates:', error);
                    setSearchError('Kunde inte söka efter order.');
                }
            } finally {
                if (searchRequestRef.current === requestId) {
                    setIsSearching(false);
                }
            }
        }, 300);

        return () => clearTimeout(timeoutId);
    }, [isOpen, orderNrSearch]);

    if (!isOpen) {
        return null;
    }

    const groups = candidate?.groups ?? [];

    const handleSelect = (group) => {
        if (isSubmitting) {
            return;
        }

        setSubmittingEditionPerPallet(group.editionPerPallet);
        onSelect?.(candidate, group);
    };

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20">
                <div
                    className="relative bg-white rounded-sm shadow-xl w-full max-w-sm mx-4 p-6"
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

                    <div className="mx-3">
                        <label className="block text-xs text-gray-600 mt-5 mb-1 text-center">Ordernummer</label>
                        <div className="relative mt-2 w-40 mx-auto">
                            <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                            <input
                                type="text"
                                autoFocus
                                value={orderNrSearch}
                                onChange={(event) => setOrderNrSearch(event.target.value)}
                                placeholder="Sök ordernummer"
                                className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                            />
                        </div>

                        {candidate ? (
                            <div className="mt-5 text-center text-xs text-gray-700">
                                <div className="font-semibold">{candidate.customerOrderNr}</div>
                                <div>{candidate.customerName}</div>
                                <div className="text-gray-500">{candidate.productName}</div>
                            </div>
                        ) : null}

                        <div className="mt-4">
                            {isSearching ? (
                                <p className="text-xs text-gray-600 py-3 text-center">Söker...</p>
                            ) : searchError ? (
                                <p className="text-xs text-gray-600 py-3 text-center">{searchError}</p>
                            ) : orderNrSearch.trim().length < 3 ? (
                                <p className="text-xs text-gray-600 py-3 text-center">Ange minst 3 tecken av ordernumret.</p>
                            ) : groups.length === 0 ? (
                                <p className="text-xs text-gray-600 py-3 text-center">Inget saldo att leverera hittades för ordern.</p>
                            ) : (
                                groups.map((group) => {
                                    const isSubmittingThisGroup = isSubmitting && submittingEditionPerPallet === group.editionPerPallet;
                                    return (
                                        <div
                                            key={`delivery-candidate-group-${group.editionPerPallet ?? 'none'}`}
                                            className="border-t border-gray-400 py-2.5"
                                        >
                                            <div className="grid grid-cols-[70px_55px_55px_1fr_60px] items-baseline gap-1">
                                                <div className="text-xs font-bold text-gray-800">{group.editionPerPallet ?? '-'}/pall</div>
                                                <div className="text-right text-xs text-gray-600">Saldo</div>
                                                <div className="text-right text-xs text-gray-600">Saldo nu</div>
                                                <div />
                                                <button
                                                    type="button"
                                                    onClick={() => handleSelect(group)}
                                                    disabled={isSubmitting}
                                                    className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:bg-gray-400 disabled:cursor-not-allowed px-4 py-[5px] justify-self-end"
                                                >
                                                    {isSubmittingThisGroup ? 'Skapar...' : 'Välj'}
                                                </button>
                                            </div>
                                            <div className="grid grid-cols-[70px_55px_55px_1fr_60px] items-center gap-1">
                                                <div className="text-xs text-gray-600">Antal</div>
                                                <div className="text-right text-xs text-gray-800">{group.nrOf}</div>
                                                <div className="text-right text-xs text-gray-800">{group.nrOfNow}</div>
                                            </div>
                                            <div className="grid grid-cols-[70px_55px_55px_1fr_60px] items-center gap-1">
                                                <div className="text-xs text-gray-600">Pall</div>
                                                <div className="text-right text-xs text-gray-800">{group.nrOfPallets}</div>
                                                <div className="text-right text-xs text-gray-800">{group.nrOfPalletsNow}</div>
                                            </div>
                                        </div>
                                    );
                                })
                            )}
                            {groups.length > 0 ? <div className="border-t border-gray-400" /> : null}
                        </div>

                        {submitError ? (
                            <p className="mt-3 text-xs text-red-600 text-center">{submitError}</p>
                        ) : null}

                        <div className="flex mt-10 mb-1 justify-end">
                            <button
                                type="button"
                                onClick={onClose}
                                disabled={isSubmitting}
                                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                Avbryt
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default SelectNewDeliveryInfo;
