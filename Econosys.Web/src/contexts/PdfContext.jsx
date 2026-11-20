import React, { createContext, useContext, useState, useCallback } from 'react';

const PdfContext = createContext(null);

const DEFAULT_EMAIL_RECIPIENTS = [{ email: '', doSend: true }];
const DEFAULT_EMAIL_CC_RECIPIENTS = [{ email: '', doSend: true }];

const createDefaultEmailInfo = () => ({
    recipients: DEFAULT_EMAIL_RECIPIENTS.map((item) => ({ ...item })),
    ccRecipients: DEFAULT_EMAIL_CC_RECIPIENTS.map((item) => ({ ...item })),
    subject: '',
    body: '',
    inquiryId: null,
    quotationId: null,
});

export const PdfProvider = ({ children, initialBadges = [] }) => {
    const [showPdfPanel, setShowPdfPanel] = useState(false);
    const [pdfUrl, setPdfUrl] = useState('');
    const [pdfFileName, setPdfFileName] = useState('');
    const [badges, setBadges] = useState(initialBadges);
    const [showEmailModal, setShowEmailModal] = useState(false);
    const [isStale, setIsStale] = useState(false);
    const [emailInfo, setEmailInfoState] = useState(() => createDefaultEmailInfo());
    const [onEmailSent, setOnEmailSent] = useState(null);
    const [mailLogItem, setMailLogItem] = useState(null);
    const [emailRecipientSource, setEmailRecipientSource] = useState(null);

    const openPdfPreview = useCallback((url, fileName = '', onSent = null, logItem = null, recipientSource = null) => {
        setPdfUrl(url);
        setPdfFileName(fileName || '');
        setOnEmailSent(() => onSent);
        setMailLogItem(logItem);
        setShowEmailModal(false);
        setEmailRecipientSource(recipientSource);
        if (url) {
            setEmailInfoState((current) => {
                const isSameInquiry = recipientSource?.itemType === 'INQUIRY' && current.inquiryId === recipientSource.itemId;
                const isSameQuotation = recipientSource?.itemType === 'QUOTATION' && current.quotationId === recipientSource.itemId;
                return isSameInquiry || isSameQuotation ? current : createDefaultEmailInfo();
            });
        }
        setShowPdfPanel(true);
        setIsStale(false);
    }, []);

    const closePdfPreview = useCallback(() => {
        setShowPdfPanel(false);
        setShowEmailModal(false);
        setOnEmailSent(null);
        setMailLogItem(null);
        setEmailRecipientSource(null);
    }, []);

    const markStale = useCallback(() => {
        setIsStale(true);
    }, []);

    const clearStale = useCallback(() => {
        setIsStale(false);
    }, []);

    const setEmailInfo = useCallback((nextEmailInfo = {}) => {
        setEmailInfoState({
            recipients: Array.isArray(nextEmailInfo.recipients)
                ? nextEmailInfo.recipients.map((item) => ({
                    email: item?.email || '',
                    doSend: Boolean(item?.doSend),
                    title: item?.title || '',
                }))
                : createDefaultEmailInfo().recipients,
            ccRecipients: Array.isArray(nextEmailInfo.ccRecipients)
                ? nextEmailInfo.ccRecipients.map((item) => ({
                    email: item?.email || '',
                    doSend: Boolean(item?.doSend),
                    title: item?.title || '',
                }))
                : createDefaultEmailInfo().ccRecipients,
            subject: nextEmailInfo.subject || '',
            body: nextEmailInfo.body || '',
            inquiryId: Number.isInteger(nextEmailInfo.inquiryId) ? nextEmailInfo.inquiryId : null,
            quotationId: Number.isInteger(nextEmailInfo.quotationId) ? nextEmailInfo.quotationId : null,
        });
    }, []);

    const clearEmailInfo = useCallback(() => {
        setEmailInfoState(createDefaultEmailInfo());
    }, []);

    const printPdf = () => {
        if (!pdfUrl) return;
        const link = document.createElement('a');
        link.href = pdfUrl;
        link.download = 'inquiry.pdf';
        link.click();
    };

    const value = {
        showPdfPanel,
        pdfUrl,
        pdfFileName,
        badges,
        showEmailModal,
        isStale,
        emailInfo,
        onEmailSent,
        mailLogItem,
        emailRecipientSource,
        openPdfPreview,
        closePdfPreview,
        printPdf,
        openEmailModal: () => setShowEmailModal(true),
        closeEmailModal: () => setShowEmailModal(false),
        markStale,
        clearStale,
        setBadges,
        setEmailInfo,
        clearEmailInfo,
    };

    return (
        <PdfContext.Provider value={value}>
            {children}
        </PdfContext.Provider>
    );
};

export const usePdf = () => {
    const ctx = useContext(PdfContext);
    if (!ctx) throw new Error('usePdf must be used within PdfProvider');
    return ctx;
};
