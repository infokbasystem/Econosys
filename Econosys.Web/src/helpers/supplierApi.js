import apiClient from '../config/apiClient'

export async function searchSuppliers({ searchTerm, pageNumber = 1, pageSize = 50 }) {
    const response = await apiClient.post('/suppliers/search', {
        searchTerm: searchTerm === '**' ? '' : searchTerm,
        pagination: {
            pageNumber,
            pageSize,
        },
    })

    return response?.data?.items ?? []
}