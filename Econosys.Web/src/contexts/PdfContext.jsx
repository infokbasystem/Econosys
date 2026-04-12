import React, { createContext, useContext, useState, useCallback } from 'react';

const PdfContext = createContext(null);

export const PdfProvider = ({ children, initialBadges = [] }) => {
    const [showPdfPanel, setShowPdfPanel] = useState(false);
    const [pdfUrl, setPdfUrl] = useState('');
    const [badges, setBadges] = useState(initialBadges);
    const [showEmailModal, setShowEmailModal] = useState(false);
    const [emailSubject, setEmailSubject] = useState('');
    const [emailBody, setEmailBody] = useState('');
    const [isStale, setIsStale] = useState(false);

    const openPdfPreview = useCallback((url) => {
        setPdfUrl(url);
        setShowPdfPanel(true);
        setIsStale(false);
    }, []);

    const closePdfPreview = useCallback(() => 
        setShowPdfPanel(false), []
    );

    const markStale = useCallback(() => {
        setIsStale(true);
    }, []);

    const clearStale = useCallback(() => {
        setIsStale(false);
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
        badges,
        showEmailModal,
        emailSubject,
        emailBody,
        isStale,
        openPdfPreview,
        closePdfPreview,
        printPdf,
        openEmailModal: () => setShowEmailModal(true),
        closeEmailModal: () => setShowEmailModal(false),
        setEmailSubject,
        setEmailBody,
        markStale,
        clearStale,
        setBadges
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
