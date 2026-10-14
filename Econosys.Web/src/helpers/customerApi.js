import apiClient from '../config/apiClient'

export async function searchCustomers({ searchTerm, pageNumber = 1, pageSize = 50 }) {
    const response = await apiClient.post('/customers/search', {
        searchTerm: searchTerm === '**' ? '' : searchTerm,
        pagination: {
            pageNumber,
            pageSize,
        },
    })

    return response?.data?.items ?? []
}