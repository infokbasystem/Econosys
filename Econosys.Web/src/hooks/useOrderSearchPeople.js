import { useEffect, useMemo, useState } from 'react';
import apiClient from '../config/apiClient';
import { getSharedRequest } from '../helpers/sharedRequest';
import { buildOrderPersonFilters } from '../helpers/orderSearchFilters';

const useOrderSearchPeople = () => {
    const [users, setUsers] = useState([]);
    const [sellerIds, setSellerIds] = useState(null);
    const [creatorIds, setCreatorIds] = useState(null);
    const [error, setError] = useState('');

    useEffect(() => {
        let isActive = true;

        const loadUsers = async () => {
            try {
                const response = await getSharedRequest(
                    'customers:form-options',
                    () => apiClient.get('/customers/form-options'),
                );
                if (isActive) setUsers(response.data.users);
            } catch (error) {
                console.error('Failed to load order search person filters:', error);
                if (isActive) setError('Personfiltren kunde inte laddas.');
            }
        };

        loadUsers();
        return () => { isActive = false; };
    }, []);

    const options = useMemo(() => users.map((user) => ({ value: user.id, label: user.name })), [users]);
    const allIds = useMemo(() => options.map((option) => option.value), [options]);
    const personFilters = useMemo(
        () => buildOrderPersonFilters(sellerIds, creatorIds, options),
        [sellerIds, creatorIds, options],
    );

    return {
        options,
        sellerIds: sellerIds ?? allIds,
        creatorIds: creatorIds ?? allIds,
        setSellerIds,
        setCreatorIds,
        personFilters,
        error,
    };
};

export default useOrderSearchPeople;
