import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import apiClient from '../config/apiClient';
import { formatDateTime } from '../helpers/dateUtils';

export default function MailLog({ itemType, itemId, refreshKey }) {
    const [entries, setEntries] = useState(null);
    const [error, setError] = useState('');
    const [retry, setRetry] = useState(0);
    const [isOpen, setIsOpen] = useState(false);
    const triggerRef = useRef(null);

    useEffect(() => {
        let active = true;
        const controller = new AbortController();

        apiClient.get(`/email/log/${itemType}/${itemId}`, { signal: controller.signal })
            .then((response) => {
                if (!Array.isArray(response.data)) {
                    throw new Error('Invalid mail log response.');
                }
                if (active) {
                    setEntries(response.data);
                    setError('');
                }
            })
            .catch((requestError) => {
                if (!active) return;
                console.error('Failed to load mail log:', requestError);
                setError('Kunde inte hämta mail-loggen.');
            });

        return () => {
            active = false;
            controller.abort();
        };
    }, [itemType, itemId, refreshKey, retry]);

    const closeModal = useCallback(() => {
        setIsOpen(false);
        triggerRef.current?.focus();
    }, []);

    const openModal = () => {
        setIsOpen(true);
        setRetry((value) => value + 1);
    };

    return (
        <div className="flex flex-wrap items-center gap-2">
            {entries === null && !error && (
                <span role="status" className="text-xs text-gray-500">Hämtar mail-logg…</span>
            )}
            {entries?.length > 0 && (
                <>
                    <span className="rounded-full border border-red-200 bg-red-50 px-3 py-1 text-xs font-medium text-red-700">
                        Mail skickat
                    </span>
                    <button
                        ref={triggerRef}
                        type="button"
                        onClick={openModal}
                        className="text-xs text-gray-700 underline hover:text-gray-900"
                    >
                        Visa logg
                    </button>
                </>
            )}
            {error && (
                <span role="alert" className="text-xs text-red-700">
                    {error}{' '}
                    <button type="button" className="underline" onClick={() => setRetry((value) => value + 1)}>
                        Försök igen
                    </button>
                </span>
            )}

            {isOpen && createPortal(
                <div
                    className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4"
                    onMouseDown={(event) => {
                        if (event.target === event.currentTarget) closeModal();
                    }}
                >
                    <section
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="mail-log-title"
                        className="flex max-h-[85vh] min-h-[350px] w-full max-w-4xl flex-col rounded-sm border-2 border-[#fafad2] bg-[#ffffe0] p-6 shadow-xl"
                        onKeyDown={(event) => {
                            if (event.key === 'Escape') {
                                event.stopPropagation();
                                closeModal();
                            } else if (event.key === 'Tab') {
                                const buttons = event.currentTarget.querySelectorAll('button');
                                const first = buttons[0];
                                const last = buttons[buttons.length - 1];
                                if (event.shiftKey && document.activeElement === first) {
                                    event.preventDefault();
                                    last?.focus();
                                } else if (!event.shiftKey && document.activeElement === last) {
                                    event.preventDefault();
                                    first?.focus();
                                }
                            }
                        }}
                    >
                        <div className="relative mb-6 flex items-center justify-center">
                            <h2 id="mail-log-title" className="text-sm font-semibold">Mail-logg</h2>
                            <button
                                type="button"
                                autoFocus
                                onClick={closeModal}
                                aria-label="Stäng mail-logg"
                                className="absolute right-0 rounded p-1 text-red-700 hover:bg-red-100"
                            >
                                <X size={16} />
                            </button>
                        </div>
                        {error && <p role="alert" className="mb-3 text-xs text-red-700">{error}</p>}
                        <div className="min-h-0 flex-1 overflow-auto">
                            <table className="w-full min-w-[600px] text-left text-xs">
                                <thead className="border-t border-gray-400 text-tiny uppercase text-gray-600">
                                    <tr>
                                        <th scope="col" className="px-2 py-1.5 font-medium">Datum</th>
                                        <th scope="col" className="px-2 py-1.5 font-medium">Mottagare</th>
                                        <th scope="col" className="px-2 py-1.5 font-medium">Info</th>
                                        <th scope="col" className="px-2 py-1.5 font-medium">Info<span className="sr-only"> (avsändare)</span></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries?.length === 0 ? (
                                        <tr><td colSpan={4} className="px-2 py-4 text-center text-gray-500">Ingen mail-logg att visa.</td></tr>
                                    ) : entries?.map((entry) => (
                                        <tr key={entry.id} className="align-top">
                                            <td className="whitespace-nowrap px-2 py-1">{formatDateTime(entry.dateTime)}</td>
                                            <td className="max-w-64 break-words px-2 py-1">{entry.recipients}</td>
                                            <td className="max-w-64 break-words px-2 py-1">{entry.subject}</td>
                                            <td className="whitespace-nowrap px-2 py-1">{entry.employeeName}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                        <div className="mt-6 flex justify-end">
                            <button
                                type="button"
                                onClick={closeModal}
                                className="bg-orange-400 px-10 py-1.5 text-xs text-white shadow-md hover:bg-orange-600"
                            >
                                Stäng
                            </button>
                        </div>
                    </section>
                </div>,
                document.body,
            )}
        </div>
    );
}
