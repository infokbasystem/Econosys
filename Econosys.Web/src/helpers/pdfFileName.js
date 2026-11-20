import apiClient from '../config/apiClient';
import { getFileNameFromContentDisposition } from './fileUtils';

// Legacy names depend on the customer's/supplier's language, which only the API knows reliably.
export const resolvePdfFileName = async (response, documentType, id, fallback) => {
    const headerName = getFileNameFromContentDisposition(response?.headers?.['content-disposition']);
    if (headerName) return headerName;

    try {
        const { data } = await apiClient.get(`/pdf/filename/${documentType}/${id}`);
        if (data?.fileName) return data.fileName;
    } catch (error) {
        console.error('Failed to resolve PDF file name:', error);
    }
    return fallback;
};
