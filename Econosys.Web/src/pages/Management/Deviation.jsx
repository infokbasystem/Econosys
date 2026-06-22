import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useLocation, useBlocker } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import { usePdf } from '../../contexts/PdfContext';
import ConfirmationModal from '../../components/ConfirmationModal';
import FileList from '../../components/FileList';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import NumberInput from '../../components/NumberInput';
import SwitchSelector from 'react-switch-selector';

import apiClient from '../../config/apiClient';
import { formatDateShort, formatDateTime, fromDateInputToSwedishIso, toSwedishDateInputValue } from '../../helpers/dateUtils';
import { getFileNameFromContentDisposition } from '../../helpers/fileUtils';
import { formatNumber, parseNullableInt, parseNullableNumber } from '../../helpers/numberUtils';
import OrderSearchModal from '../../components/OrderSearchModal';

const statusOptions = [
    {
        label: <span className='text-tiny'>Öppen</span>,
        value: 'OPEN',
        id: 'OPEN',
        index: 0,
        selectedBackgroundColor: '#16a34a',
        fontColor: '#374151',
    },
    {
        label: <span className='text-tiny'>Stängd</span>,
        value: 'CLOSED',
        id: 'CLOSED',
        index: 1,
        selectedBackgroundColor: '#ef4444',
    },
];

const createNewDeviationModel = () => ({
    id: 0,
    isInternal: false,
    status: 'OPEN',
    costs: [],
});

const Deviation = () => {
    const navigate = useNavigate();
    const { openPdfPreview, setBadges, setEmailInfo, markStale, clearStale, showPdfPanel, closePdfPreview } = usePdf();

    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedWarningReason, setUnsavedWarningReason] = useState('navigate'); // 'navigate' | 'print'
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showLinkOrder, setShowLinkOrder] = useState(false);
    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [attachedFiles, setAttachedFiles] = useState([]);

    const { id } = useParams();
    const isNewDeviation = id === 'new';
    const [deviation, setDeviation] = useState(null);
    const [originalDeviation, setOriginalDeviation] = useState(null);
    const skipUnsavedCheckRef = useRef(false);

    const [suppliers, setSuppliers] = useState([]);
    const [customers, setCustomers] = useState([]);
    const [users, setUsers] = useState([]);
    const [currencies, setCurrencies] = useState([]);
    const [deviationProcesses, setDeviationProcesses] = useState([]);
    const [deviationTypes, setDeviationTypes] = useState([]);
    const [deviationDecisions, setDeviationDecisions] = useState([]);

    const defaultBadges = [
        { text: 'Epostadress finns', color: '#56983cff' }
    ];

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }
        window.close();
    };

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!deviation || !originalDeviation) return false;
        return JSON.stringify(deviation) !== JSON.stringify(originalDeviation);
    }, [deviation, originalDeviation]);

    const blocker = useBlocker(hasUnsavedChanges);

    // Cleanup: close PDF panel when component unmounts (navigating away)
    useEffect(() => {
        return () => {
            closePdfPreview?.();
        };
    }, [closePdfPreview]);

    const getPdf = async ({ ignoreUnsaved = false } = {}) => {
        if (!deviation) return;

        if (!ignoreUnsaved && hasUnsavedChanges()) {
            setUnsavedWarningReason('print');
            setShowUnsavedWarning(true);
            return;
        }

        // Open the PDF panel immediately
        openPdfPreview('');

        // await fetchInquiryEmailInfo(inquiry.supplierId);

        try {
            const res = await apiClient.get(`/pdf/deviation/${id}`, {
                responseType: 'blob'
            });
            const contentDisposition = res.headers['content-disposition'];
            const apiFileName = getFileNameFromContentDisposition(contentDisposition);
            const blob = res.data;
            const url = URL.createObjectURL(blob);
            openPdfPreview(url, apiFileName); // update the panel with the real PDF and file name from API
        } catch (error) {
            console.error('Error getting deviation PDF:', error);
            // Optional: toast.error('Kunde inte ladda PDF');
            // Optional: keep panel open to show an error state in PdfPanel
        }
    };

    // Native beforeunload warning for tab close if unsaved changes
    useEffect(() => {
        const handleBeforeUnload = (e) => {
            if (hasUnsavedChanges()) {
                e.preventDefault();
                e.returnValue = '';
                return '';
            }
        };
        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => {
            window.removeEventListener('beforeunload', handleBeforeUnload);
        };
    }, [hasUnsavedChanges]);

    // Show custom modal when navigation is blocked
    useEffect(() => {
        if (blocker.state === 'blocked') {
            setUnsavedWarningReason('navigate');
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [id]);

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        blocker.reset();
    };

    const handleAddFile = (uploadedData) => {
        setAttachedFiles(prev => [...prev, uploadedData]);
    };

    const handleEditFile = (updatedData) => {
        setAttachedFiles(prev => prev.map(f => {
            if (f.id === updatedData.id) {
                return { ...f, ...updatedData };
            }
            return f;
        }));
    };

    const handleRemoveFile = async (fileId) => {
        const fileToRemove = attachedFiles.find(f => f.id === fileId);

        if (!fileToRemove) return;

        // If the file has a real server ID, delete it from the server
        if (fileToRemove.id) {
            try {
                const token = localStorage.getItem('token');
                const res = await fetch(`${apiUrl}/attachment/${fileToRemove.id}`, {
                    method: 'DELETE',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                    }
                });

                if (!res.ok) {
                    throw new Error('Failed to delete file');
                }

                // Remove from local state
                setAttachedFiles(prev => prev.filter(f => f.id !== fileId));

            } catch (error) {
                console.error('Error deleting file:', error);
                alert('Kunde inte ta bort filen');
            }
        } else {
            // For files not yet uploaded, just remove from local state
            setAttachedFiles(prev => prev.filter(f => f.id !== fileId));
        }
    };

    const handleSave = async () => {
        if (!deviation) return;

        try {
            const payload = buildDeviationPayload(deviation);
            const deviationId = parseNullableInt(deviation?.id);
            const isCreatingNew = !(deviationId && deviationId > 0);

            const response = deviationId && deviationId > 0
                ? await apiClient.put(`/deviations/${deviationId}`, payload)
                : await apiClient.post('/deviations', payload);

            const savedDeviation = response.data;
            const attachments = (
                Array.isArray(savedDeviation?.attachedFiles)
                    ? savedDeviation.attachedFiles
                    : []
            ).map(att => ({
                ...att,
                url: att.path ?? ''
            }));

            setDeviation(savedDeviation ?? null);
            setOriginalDeviation(structuredClone(savedDeviation));
            setAttachedFiles(attachments.map(f => ({ ...f })));
            clearStale();
            setMessages(prev => [
                ...prev.filter(msg => msg.type !== 'success'),
                { type: 'success', text: 'Avvikelsen sparades.' }
            ]);

            if (isCreatingNew && savedDeviation?.id) {
                skipUnsavedCheckRef.current = true;
                navigate(`/management/deviations/${savedDeviation.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save deviation:', error);
            setMessages(prev => [
                ...prev.filter(msg => msg.type !== 'error'),
                { type: 'error', text: 'Kunde inte spara avvikelsen.' }
            ]);
        }
    };

    const handleDelete = async () => {
        setShowDeleteConfirm(false);
        try {
            await apiClient.delete(`/deviations/${deviation.id}`);
            navigate('/management/deviations');
        } catch (error) {
            console.error('Failed to delete deviation:', error);
            setMessages(prev => [
                ...prev.filter(msg => msg.type !== 'error'),
                { type: 'error', text: 'Kunde inte radera avvikelsen.' }
            ]);
        }
    };

    const handleChange = (field, e) => {
        markStale();
        setMessages(prev => prev.filter(msg => msg.type !== 'success'));
        setDeviation(prev => ({
            ...prev,
            [field]: e
        }));
        if (showPdfPanel) {
            closePdfPreview();
        }
    };

    const addCost = () => {
        markStale();
        const tempId = `temp_${Date.now()}`;
        setDeviation(prev => ({
            ...prev,
            costs: [...(prev?.costs ?? []), { tempId, text: '', costSEK: null }]
        }));
    };

    const removeCost = (idOrTempId) => {
        markStale();
        setDeviation(prev => ({
            ...prev,
            costs: (prev?.costs ?? []).filter(
                cost => (cost.id ?? cost.tempId) !== idOrTempId
            )
        }));
    };

    const handleCostChange = (idOrTempId, field, value) => {
        markStale();
        setDeviation(prev => ({
            ...prev,
            costs: (prev?.costs ?? []).map(cost =>
                (cost.id ?? cost.tempId) === idOrTempId
                    ? { ...cost, [field]: value }
                    : cost
            )
        }));
    };

    const applyLinkedOrderData = (order, linkedData) => {
        markStale();
        setMessages(prev => prev.filter(msg => msg.type !== 'success'));
        setDeviation(prev => ({
            ...prev,
            customerOrderId: order.id,
            customerOrderNr: order.customerOrderNr,
            customerOrderCustomerName: linkedData?.customerName ?? order.customerName ?? null,
            customerId: linkedData?.customerId ?? prev?.customerId ?? null,
            supplierId: linkedData?.supplierId ?? prev?.supplierId ?? null,
            pricePer1000: linkedData?.pricePer1000 ?? null,
            nrOfProduced: linkedData?.nrOfProduced ?? null,
            freightCostPer1000: linkedData?.freightCostPer1000 ?? null
        }));

        if (showPdfPanel) {
            closePdfPreview();
        }
    };

    const buildDeviationPayload = (source) => {
        const costs = Array.isArray(source?.costs)
            ? source.costs.map(cost => ({
                text: cost?.text ?? null,
                costSEK: parseNullableNumber(cost?.costSEK)
            }))
            : [];

        return {
            customerOrderId: parseNullableInt(source?.customerOrderId),
            isInternal: Boolean(source?.isInternal),
            status: source?.status || 'OPEN',
            deviationNr: source?.deviationNr ?? null,
            createdByUserId: parseNullableInt(source?.createdByUserId),
            createdTimeStamp: source?.createdTimeStamp ?? null,
            editedByUserId: parseNullableInt(source?.editedByUserId),
            editedTimeStamp: source?.editedTimeStamp ?? null,
            responsibleUserId: parseNullableInt(source?.responsibleUserId),
            deviationOpened: source?.deviationOpened ?? null,
            deviationClosed: source?.deviationClosed ?? null,
            deviationProcessCode: source?.deviationProcessCode ?? null,
            supplierId: parseNullableInt(source?.supplierId),
            customerId: parseNullableInt(source?.customerId),
            deviationTypeCode: source?.deviationTypeCode ?? null,
            description: source?.description ?? null,
            rootCause: source?.rootCause ?? null,
            measureTaken: source?.measureTaken ?? null,
            estimatedInternalCostSEK: parseNullableNumber(source?.estimatedInternalCostSEK),
            estimatedInternalCostPercentageOfOrder: parseNullableNumber(source?.estimatedInternalCostPercentageOfOrder),
            supplierDecisionCode: source?.supplierDecisionCode ?? null,
            companyDecisionCode: source?.companyDecisionCode ?? null,
            actualInternalCostSEK: parseNullableNumber(source?.actualInternalCostSEK),
            nrOfProduced: parseNullableNumber(source?.nrOfProduced),
            nrOfClaimed: parseNullableNumber(source?.nrOfClaimed),
            freightCostPer1000: parseNullableNumber(source?.freightCostPer1000),
            freightCostCurrencyId: parseNullableInt(source?.freightCostCurrencyId),
            claimReason: source?.claimReason ?? null,
            followupInfo: source?.followupInfo ?? null,
            deviationClosedToSupplier: source?.deviationClosedToSupplier ?? null,
            costs
        };
    };

    const handleLinkOrder = async (order) => {
        setShowLinkOrder(false);

        try {
            const response = await apiClient.get(`/deviations/customer-order-data/${order.id}`);
            applyLinkedOrderData(order, response.data);
        } catch (error) {
            console.error('Failed to load linked customer order data:', error);
            applyLinkedOrderData(order, null);
            setMessages(prev => [
                ...prev.filter(msg => msg.type !== 'error'),
                { type: 'error', text: 'Kunde inte hämta all orderdata. Kontrollera uppgifterna innan du sparar.' }
            ]);
        }
    };

    const handleUnlinkOrder = () => {
        handleChange('customerOrderId', null);
        handleChange('customerOrderNr', null);
        handleChange('customerOrderCustomerName', null);
        handleChange('pricePer1000', null);
        handleChange('nrOfProduced', null);
        handleChange('freightCostPer1000', null);
    };

    const costRows = useMemo(() => deviation?.costs ?? [], [deviation]);

    useEffect(() => {
        const controller = new AbortController();

        const loadDeviation = async () => {
            setLoading(true);
            try {
                const formOptionsRes = await apiClient.get('/deviations/form-options', { signal: controller.signal });
                const opts = formOptionsRes.data;
                setSuppliers(opts.suppliers ?? []);
                setCustomers(opts.customers ?? []);
                setUsers(opts.users ?? []);
                setCurrencies(opts.currencies ?? []);
                setDeviationProcesses(opts.deviationProcessCodes ?? []);
                setDeviationTypes(opts.deviationTypeCodes ?? []);
                setDeviationDecisions(opts.supplierDecisionCodes ?? []);

                if (isNewDeviation) {
                    const emptyDeviation = createNewDeviationModel();
                    setDeviation(emptyDeviation);
                    setOriginalDeviation(structuredClone(emptyDeviation));
                    setAttachedFiles([]);
                } else {
                    const deviationRes = await apiClient.get(`/deviations/${id}`, { signal: controller.signal });
                    const data = deviationRes.data;
                    const attachments = (
                        Array.isArray(data?.attachedFiles)
                            ? data.attachedFiles
                            : []
                    ).map(att => ({
                        ...att,
                        url: att.path ?? ''
                    }));

                    setDeviation(data ?? null);
                    setOriginalDeviation(structuredClone(data));
                    setAttachedFiles(attachments.map(f => ({ ...f })));
                }
                
                try { setBadges && setBadges(defaultBadges); } catch (e) { /* ignore if unavailable */ }
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load deviation:', error);
                setDeviation(null);
            } finally {
                if (!controller.signal.aborted) {
                    setLoading(false);
                }
            }
        };

        loadDeviation();

        return () => controller.abort();
    }, [id, isNewDeviation]);






    if (loading) {
        return (
            <div className="pl-10 space-y-4 pt-1 ml-80 mr-60">
                <Skeleton height={30} width={420} className='mb-5' />
                <Skeleton height={240} />
                <Skeleton height={180} />
                <Skeleton height={130} />
            </div>
        );
    }

    if (!deviation) {
        return (
            <div className="pt-2 text-sm text-red-700">
                Kunde inte ladda avvikelsen.
                <button
                    type="button"
                    onClick={() => navigate('/management/deviations')}
                    className="ml-4 rounded-sm border border-gray-300 bg-white px-3 py-1 text-xs text-gray-700 hover:bg-gray-100"
                >
                    Tillbaka
                </button>
            </div>
        );
    }

    const isClosed = String(deviation.status || '').toUpperCase() === 'CLOSED';

    return (
        <div className="relative flex flex-col h-full">

            <OrderSearchModal
                isOpen={showLinkOrder}
                onClose={() => setShowLinkOrder(false)}
                onSelect={handleLinkOrder}
            />

            {/* Delete Confirmation Modal */}
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA AVVIKELSE"
                message={`Är du säker på att du vill radera avvikelse ${deviation.deviationNr ?? deviation.id}? Åtgärden kan inte ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            {/* Unsaved Changes Warning Modal */}
            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparande ändringar"
                message={
                    unsavedWarningReason === 'print'
                        ? 'Du har osparande ändringar. Vänligen spara avvikelsen innan du fortsätter.'
                        : 'Det finns ej sparande ändringar, vill du ändå fortsätta?'
                }
                confirmText={unsavedWarningReason === 'print' ? '' : 'Fortsätt ändå'}
                cancelText={unsavedWarningReason === 'print' ? 'Avbryt' : 'Avbryt'}
                isDestructive={false}
            />

            <h2 className="ml-90 text-sm pt-2 pb-2 text-gray-700">{deviation?.id ? (<>Avvikelse <span className="ml-2 text-red-500">{deviation.deviationNr}</span></>) : ("Ny avvikelse")}</h2>

            <div className="flex h-full items-stretch">

                {/* Left panel */}
                <div className="flex flex-col w-80 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            <div className="grid grid-cols-5 gap-4 mx-2">
                                {deviation?.createdTimeStamp && (
                                    <>
                                        <div className="col-span-1">
                                            <span className="font-medium">Skapad:</span>
                                        </div>
                                        <div className="col-span-2">
                                            {new Date(deviation.createdTimeStamp).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                        </div>
                                        <div className="col-span-2 text-gray-500">
                                            {deviation?.createdByUserName && `av ${deviation.createdByUserName}`}
                                        </div>
                                    </>
                                )}
                            </div>
                            <div className="grid grid-cols-5 gap-4 mx-2">
                                {deviation?.editedTimeStamp && (
                                    <>
                                        <div className="col-span-1">
                                            <span className="font-medium">Redigerad:</span>
                                        </div>
                                        <div className="col-span-2">
                                            {new Date(deviation.editedTimeStamp).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
                                        </div>
                                        <div className="col-span-2 text-gray-500">
                                            {deviation?.editedByUserName && `av ${deviation.editedByUserName}`}
                                        </div>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                    {deviation?.customerOrderId && (
                        <>
                            <hr className="mt-4 border-gray-300 dark:border-white" />
                            <div className="mt-4 space-y-1 text-xs text-gray-600 mx-2">
                                <div className="font-medium text-gray-700 mb-1">Kopplad order</div>
                                <div className="flex gap-1">
                                    <span className="text-gray-500 w-14 flex-none">Ordernr:</span>
                                    <span className="font-medium">{deviation.customerOrderNr ?? deviation.customerOrderId}</span>
                                </div>
                                {(deviation.customerOrderCustomerName) && (
                                    <div className="flex gap-1">
                                        <span className="text-gray-500 w-14 flex-none">Kund:</span>
                                        <span>{deviation.customerOrderCustomerName}</span>
                                    </div>
                                )}
                            </div>
                        </>
                    )}
                    <hr className="mt-5 border-gray-300 dark:border-white" />
                    <h2 className="text-sm text-center text-gray-700 mt-5">Meddelanden</h2>
                    {messages.length == 0 ? (
                        <p className='text-xs text-center font-light mt-4'>Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            {[...messages].map((message, index) => (
                                <li key={index} className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' :
                                    message.type === 'warning' ? 'bg-yellow-100 text-yellow-800' :
                                        'bg-green-100 text-green-700'
                                    }`}>
                                    {message.text}
                                </li>
                            ))}
                        </ul>
                    )}
                    <hr className="mt-5 border-gray-300 dark:border-white" />
                    {deviation && attachedFiles && (
                        <FileList
                            files={attachedFiles}
                            onRemove={handleRemoveFile}
                            onAdd={handleAddFile}
                            onEdit={handleEditFile}
                            entityId={deviation?.id}
                            entityType="deviation"
                        />
                    )}
                </div>

                {/* Form */}
                <div className="flex-grow ps-4 pe-10 py-2 max-w-350">

                    {/* Toolbar with actions */}
                    <div className="flex justify-between w-full mb-5">
                        <div className='flex items-center space-x-4'>
                            <button
                                type='button'
                                onClick={handleBackClick}
                                className="shadow-md/30 text-xs text-white bg-gray-500 hover:bg-gray-700 px-5 p-[5px]"
                            >
                                Tillbaka
                            </button>
                            <button
                                type='button'
                                onClick={handleSave}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px]"
                            >
                                Spara
                            </button>
                            {deviation?.customerOrderId ? (
                                <button
                                    type='button'
                                    onClick={handleUnlinkOrder}
                                    className="shadow-md/30 text-xs text-white bg-amber-500 hover:bg-amber-600 px-5 p-[5px]"
                                    title={`Kopplad till order ${deviation.customerOrderNr ?? deviation.customerOrderId}`}
                                >
                                    Ta bort koppling till order
                                </button>
                            ) : (
                                <button
                                    type='button'
                                    onClick={() => setShowLinkOrder(true)}
                                    className="shadow-md/30 text-xs text-white bg-amber-500 hover:bg-amber-600 px-5 p-[5px]"
                                >
                                    Koppla till order
                                </button>
                            )}
                            {deviation?.id != 0 && (
                                <button
                                    type="button"
                                    onClick={getPdf}
                                    className="shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-[5px] ml-10"
                                >
                                    Skriv ut
                                </button>
                            )}
                        </div>
                        <div className='flex items-center space-x-4'>
                            {deviation?.id != 0 && (
                                <button
                                    type="button"
                                    onClick={() => setShowDeleteConfirm(true)}
                                    className="shadow-md/30 text-xs text-white bg-red-700 hover:bg-red-800 px-5 p-[5px]"
                                >
                                    Radera
                                </button>
                            )}
                        </div>
                    </div>

                    <div className="mt-8 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-25">
                        <span>
                            <div className="grid grid-cols-[350px_250px] gap-x-20 gap-y-10">
                                <span>
                                    <LabeledReactSelect
                                        name='customerId'
                                        label='Kund'
                                        value={deviation?.customerId || ''}
                                        items={customers}
                                        onChange={(e) => handleChange('customerId', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledReactSelect
                                        name='supplierId'
                                        label='Leverantör'
                                        value={deviation?.supplierId || ''}
                                        items={suppliers}
                                        onChange={(e) => handleChange('supplierId', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledReactSelect
                                        name='responsibleUserId'
                                        label='Ansvarig'
                                        value={deviation?.responsibleUserId || ''}
                                        items={users}
                                        onChange={(e) => handleChange('responsibleUserId', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="2"
                                    />
                                    <LabeledReactSelect
                                        name='deviationProcessCode'
                                        label='Process'
                                        value={deviation?.deviationProcessCode || ''}
                                        items={deviationProcesses}
                                        onChange={(e) => handleChange('deviationProcessCode', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledReactSelect
                                        name='deviationTypeCode'
                                        label='Avvikelsetyp'
                                        value={deviation?.deviationTypeCode || ''}
                                        items={deviationTypes}
                                        onChange={(e) => handleChange('deviationTypeCode', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                    <LabeledReactSelect
                                        name='supplierDecisionCode'
                                        label='Lev.beslut'
                                        value={deviation?.supplierDecisionCode || ''}
                                        items={deviationDecisions}
                                        onChange={(e) => handleChange('supplierDecisionCode', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="2"
                                    />
                                    <LabeledReactSelect
                                        name='companyDecisionCode'
                                        label='Econop.beslut'
                                        value={deviation?.companyDecisionCode || ''}
                                        items={deviationDecisions}
                                        onChange={(e) => handleChange('companyDecisionCode', e)}
                                        disableInactive
                                        labelWidth="w-20"
                                        margintop="0"
                                    />
                                </span>
                                <span>
                                    <div className='flex items-center space-x-1 w-50 text-xs pt-[1px] pb-1'>
                                        <label className={`w-20 flex-none text-xs text-gray-700`}>Status</label>
                                        <SwitchSelector
                                            name="status"
                                            forcedSelectedIndex={deviation.status === 'OPEN' ? 0 : deviation.status === 'CLOSED' ? 1 : null}
                                            onChange={(value) => handleChange('status', value)}
                                            options={statusOptions}
                                            backgroundColor={"#353b48"} />
                                    </div>
                                    <LabeledSwitch
                                        field='isInternal'
                                        name='isInternal'
                                        label='Intern'
                                        value={deviation?.isInternal || false}
                                        onChange={(rowId, field, checked) => handleChange(field, checked)}
                                        labelWidth="w-20"
                                        margintop="0" />
                                    <div className='flex items-center space-x-1 w-50 text-xs pt-[1px] mt-3'>
                                        <label className={`w-20 flex-none text-xs text-gray-700`}>Öppnad</label>
                                        <input
                                            type="date"
                                            value={toSwedishDateInputValue(deviation?.deviationOpened)}
                                            onChange={(e) => handleChange('deviationOpened', fromDateInputToSwedishIso(e.target.value))}
                                            className="w-30 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                                        />
                                    </div>
                                    <div className='flex items-center space-x-1 w-50 text-xs pt-[1px]'>
                                        <label className={`w-20 flex-none text-xs text-gray-700`}>Stängd</label>
                                        <input
                                            type="date"
                                            value={toSwedishDateInputValue(deviation?.deviationClosed)}
                                            onChange={(e) => handleChange('deviationClosed', fromDateInputToSwedishIso(e.target.value))}
                                            className="w-30 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                                        />
                                    </div>
                                    <div className='flex items-center space-x-1 w-50 text-xs pt-[1px]'>
                                        <label className={`w-20 flex-none text-xs text-gray-700`}>Stängd mot lev.</label>
                                        <input
                                            type="date"
                                            value={toSwedishDateInputValue(deviation?.deviationClosedToSupplier)}
                                            onChange={(e) => handleChange('deviationClosedToSupplier', fromDateInputToSwedishIso(e.target.value))}
                                            className="w-30 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                                        />
                                    </div>
                                </span>
                            </div>
                            <div className="mt-10 grid grid-cols-[350px_250px] gap-x-20 gap-y-10">
                                <span>
                                    <LabeledInput
                                        label='Tot. uppskattad intern kostnad'
                                        value={deviation?.estimatedInternalCostSEK || ''}
                                        onChange={(e) => handleChange('estimatedInternalCostSEK', e.target.value)}
                                        labelWidth="w-45"
                                        margintop="0"
                                        suffix="SEK"
                                    />
                                    <LabeledInput
                                        label='Tot. verklig intern kostnad'
                                        value={deviation?.actualInternalCostSEK || ''}
                                        onChange={(e) => handleChange('actualInternalCostSEK', e.target.value)}
                                        labelWidth="w-45"
                                        margintop="0"
                                        suffix="SEK"
                                    />
                                </span>
                                <span>
                                    <LabeledInput
                                        label='Antal producerade'
                                        value={deviation?.nrOfProduced || ''}
                                        onChange={(e) => handleChange('nrOfProduced', e.target.value)}
                                        labelWidth="w-25"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label='Antal claimed'
                                        value={deviation?.nrOfClaimed || ''}
                                        onChange={(e) => handleChange('nrOfClaimed', e.target.value)}
                                        labelWidth="w-25"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label='Ink.pris/1000'
                                        value={deviation?.pricePer1000 || ''}
                                        labelWidth="w-25"
                                        margintop="0"
                                        disabled
                                    />
                                    <div className="flex items-center w-full">
                                        <div className="flex-1 min-w-0">
                                            <LabeledInput
                                                label='Fraktkostnad/1000'
                                                value={deviation?.freightCostPer1000 || ''}
                                                onChange={(e) => handleChange('freightCostPer1000', e.target.value)}
                                                labelWidth="w-25"
                                                margintop="0"
                                            />
                                        </div>
                                        <select
                                            type="text"
                                            value={deviation?.freightCostCurrencyId || ''}
                                            onChange={(e) => handleChange('freightCostCurrencyId', e.target.value)}
                                            className="w-15 ml-1 text-xs px-1 pt-[3px] pb-[3px] border border-gray-300 bg-white rounded-sm focus:outline-none"
                                            placeholder="Valuta"
                                        >
                                            <option value="">Valuta</option>
                                            {currencies.map((currency) => (
                                                <option key={currency.id} value={currency.id}>{currency.name}</option>
                                            ))}
                                        </select>
                                    </div>
                                </span>
                            </div>
                            <div className='mt-15 flex-1 bg-slate-50 p-5 border border-gray-200 rounded-sm shadow-sm mt-6'>
                                <div className="relative flex items-center mb-3">
                                    <h3 className="ml-1 text-xs text-gray-700 font-semibold">Kostnaderregistreringar</h3>
                                    <button
                                        type="button"
                                        onClick={addCost}
                                        className="absolute right-0 text-xs text-blue-600 hover:bg-blue-50 px-2 py-1 rounded border border-transparent hover:border-blue-200"
                                    >
                                        + Lägg till
                                    </button>
                                </div>
                                <div className="mt-5">
                                    <div className="flex items-center space-x-1 w-full text-xs pt-[1px] mt-3">
                                        <table className='w-full table-auto border-separate border-spacing-y-0 border-spacing-x-[2px]'>
                                            <thead>
                                                <tr className='align-top'>
                                                    <th className='pl-2 pb-1 w-50 text-xs text-gray-700 font-normal text-left'>Text</th>
                                                    <th className='pl-2 w-30 pb-1 text-xs text-gray-700 font-normal text-left'>Kostnad</th>
                                                    <th className='pb-1 w-15 text-xs text-gray-700 font-normal text-center'>Åtgärd</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {costRows.length === 0 ? (
                                                    <tr>
                                                        <td colSpan={3} className="px-2 py-4 text-center text-gray-400">Inga kostnader registrerade.</td>
                                                    </tr>
                                                ) : (
                                                    costRows.map((cost) => (
                                                        <tr key={cost.id ?? cost.tempId} className="">
                                                            <td className="items-center">
                                                                <div className={"flex items-center rounded-sm px-2 py-0 border border-gray-300 rounded-sm bg-white"}>
                                                                    <input
                                                                        value={cost.text ?? ''}
                                                                        onChange={(e) => handleCostChange(cost.id ?? cost.tempId, 'text', e.target.value)}
                                                                        className="text-xs min-w-0 w-20 grow pl-2 pr-1 py-1 text-base text-left focus:outline-none"
                                                                    />
                                                                </div>
                                                            </td>
                                                            <td className="items-center">
                                                                <div className={"flex items-center rounded-sm px-2 py-0 border border-gray-300 rounded-sm bg-white"}>
                                                                    <NumberInput
                                                                        rowId={cost.id ?? cost.tempId}
                                                                        field="costSEK"
                                                                        value={cost.costSEK ?? ''}
                                                                        decimals={0}
                                                                        onChange={(rowId, field, value) => handleCostChange(rowId, field, value)}
                                                                        className="block text-xs min-w-0 w-20 grow px-2 py-1 text-base text-left focus:outline-none"
                                                                    />
                                                                    <span className="text-xs text-gray-500">SEK</span>
                                                                </div>
                                                            </td>
                                                            <td className="text-center">
                                                                <button
                                                                    type="button"
                                                                    onClick={() => removeCost(cost.id ?? cost.tempId)}
                                                                    className="text-xs text-red-600 hover:underline"
                                                                >
                                                                    Ta bort
                                                                </button>
                                                            </td>
                                                        </tr>
                                                    ))
                                                )}
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                            </div>
                        </span>

                        <span className='min-w-0'>
                            <div>
                                <div className="ml-2 mb-1 text-xs text-gray-700">Beskrivning</div>
                                <textarea
                                    value={deviation.description || ''}
                                    onChange={(e) => handleChange('description', e.target.value)}
                                    className={`text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25`}
                                />
                            </div>
                            <div className='mt-2'>
                                <div className="ml-2 mb-1 text-xs text-gray-700">Grundorsak</div>
                                <textarea
                                    value={deviation.rootCause || ''}
                                    onChange={(e) => handleChange('rootCause', e.target.value)}
                                    className={`text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25`}
                                />
                            </div>
                            <div className='mt-2'>
                                <div className="ml-2 mb-1 text-xs text-gray-700">Åtgärd</div>
                                <textarea
                                    value={deviation.measureTaken || ''}
                                    onChange={(e) => handleChange('measureTaken', e.target.value)}
                                    className={`text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25`}
                                />
                            </div>
                            <div className='mt-2'>
                                <div className="ml-2 mb-1 text-xs text-gray-700">Uppföljning</div>
                                <textarea
                                    value={deviation.followupInfo || ''}
                                    onChange={(e) => handleChange('followupInfo', e.target.value)}
                                    className={`text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25`}
                                />
                            </div>
                            <div className='mt-2'>
                                <div className="ml-2 mb-1 text-xs text-gray-700">Claim reason</div>
                                <textarea
                                    value={deviation.claimReason || ''}
                                    onChange={(e) => handleChange('claimReason', e.target.value)}
                                    className={`text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25`}
                                />
                            </div>
                        </span>
                    </div>
                </div>

            </div>


        </div>
    );
};

export default Deviation;
