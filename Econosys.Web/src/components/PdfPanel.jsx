import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
// import EmailModal from './EmailModal';

import { usePdf } from '../contexts/PdfContext';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

export default function PdfPanel({ onOpenFileModal = null }) {
    const {
        showPdfPanel,
        pdfUrl,
        closePdfPreview,
        badges,
        openEmailModal,
        showEmailModal,
        emailSubject,
        emailBody,
        setEmailSubject,
        setEmailBody,
        closeEmailModal,
        isStale
    } = usePdf();
    const [numPages, setNumPages] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(false);
    const [visible, setVisible] = useState(false);
    const [showDoc, setShowDoc] = useState(false);
    const [showLocalModal, setShowLocalModal] = useState(false);
    const delayRef = useRef(null);

    const pdfOptions = useMemo(() => ({
        disableRange: true,
        disableStream: true
    }), []);

    const getPageNumbers = () => {
        if (!numPages || typeof numPages !== 'number' || numPages < 1) return [];
        return Array.from({ length: numPages }, (_, i) => i + 1);
    };

    // Keep panel visibility synced
    useEffect(() => {
        if (showPdfPanel) setVisible(true);
    }, [showPdfPanel]);

    // Sync visibility when panel is closed externally
    useEffect(() => {
        if (!showPdfPanel) setVisible(false);
    }, [showPdfPanel]);

    // Reset state on URL change, clear any pending delay
    useEffect(() => {
        setNumPages(null);
        setError(false);
        setShowDoc(false);
        if (pdfUrl) {
            setLoading(true);
            setVisible(true);
        }
        if (delayRef.current) {
            clearTimeout(delayRef.current);
            delayRef.current = null;
        }
        return () => {
            if (delayRef.current) {
                clearTimeout(delayRef.current);
                delayRef.current = null;
            }
        };
    }, [pdfUrl]);

    const handleClose = () => {
        setVisible(false);
        closePdfPreview?.();
    };

    const handleSendEmail = () => {
        const subject = encodeURIComponent(emailSubject || '');
        const body = encodeURIComponent(emailBody || '');
        window.open(`mailto:?subject=${subject}&body=${body}`, '_blank');
        closeEmailModal();
    };

    const handlePrint = () => {
        if (!pdfUrl) return;
        const win = window.open(pdfUrl, '_blank');
        if (win) {
            win.focus();
            win.onload = () => win.print();
        }
    };

    useEffect(() => {
        if (!showDoc) return;

        const canvases = document.querySelectorAll(
            '.react-pdf__Page canvas'
        );

        canvases.forEach((canvas) => {
            const ctx = canvas.getContext('2d');
            if (!ctx || ctx.__patched) return;

            const originalClear = ctx.clearRect.bind(ctx);
            ctx.clearRect = (...args) => {
                console.log('📉 PDF canvas cleared', args);
                return originalClear(...args);
            };

            ctx.__patched = true;
            console.log('🧠 Patched canvas', canvas);
        });
    }, [showDoc]);

    useEffect(() => {
        if (!showDoc) return;

        const observers = [];

        document
            .querySelectorAll('.react-pdf__Page canvas')
            .forEach((canvas) => {
                const ro = new ResizeObserver(() => {
                    console.log('📐 PDF canvas resized');
                });
                ro.observe(canvas);
                observers.push(ro);
            });

        return () => observers.forEach(o => o.disconnect());
    }, [showDoc]);


    return (
        <>
            <div
                className={`absolute right-0 w-[560px] bg-yellow-50 shadow-xl/30 z-50 pt-3 pl-3
                    transform transition-transform duration-300
                    ${visible ? 'translate-x-0 opacity-100 pointer-events-auto' : 'translate-x-full opacity-0 pointer-events-none'}`}
                style={{ top: 0, bottom: 0 }}
            >
                <div className="flex items-center p-3">
                    <button
                        type="button"
                        onClick={handleClose}
                        className="shadow-md/30 text-xs text-white bg-red-700 hover:bg-red-900 px-5 p-[5px] w-20">
                        Stäng
                    </button>
                    <button
                        type="button"
                        onClick={handlePrint}
                        disabled={isStale}
                        className={`shadow-md/30 text-xs text-gray bg-blue-200 px-4 py-[5px] ml-10 w-20 ${isStale ? 'opacity-50 cursor-not-allowed' : 'hover:bg-blue-300'}`}>
                        Skriv ut
                    </button>
                    <button
                        type="button"
                        onClick={openEmailModal}
                        disabled={isStale}
                        className={`shadow-md/30 text-xs text-gray bg-blue-200 px-4 py-[5px] ml-3 w-20 ${isStale ? 'opacity-50 cursor-not-allowed' : 'hover:bg-blue-300'}`}>
                        Maila
                    </button>
                    {/* <button
                        type="button"
                        onClick={openExampleModal}
                        className="shadow-md/30 text-xs text-gray bg-green-200 hover:bg-green-300 px-4 py-[5px] ml-3 w-20">
                        Info
                    </button>
                    <button
                        type="button"
                        onClick={() => setShowLocalModal(true)}
                        className="shadow-md/30 text-xs text-gray bg-purple-200 hover:bg-purple-300 px-4 py-[5px] ml-3 w-20">
                        Lokal
                    </button> */}
                </div>

                {Array.isArray(badges) && badges.length > 0 && (
                    <div className="flex items-center px-3 py-2">
                        {badges.map((b, i) => (
                            <span
                                key={i}
                                className="text-tiny px-2.5 py-1 rounded-full shadow-sm border border-gray-300"
                                style={{
                                    background: b?.color || '#e5e7eb',
                                    color: b?.color ? '#fff' : '#111'
                                }}
                            >
                                {b?.text ?? String(b)}
                            </span>
                        ))}
                    </div>
                )}

                <div className="relative ml-3 mr-5 mt-5 mb-3 overflow-auto" style={{ height: 'calc(100% - 100px)' }}>
                    {showPdfPanel && !pdfUrl ? null : null}

                    {pdfUrl && (
                        <Document
                            file={pdfUrl}
                            options={pdfOptions}
                            onLoadSuccess={({ numPages }) => {
                                setNumPages(numPages);
                                // Start 100ms delay after the Document is fully ready
                                if (delayRef.current) clearTimeout(delayRef.current);
                                delayRef.current = setTimeout(() => setShowDoc(true), 10);
                            }}
                            onLoadError={(err) => console.error('PDF load error:', err)}
                            loading={null}
                        >
                            {getPageNumbers().map((pageNumber) => (
                                <div
                                    key={`wrapper_${pageNumber}`}
                                    className=""
                                    style={{
                                        background: 'transparent',
                                        visibility: showDoc ? 'visible' : 'hidden'
                                    }}
                                >
                                    <Page
                                        key={`page_${pageNumber}`}
                                        pageNumber={pageNumber}
                                        width={500}
                                        renderTextLayer={false}
                                        renderAnnotationLayer={false}
                                    />
                                </div>
                            ))}
                        </Document>
                    )}

                    {isStale && (
                        <div className="absolute inset-0 flex items-center justify-center bg-gray-100/90 pointer-events-none">
                            <div className="text-center">
                                <p className="text-xs text-gray-600 font-semibold">PDF är inaktuell</p>
                                <p className="text-xs text-gray-500">Spara för att uppdatera</p>
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {/* {showEmailModal && (
                <EmailModal
                    subject={emailSubject}
                    body={emailBody}
                    onSubjectChange={setEmailSubject}
                    onBodyChange={setEmailBody}
                    onSend={handleSendEmail}
                    onClose={closeEmailModal}
                />
            )} */}
        </>
    );
}