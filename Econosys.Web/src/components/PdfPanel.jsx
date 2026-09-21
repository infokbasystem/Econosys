import React, { useState, useEffect, useRef, useMemo } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { Mail, Printer, X } from 'lucide-react';
import EmailModal from './EmailModal';
import ActionButton from './ActionButton';

import { usePdf } from '../contexts/PdfContext';

pdfjs.GlobalWorkerOptions.workerSrc = `//unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;

export default function PdfPanel({ onOpenFileModal = null }) {
    const {
        showPdfPanel,
        pdfUrl,
        pdfFileName,
        closePdfPreview,
        badges,
        emailInfo,
        openEmailModal,
        showEmailModal,
        closeEmailModal,
        isStale
    } = usePdf();
    const [numPages, setNumPages] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(false);
    const [visible, setVisible] = useState(false);
    const [mounted, setMounted] = useState(false);
    const [showDoc, setShowDoc] = useState(false);
    const [showLocalModal, setShowLocalModal] = useState(false);
    const delayRef = useRef(null);
    const unmountTimerRef = useRef(null);

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
        if (!showPdfPanel) return;

        setMounted(true);
        const frameId = requestAnimationFrame(() => setVisible(true));

        return () => cancelAnimationFrame(frameId);
    }, [showPdfPanel]);

    // Start the exit transition when the preview is closed externally.
    useEffect(() => {
        if (showPdfPanel) return undefined;

        setVisible(false);
        unmountTimerRef.current = setTimeout(() => setMounted(false), 500);

        return () => {
            if (unmountTimerRef.current) {
                clearTimeout(unmountTimerRef.current);
                unmountTimerRef.current = null;
            }
        };
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

    const getPdfFileName = () => {
        if (pdfFileName) return pdfFileName;
        if (!pdfUrl) return 'document.pdf';
        try {
            const parsedUrl = new URL(pdfUrl, window.location.origin);
            const segments = parsedUrl.pathname.split('/').filter(Boolean);
            return decodeURIComponent(segments[segments.length - 1] || 'document.pdf');
        } catch {
            return 'document.pdf';
        }
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
                className={`absolute right-0 w-[560px] overflow-hidden border-l border-slate-200/80 bg-yellow-50/95 shadow-2xl z-50 pl-7 pr-10 py-10 backdrop-blur-sm
                    transform-gpu transition-[translate,scale,opacity] duration-500 ease-[cubic-bezier(0.22,1,0.36,1)] will-change-transform
                    ${!mounted ? 'hidden' : visible ? 'translate-x-0 scale-100 opacity-100 pointer-events-auto' : 'translate-x-full scale-[0.985] opacity-0 pointer-events-none'}`}
                aria-hidden={!visible}
                style={{ top: 0, bottom: 0 }}
            >
                <div className="flex items-center pb-3 pl-5">
                    <div className="flex items-center gap-6">
                        <ActionButton
                            label="Stäng"
                            icon={X}
                            onClick={handleClose}
                            accent="rose"
                        />
                        <ActionButton
                            label="Skriv ut"
                            icon={Printer}
                            onClick={handlePrint}
                            disabled={isStale}
                            accent="sky"
                        />
                        <ActionButton
                            label="Maila"
                            icon={Mail}
                            onClick={openEmailModal}
                            disabled={isStale}
                            accent="sky"
                        />
                    </div>
                </div>

                <div className="relative">
                    
                    {/* {Array.isArray(badges) && badges.length > 0 && (
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
                    )} */}

                    <div className="pb-3 overflow-auto" style={{ height: 'calc(100% - 100px)' }}>
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
                                        className="my-3 max-w-full shadow-lg border border-gray-200"
                                        style={{
                                            background: 'transparent',
                                            visibility: showDoc ? 'visible' : 'hidden'
                                        }}
                                    >
                                        <Page
                                            key={`page_${pageNumber}`}
                                            pageNumber={pageNumber}
                                            width={480}
                                            renderTextLayer={false}
                                            renderAnnotationLayer={false}
                                        />
                                    </div>
                                ))}
                            </Document>
                        )}
                    </div>

                    {isStale && (
                        <div className="absolute inset-0 flex items-center justify-center bg-gray-100/90 pointer-events-auto">
                            <div className="text-center">
                                <p className="text-xs text-gray-600 font-semibold">PDF är inaktuell</p>
                                <p className="text-xs text-gray-500">Spara för att uppdatera</p>
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {showEmailModal && (
                <EmailModal
                    initialSubject={emailInfo?.subject || ''}
                    initialBody={emailInfo?.body || ''}
                    receiverList={emailInfo?.recipients || []}
                    ccMailList={emailInfo?.ccRecipients || []}
                    sourceAttachmentUrl={pdfUrl}
                    sourceAttachmentName={getPdfFileName()}
                    inquiryId={emailInfo?.inquiryId ?? null}
                    quotationId={emailInfo?.quotationId ?? null}
                    onClose={closeEmailModal}
                />
            )}
        </>
    );
}