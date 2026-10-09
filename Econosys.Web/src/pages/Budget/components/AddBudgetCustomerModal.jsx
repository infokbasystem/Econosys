import { useEffect, useState } from 'react';

import LabeledInput from '../../../components/LabeledInput';
import apiClient from '../../../config/apiClient';
import BudgetModal from './BudgetModal';

const SEARCH_DEBOUNCE_MS = 300;

const AddBudgetCustomerModal = ({ isOpen, budgetId, excludedCustomerIds, onClose, onSelect }) => {
    const [search, setSearch] = useState('');
    const [customers, setCustomers] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!isOpen) {
            return undefined;
        }

        let isActive = true;
        const timer = setTimeout(() => {
            setLoading(true);
            setError('');

            apiClient.get('/budget/available-customers', {
                params: {
                    budgetId: budgetId || undefined,
                    search: search.trim() || undefined,
                },
            })
                .then((response) => {
                    if (isActive) {
                        setCustomers(Array.isArray(response.data) ? response.data : []);
                    }
                })
                .catch((requestError) => {
                    console.error('Failed to load available customers:', requestError);
                    if (isActive) {
                        setError('Kunde inte hämta kunder.');
                    }
                })
                .finally(() => {
                    if (isActive) {
                        setLoading(false);
                    }
                });
        }, SEARCH_DEBOUNCE_MS);

        return () => {
            isActive = false;
            clearTimeout(timer);
        };
    }, [budgetId, isOpen, search]);

    const excluded = new Set(excludedCustomerIds);
    const visibleCustomers = customers.filter((customer) => !excluded.has(customer.id));

    return (
        <BudgetModal
            isOpen={isOpen}
            title="LÄGG TILL KUND"
            onClose={onClose}
            confirmText=""
        >
            <LabeledInput
                label="Sök"
                labelWidth="w-12"
                value={search}
                onChange={setSearch}
                placeholder="Kundnamn"
                autoFocus
            />
            <div className="mt-3 max-h-80 overflow-y-auto border-t border-gray-300">
                {loading && <p className="py-3 text-center text-gray-500">Söker...</p>}
                {!loading && error && <p className="py-3 text-center text-rose-700">{error}</p>}
                {!loading && !error && visibleCustomers.length === 0 && (
                    <p className="py-3 text-center font-light text-gray-500">Inga kunder hittades</p>
                )}
                {!loading && !error && visibleCustomers.map((customer) => (
                    <button
                        key={customer.id}
                        type="button"
                        onClick={() => onSelect(customer)}
                        className="flex w-full items-center justify-between border-b border-gray-200 px-2 py-1.5 text-left hover:bg-lime-50"
                    >
                        <span>{customer.name}</span>
                        <span className="text-gray-500">{customer.responsibleUserName}</span>
                    </button>
                ))}
            </div>
        </BudgetModal>
    );
};

export default AddBudgetCustomerModal;
