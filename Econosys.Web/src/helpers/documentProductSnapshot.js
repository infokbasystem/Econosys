export const mergeDocumentProductSnapshot = (document, type, response) => {
    if (!document) return document;
    const { productNoteExternal, ...snapshot } = response.saved;
    const external = type === 'quotations' || type === 'customerorders'
        ? { productMessage: productNoteExternal }
        : { productNoteExternal };
    const version = type === 'inquiries' || type === 'quotations'
        ? { editedAt: response.edited }
        : { edited: response.edited };
    return { ...document, ...snapshot, ...external, ...version };
};
const fields = ['product', 'material', 'materialThickness', 'construction', 'format', 'color',
    'varnish', 'printMethod', 'productNoteInternal', 'productNoteExternal'];

export const hasDocumentProductDifference = (saved, current) =>
    fields.some((field) => (saved?.[field] ?? '') !== (current?.[field] ?? ''));

export const canRefreshDocumentProduct = ({ id, changed, dirty, refreshing }) =>
    Boolean(id && changed && !dirty && !refreshing);

