import React, { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import RichTextEditor from './RichTextEditor';
import apiClient from '../config/apiClient';
import useAppUpdateGuard from '../hooks/useAppUpdateGuard';

const createEmptyRecipient = () => ({ email: '', doSend: true, title: '' });

const normalizeRecipients = (items = []) => {
    const normalized = items.map((item) => ({
        email: item?.email || '',
        doSend: Boolean(item?.doSend),
        title: item?.title || '',
    }));

    if (normalized.length === 0 || normalized[normalized.length - 1].email.trim() !== '') {
        normalized.push(createEmptyRecipient());
    }

    return normalized;
};

const getAttachmentNameFromUrl = (url) => {
    if (!url) return '';
    try {
        const parsedUrl = new URL(url, window.location.origin);
        const parts = parsedUrl.pathname.split('/').filter(Boolean);
        return decodeURIComponent(parts[parts.length - 1] || 'attachment');
    } catch {
        return 'attachment';
    }
};

const ensurePdfFileName = (fileName) => {
    const baseName = (fileName || '').trim();
    if (!baseName) return 'document.pdf';
    return baseName.toLowerCase().endsWith('.pdf') ? baseName : `${baseName}.pdf`;
};

const createExtraAttachment = (file) => ({
    file,
    displayName: file?.name || 'attachment',
});

const RecipientTable = ({ title, rows, onChange }) => {
    const updateRow = (index, prop, value) => {
        const updatedRows = rows.map((row, rowIndex) => (
            rowIndex === index ? { ...row, [prop]: value } : row
        ));
        onChange(normalizeRecipients(updatedRows));
    };

    const removeRow = (index) => {
        const updatedRows = rows.filter((_, rowIndex) => rowIndex !== index);
        onChange(normalizeRecipients(updatedRows));
    };

    return (
        <div>
            {/* <div className="text-xs mb-2 text-center text-gray-700">{title}</div> */}
            <div className="overflow-hidden">
                <div className="grid grid-cols-[1fr_90px_60px] border-b border-gray-300 text-tiny text-gray-700">
                    <div className="px-2 py-1">{title}</div>
                    <div className="px-2 py-1 text-center">Skicka</div>
                    <div className="px-2 py-1 text-center">Ta bort</div>
                </div>
                {rows.map((row, index) => {
                    const isEmptyRow = row.email.trim() === '' && index === rows.length - 1;
                    return (
                        <div key={`${title}-${index}`} className="bg-white grid grid-cols-[1fr_90px_60px] border-b last:border-b-0 border-gray-200">
                            <div className="flex min-w-0 items-center">
                                <input
                                    className="w-full min-w-0 px-2 py-1.5 text-xs outline-none"
                                    value={row.email}
                                    onChange={(e) => updateRow(index, 'email', e.target.value)}
                                    placeholder="namn@foretag.se"
                                    aria-label={`${title} ${index + 1}`}
                                />
                                {row.title && (
                                    <span className="max-w-24 shrink-0 truncate px-2 text-xs text-gray-600" title={row.title}>
                                        {row.title}
                                    </span>
                                )}
                            </div>
                            <label className="flex items-center justify-center">
                                <input
                                    type="checkbox"
                                    checked={row.doSend}
                                    onChange={(e) => updateRow(index, 'doSend', e.target.checked)}
                                    aria-label={`Skicka till ${row.email || `${title} ${index + 1}`}`}
                                />
                            </label>
                            <div className="flex items-center justify-center">
                                {!isEmptyRow && (
                                    <button
                                        type="button"
                                        onClick={() => removeRow(index)}
                                        className="text-[11px] text-red-700 hover:text-red-900"
                                    >
                                        X
                                    </button>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default function EmailModal({
    initialSubject = '',
    initialBody = '',
    onClose,
    receiverList = [],
    ccMailList = [],
    recipientSource = null,
    sourceAttachmentUrl = '',
    sourceAttachmentName = '',
    inquiryId = null,
    quotationId = null,
    callOffId = null,
    transportOrderId = null,
    invoiceId = null,
    onSent,
}) {
    const [subject, setSubject] = useState(recipientSource ? '' : initialSubject);
    useAppUpdateGuard(true);
    const [body, setBody] = useState(recipientSource ? '' : initialBody);
    const initialTextRef = useRef({ subject: initialSubject, body: initialBody });
    initialTextRef.current = { subject: initialSubject, body: initialBody };
    const [receivers, setReceivers] = useState(() => normalizeRecipients(recipientSource ? [] : receiverList));
    const [ccRecipients, setCcRecipients] = useState(() => normalizeRecipients(recipientSource ? [] : ccMailList));
    const [extraFiles, setExtraFiles] = useState([]);
    const [isDragOverAttachments, setIsDragOverAttachments] = useState(false);
    const [isSending, setIsSending] = useState(false);
    const [emailSent, setEmailSent] = useState(false);
    const [sendError, setSendError] = useState('');
    const [sendErrorDetail, setSendErrorDetail] = useState('');
    const [isLoadingRecipients, setIsLoadingRecipients] = useState(Boolean(recipientSource));
    const [recipientError, setRecipientError] = useState('');
    const fileInputRef = useRef(null);

    useEffect(() => {
        if (recipientSource) return;
        setSubject(initialSubject || '');
    }, [initialSubject, recipientSource]);

    useEffect(() => {
        if (recipientSource) return;
        setBody(initialBody || '');
    }, [initialBody, recipientSource]);

    useEffect(() => {
        if (recipientSource) return;
        setReceivers(normalizeRecipients(receiverList));
    }, [receiverList, recipientSource]);

    useEffect(() => {
        if (recipientSource) return;
        setCcRecipients(normalizeRecipients(ccMailList));
    }, [ccMailList, recipientSource]);

    useEffect(() => {
        if (!recipientSource) return;
        const controller = new AbortController();
        setIsLoadingRecipients(true);
        setRecipientError('');
        const query = new URLSearchParams();
        if (recipientSource.receiverType) query.set('receiverType', recipientSource.receiverType);
        (recipientSource.receiverIds || []).forEach((id) => query.append('receiverIds', String(id)));

        apiClient.get(`/email/recipients/${recipientSource.itemType}/${recipientSource.itemId}?${query}`, {
            signal: controller.signal,
        }).then(({ data }) => {
            if (controller.signal.aborted) return;
            setReceivers(normalizeRecipients(data.recipients));
            setCcRecipients(normalizeRecipients(data.ccRecipients));
            setSubject(data.subject || initialTextRef.current.subject || '');
            setBody(data.body || initialTextRef.current.body || '');
        }).catch((error) => {
            if (controller.signal.aborted) return;
            setSubject(initialTextRef.current.subject || '');
            setBody(initialTextRef.current.body || '');
            console.error('Failed to load email recipients:', error);
            setRecipientError(error.response?.data?.detail || 'Kunde inte ladda e-postmottagare. Stäng och öppna dialogen för att försöka igen.');
        }).finally(() => {
            if (!controller.signal.aborted) setIsLoadingRecipients(false);
        });

        return () => controller.abort();
    }, [recipientSource]);

    const attachmentName = useMemo(
        () => ensurePdfFileName(sourceAttachmentName || getAttachmentNameFromUrl(sourceAttachmentUrl)),
        [sourceAttachmentName, sourceAttachmentUrl]
    );

    const appendUniqueFiles = (incomingFiles) => {
        const files = Array.from(incomingFiles || []);
        if (files.length === 0) return;

        setExtraFiles((currentFiles) => {
            const existingKeys = new Set(
                currentFiles.map((item) => `${item.file?.name}-${item.file?.size}-${item.file?.lastModified}`)
            );
            const uniqueFiles = files.filter((file) => !existingKeys.has(`${file.name}-${file.size}-${file.lastModified}`));
            return [...currentFiles, ...uniqueFiles.map(createExtraAttachment)];
        });
    };

    const handleExtraFileNameChange = (indexToUpdate, newName) => {
        setExtraFiles((currentFiles) => currentFiles.map((item, index) => (
            index === indexToUpdate
                ? { ...item, displayName: newName }
                : item
        )));
    };

    const handleExtraFilesSelected = (event) => {
        appendUniqueFiles(event.target.files || []);

        event.target.value = '';
    };

    const handleAttachmentDragOver = (event) => {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
        setIsDragOverAttachments(true);
    };

    const handleAttachmentDragLeave = (event) => {
        event.preventDefault();
        if (event.currentTarget.contains(event.relatedTarget)) return;
        setIsDragOverAttachments(false);
    };

    const handleAttachmentDrop = (event) => {
        event.preventDefault();
        setIsDragOverAttachments(false);

        const droppedFromItems = Array.from(event.dataTransfer?.items || [])
            .filter((item) => item.kind === 'file')
            .map((item) => item.getAsFile())
            .filter(Boolean);

        const droppedFiles = droppedFromItems.length > 0
            ? droppedFromItems
            : Array.from(event.dataTransfer?.files || []);

        appendUniqueFiles(droppedFiles);
    };

    const handleAttachmentPaste = (event) => {
        const pastedFromItems = Array.from(event.clipboardData?.items || [])
            .filter((item) => item.kind === 'file')
            .map((item) => item.getAsFile())
            .filter(Boolean);

        const pastedFiles = pastedFromItems.length > 0
            ? pastedFromItems
            : Array.from(event.clipboardData?.files || []);

        if (pastedFiles.length === 0) return;

        event.preventDefault();
        appendUniqueFiles(pastedFiles);
    };

    const removeExtraFile = (indexToRemove) => {
        setExtraFiles((currentFiles) => currentFiles.filter((_, index) => index !== indexToRemove));
    };

    const filteredReceivers = receivers.filter((item) => item.email.trim() !== '');
    const filteredCcRecipients = ccRecipients.filter((item) => item.email.trim() !== '');
    const hasNoRecipients = !filteredReceivers.some((item) => item.doSend);

    const handleSubjectChange = (event) => {
        setSubject(event.target.value);
    };

    const handleBodyChange = (html) => {
        setBody(html);
    };

    const handleBackdropClick = (event) => {
        if (event.target === event.currentTarget && !isSending) {
            onClose?.();
        }
    };

    const handleSendClick = async () => {
        if (isLoadingRecipients || recipientError) return;
        setIsSending(true);
        setSendError('');
        setSendErrorDetail('');
        let wasSent = false;
        const payload = {
            subject,
            body,
            receivers: filteredReceivers,
            ccRecipients: filteredCcRecipients,
            sourceAttachmentUrl,
            sourceAttachmentName: attachmentName,
            inquiryId,
            quotationId,
            callOffId,
            transportOrderId,
            invoiceId,
            extraFiles,
        };

        try {
            const formData = new FormData();
            formData.append('subject', payload.subject || '');
            formData.append('body', payload.body || '');
            formData.append('receivers', JSON.stringify(payload.receivers || []));
            formData.append('ccRecipients', JSON.stringify(payload.ccRecipients || []));
            if (Number.isInteger(payload.inquiryId)) {
                formData.append('inquiryId', String(payload.inquiryId));
            }
            if (Number.isInteger(payload.quotationId)) {
                formData.append('quotationId', String(payload.quotationId));
            }
            if (Number.isInteger(payload.callOffId)) {
                formData.append('callOffId', String(payload.callOffId));
            }
            if (Number.isInteger(payload.transportOrderId)) {
                formData.append('transportOrderId', String(payload.transportOrderId));
            }
            if (Number.isInteger(payload.invoiceId)) {
                formData.append('invoiceId', String(payload.invoiceId));
            }

            if (payload.sourceAttachmentUrl) {
                const { data: sourceBlob } = await apiClient.get(payload.sourceAttachmentUrl, {
                    responseType: 'blob',
                    baseURL: payload.sourceAttachmentUrl.startsWith('blob:') ? '' : apiClient.defaults.baseURL,
                });
                const sourceFileName = ensurePdfFileName(payload.sourceAttachmentName);
                formData.append('files', sourceBlob, sourceFileName);
            }

            (payload.extraFiles || []).forEach((item) => {
                if (!item?.file) return;
                const editedName = (item.displayName || '').trim();
                const fileNameToUse = editedName || item.file.name || 'attachment';
                formData.append('files', item.file, fileNameToUse);
            });

            await apiClient.post('/email/send', formData, {
                headers: { 'Content-Type': 'multipart/form-data' },
            });

            wasSent = true;
            await onSent?.(payload);
            onClose?.();
        } catch (error) {
            if (!wasSent && error.response?.data?.emailSent === true) {
                console.error('Email was sent but its log could not be saved:', error);
                let message = error.response.data.message
                    || 'E-postmeddelandet skickades, men kunde inte sparas i mail-loggen. Skicka inte om meddelandet.';
                try {
                    await onSent?.(payload);
                } catch (updateError) {
                    console.error('Error updating after sending email:', updateError);
                    message += ' Statusuppdateringen misslyckades också.';
                }
                setEmailSent(true);
                setSendError(message);
            } else if (wasSent) {
                console.error('Error updating after sending email:', error);
                setEmailSent(true);
                setSendError('E-postmeddelandet skickades, men uppdateringen misslyckades. Stäng och kontrollera uppgifterna.');
            } else {
                console.error('Error sending email:', error);
                setSendError(error.response?.data?.message || (typeof error.response?.data === 'string' ? error.response.data : '') || error.message || 'Kunde inte skicka e-post');
                setSendErrorDetail(typeof error.response?.data?.detail === 'string' ? error.response.data.detail : '');
            }
        } finally {
            setIsSending(false);
        }
    };

    return createPortal(
        <div className="fixed inset-0 z-50">

            <div
                className="absolute inset-0 bg-black/50 z-40"
            />

            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20">

                <div className="rounded-sm shadow-xl py-6 w-[92%] max-w-5xl max-h-[min(92vh,calc(100vh-6rem))] overflow-y-auto bg-[#f4f3f2]">

                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold text-center">Skicka e-post</h2>
                        <button
                            onClick={handleBackdropClick}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1 mr-5"
                        >
                            ×
                        </button>
                    </div>

                    <div className="grid grid-cols-1 lg:grid-cols-[0.8fr_1.1fr] gap-10 mx-10 mt-7">
                        <div>
                            {isLoadingRecipients && <div role="status" className="mb-3 text-xs text-gray-700">Laddar e-postmottagare...</div>}
                            {recipientError && <div role="alert" className="mb-3 text-xs text-red-700">{recipientError}</div>}
                            <fieldset disabled={isLoadingRecipients || Boolean(recipientError) || isSending || emailSent}>
                                <RecipientTable title="Mottagare" rows={receivers} onChange={setReceivers} />
                                <div className="mt-6">
                                    <RecipientTable title="Kopia till" rows={ccRecipients} onChange={setCcRecipients} />
                                </div>
                            </fieldset>

                            <div
                                className={`mt-4 border rounded-sm p-3 bg-gray-50 transition-colors ${isDragOverAttachments ? 'border-blue-500 bg-blue-50' : 'border-gray-300'}`}
                                onDragOver={handleAttachmentDragOver}
                                onDragEnter={handleAttachmentDragOver}
                                onDragLeave={handleAttachmentDragLeave}
                                onDrop={handleAttachmentDrop}
                                onPaste={handleAttachmentPaste}
                                tabIndex={0}
                            >
                                <div className="text-xs font-semibold mb-2">Bilagor</div>
                                <div className="text-xs text-gray-700 mb-2">
                                    Ursprunglig fil:
                                    <span className="ml-2 font-medium">{attachmentName || 'Ingen fil vald'}</span>
                                </div>
                                {/* {sourceAttachmentUrl && (
                                    <div className="text-[11px] text-gray-500 break-all mb-3">{sourceAttachmentUrl}</div>
                                )} */}
                                <div className="flex items-center gap-2 mb-3">
                                    <button
                                        type="button"
                                        onClick={() => fileInputRef.current?.click()}
                                        className="text-xs px-3 py-1 rounded border border-gray-300 hover:bg-gray-100"
                                    >
                                        Lägg till filer
                                    </button>
                                    <span className="text-[11px] text-gray-500">Dra filer hit, klistra in med Cmd+V, eller klicka på knappen</span>
                                    <input
                                        ref={fileInputRef}
                                        type="file"
                                        multiple
                                        className="hidden"
                                        onChange={handleExtraFilesSelected}
                                    />
                                </div>
                                {extraFiles.length === 0 ? (
                                    <div className="text-xs text-gray-500">Inga extra filer valda</div>
                                ) : (
                                    <ul className="space-y-2">
                                        {extraFiles.map((item, index) => (
                                            <li key={`${item.file?.name}-${item.file?.size}-${index}`} className="flex items-center gap-2 text-xs bg-white border border-gray-200 rounded px-2 py-1">
                                                <input
                                                    type="text"
                                                    value={item.displayName}
                                                    onChange={(e) => handleExtraFileNameChange(index, e.target.value)}
                                                    className="flex-1 min-w-0 border border-gray-300 rounded px-2 py-1 text-xs outline-none"
                                                    placeholder={item.file?.name || 'Filnamn'}
                                                />
                                                <button
                                                    type="button"
                                                    onClick={() => removeExtraFile(index)}
                                                    className="text-red-700 hover:text-red-900"
                                                >
                                                    Ta bort
                                                </button>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs mb-1 text-center text-gray-700">Ämne</label>
                            <input
                                className="w-full bg-white border border-gray-300 rounded-sm px-2 py-1 focus:outline-none text-xs mb-3"
                                value={subject}
                                onChange={handleSubjectChange}
                                placeholder="Ämne"
                            />
                            <label className="block text-xs mb-1 text-center text-gray-700">Meddelande</label>
                            <RichTextEditor
                                value={body}
                                onChange={handleBodyChange}
                                placeholder="Meddelande"
                                height="min-h-72 max-h-[28rem]"
                            />
                        </div>
                    </div>
                    <div className="flex gap-4 mt-6 mb-3 pt-4 mx-10 justify-end">
                        {!isLoadingRecipients && !recipientError && hasNoRecipients && (
                            <div className="text-xs text-amber-700 bg-amber-50 border border-gray-300 rounded px-10 py-1">
                                Välj minst en mottagare.
                            </div>
                        )}
                        {sendError && (
                            <div className="text-xs text-red-700 bg-red-50 border border-gray-300 rounded px-10 py-1">
                                {sendError}
                                {sendErrorDetail && <div className="mt-1">{sendErrorDetail}</div>}
                            </div>
                        )}
                        <button
                            type="button"
                            disabled={isSending}
                            onClick={onClose}
                            className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {emailSent ? 'Stäng' : 'Avbryt'}
                        </button>
                        <button
                            type="button"
                            onClick={handleSendClick}
                            disabled={isSending || emailSent || hasNoRecipients || isLoadingRecipients || Boolean(recipientError)}
                            className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-10 p-[5px] disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {isSending ? 'Skickar...' : 'Skicka'}
                        </button>
                    </div>
                </div>

            </div>


        </div>,
        document.body
    );
}
