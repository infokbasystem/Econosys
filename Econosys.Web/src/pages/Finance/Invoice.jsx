import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import { usePdf } from '../../contexts/PdfContext';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledSelect from '../../components/LabeledSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import NumberInput from '../../components/NumberInput';
import apiClient from '../../config/apiClient';
import { formatDateTime } from '../../helpers/dateUtils';
import { getFileNameFromContentDisposition } from '../../helpers/fileUtils';
import { parseNullableInt, parseNullableNumber } from '../../helpers/numberUtils';

const createNewInvoiceModel = () => ({
    id: 0,
    invoiceNumber: null,
    customerName: '',
    address: '',
    postalNr: '',
    postalAddress: '',
    country: '',
    vatNr: '',
    yourReference: '',
    ourReference: '',
    invoiceDate: new Date().toISOString(),
    invoiceDays: 30,
    termsOfPayment: '30 dagar fran fakturadatum.',
    salesCurrencyId: null,
    accountDate: null,
    journalNr: null,
    account: '',
    costCenter: '',
    invoiceOk: false,
    invoiceRows: [
        {
            tempId: `temp_${Date.now()}`,
            text: '',
            weight: null,
            nrOf: null,
            unitPrice: null,
            sum: null,
            accountNr: '',
            costCenter: '',
            vatGround: true,
            unitId: null,
            calculate: true,
        },
    ],
});

const toDateInputValue = (value) => {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return date.toISOString().split('T')[0];
};

const autoResizeTextarea = (element) => {
    if (!element) return;
    element.style.height = 'auto';
    element.style.height = `${element.scrollHeight}px`;
};

const Invoice = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewInvoice = id === 'new';

    const { openPdfPreview, markStale, clearStale, showPdfPanel, closePdfPreview } = usePdf();

    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [unsavedWarningReason, setUnsavedWarningReason] = useState('navigate');
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);

    const [invoice, setInvoice] = useState(null);
    const [originalInvoice, setOriginalInvoice] = useState(null);
    const [currencies, setCurrencies] = useState([]);
    const [units, setUnits] = useState([]);

    const hasUnsavedChanges = useCallback(() => {
        if (!invoice || !originalInvoice) return false;
        return JSON.stringify(invoice) !== JSON.stringify(originalInvoice);
    }, [invoice, originalInvoice]);

    const blocker = useBlocker(hasUnsavedChanges);

    useEffect(() => {
        return () => {
            closePdfPreview?.();
        };
    }, [closePdfPreview]);

    useEffect(() => {
        const handleBeforeUnload = (e) => {
            if (hasUnsavedChanges()) {
                e.preventDefault();
                e.returnValue = '';
                return '';
            }
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setUnsavedWarningReason('navigate');
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }
        navigate('/finance/searchinvoice');
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);

        if (unsavedWarningReason === 'print') {
            getPdf({ ignoreUnsaved: true });
            return;
        }

        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    const handleChange = (field, value) => {
        markStale();
        setMessages((prev) => prev.filter((msg) => msg.type !== 'success'));
        setInvoice((prev) => ({
            ...prev,
            [field]: value,
        }));

        if (showPdfPanel) {
            closePdfPreview();
        }
    };

    const handleInvoiceRowChange = (idOrTempId, field, value) => {
        markStale();

        setInvoice((prev) => ({
            ...prev,
            invoiceRows: (prev?.invoiceRows ?? []).map((row) => {
                if ((row.id ?? row.tempId) !== idOrTempId) {
                    return row;
                }

                const next = { ...row, [field]: value };
                if ((field === 'nrOf' || field === 'unitPrice') && next.calculate) {
                    const nrOf = parseNullableNumber(next.nrOf) ?? 0;
                    const unitPrice = parseNullableNumber(next.unitPrice) ?? 0;
                    next.sum = nrOf * unitPrice;
                }

                return next;
            }),
        }));
    };

    const addInvoiceRow = () => {
        markStale();
        const tempId = `temp_${Date.now()}`;

        setInvoice((prev) => ({
            ...prev,
            invoiceRows: [
                ...(prev?.invoiceRows ?? []),
                {
                    tempId,
                    text: '',
                    weight: null,
                    nrOf: null,
                    unitPrice: null,
                    sum: null,
                    accountNr: '',
                    costCenter: '',
                    vatGround: true,
                    unitId: null,
                    calculate: true,
                },
            ],
        }));
    };

    const removeInvoiceRow = (idOrTempId) => {
        markStale();

        setInvoice((prev) => ({
            ...prev,
            invoiceRows: (prev?.invoiceRows ?? []).filter((row) => (row.id ?? row.tempId) !== idOrTempId),
        }));
    };

    const buildInvoicePayload = (source) => {
        const invoiceRows = Array.isArray(source?.invoiceRows)
            ? source.invoiceRows.map((row, index) => ({
                sortOrder: index + 1,
                invoiceRowNumber: index + 1,
                text: row?.text ?? null,
                nrOf: parseNullableNumber(row?.nrOf),
                unitPrice: parseNullableNumber(row?.unitPrice),
                sum: parseNullableNumber(row?.sum),
                vatGround: Boolean(row?.vatGround),
                calculate: Boolean(row?.calculate),
                accountNr: row?.accountNr ?? null,
                costCenter: row?.costCenter ?? null,
                unitId: parseNullableInt(row?.unitId),
                weight: parseNullableNumber(row?.weight),
            }))
            : [];

        return {
            invoiceTypeCode: source?.invoiceTypeCode ?? null,
            customerName: source?.customerName ?? null,
            address: source?.address ?? null,
            postalNr: source?.postalNr ?? null,
            postalAddress: source?.postalAddress ?? null,
            country: source?.country ?? null,
            vatNr: source?.vatNr ?? null,
            invoiceNumber: parseNullableInt(source?.invoiceNumber),
            invoiceDate: source?.invoiceDate ?? null,
            yourReference: source?.yourReference ?? null,
            ourReference: source?.ourReference ?? null,
            invoiceDays: parseNullableInt(source?.invoiceDays),
            termsOfPayment: source?.termsOfPayment ?? null,
            salesCurrencyId: parseNullableInt(source?.salesCurrencyId),
            accountDate: source?.accountDate ?? null,
            journalNr: parseNullableInt(source?.journalNr),
            account: source?.account ?? null,
            costCenter: source?.costCenter ?? null,
            invoiceOk: Boolean(source?.invoiceOk),
            invoiceRows,
        };
    };

    const getPdf = async ({ ignoreUnsaved = false } = {}) => {
        if (!invoice?.id) return;

        if (!ignoreUnsaved && hasUnsavedChanges()) {
            setUnsavedWarningReason('print');
            setShowUnsavedWarning(true);
            return;
        }

        openPdfPreview('');

        try {
            const response = await apiClient.get(`/pdf/invoice/${invoice.id}`, {
                responseType: 'blob',
            });
            const contentDisposition = response.headers['content-disposition'];
            const apiFileName = getFileNameFromContentDisposition(contentDisposition) || 'invoice.pdf';
            const blob = response.data;
            const url = URL.createObjectURL(blob);
            openPdfPreview(url, apiFileName);
        } catch (error) {
            console.error('Failed to get invoice PDF:', error);
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error'),
                { type: 'error', text: 'Kunde inte skriva ut fakturan. PDF endpoint saknas eller kunde inte laddas.' },
            ]);
        }
    };

    const handleSave = async () => {
        if (!invoice) return;

        const payload = buildInvoicePayload(invoice);
        const invoiceId = parseNullableInt(invoice?.id);
        const isCreatingNew = !(invoiceId && invoiceId > 0);

        try {
            const response = invoiceId && invoiceId > 0
                ? await apiClient.put(`/invoices/${invoiceId}`, payload)
                : await apiClient.post('/invoices', payload);

            const savedInvoice = response.data;
            setInvoice(savedInvoice ?? null);
            setOriginalInvoice(structuredClone(savedInvoice));
            clearStale();
            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'success'),
                { type: 'success', text: 'Fakturan sparades.' },
            ]);

            if (isCreatingNew && savedInvoice?.id) {
                navigate(`/finance/invoice/${savedInvoice.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save invoice:', error);

            const status = error?.response?.status;
            const text = status === 404 || status === 405
                ? 'Sparfunktionen for faktura ar inte tillganglig i API:et an.'
                : 'Kunde inte spara fakturan.';

            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error'),
                { type: 'error', text },
            ]);
        }
    };

    const handleDelete = async () => {
        if (!invoice?.id) return;

        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/invoices/${invoice.id}`);
            navigate('/finance/searchinvoice');
        } catch (error) {
            console.error('Failed to delete invoice:', error);

            const status = error?.response?.status;
            const text = status === 404 || status === 405
                ? 'Raderingsfunktionen for faktura ar inte tillganglig i API:et an.'
                : 'Kunde inte radera fakturan.';

            setMessages((prev) => [
                ...prev.filter((msg) => msg.type !== 'error'),
                { type: 'error', text },
            ]);
        }
    };

    useEffect(() => {
        const controller = new AbortController();

        const loadOptions = async () => {
            try {
                const [currencyRes, unitRes] = await Promise.all([
                    apiClient.post('/currencies/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }, { signal: controller.signal }),
                    apiClient.post('/units/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }, { signal: controller.signal }),
                ]);

                setCurrencies(Array.isArray(currencyRes?.data?.items) ? currencyRes.data.items : []);
                setUnits(Array.isArray(unitRes?.data?.items) ? unitRes.data.items : []);
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed loading invoice options:', error);
                setCurrencies([]);
                setUnits([]);
            }
        };

        const loadInvoice = async () => {
            setLoading(true);

            try {
                await loadOptions();

                if (isNewInvoice) {
                    const emptyInvoice = createNewInvoiceModel();
                    setInvoice(emptyInvoice);
                    setOriginalInvoice(structuredClone(emptyInvoice));
                } else {
                    const response = await apiClient.get(`/invoices/${id}`, { signal: controller.signal });
                    const data = response.data;

                    const normalized = {
                        ...data,
                        invoiceRows: Array.isArray(data?.invoiceRows)
                            ? data.invoiceRows.map((row) => ({ ...row }))
                            : [],
                    };

                    setInvoice(normalized ?? null);
                    setOriginalInvoice(structuredClone(normalized));
                }

                clearStale();
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load invoice:', error);
                setInvoice(null);
            } finally {
                if (!controller.signal.aborted) {
                    setLoading(false);
                }
            }
        };

        loadInvoice();
        return () => controller.abort();
    }, [id, isNewInvoice]);

    const totals = useMemo(() => {
        const rows = invoice?.invoiceRows ?? [];

        let sumExVat = 0;
        let vat = 0;

        rows.forEach((row) => {
            const rowSum = parseNullableNumber(row?.sum) ?? 0;
            sumExVat += rowSum;
            if (row?.vatGround) {
                vat += rowSum * 0.25;
            }
        });

        return {
            sumExVat,
            vat,
            sumInclVat: sumExVat + vat,
        };
    }, [invoice?.invoiceRows]);

    if (loading) {
        return (
            <div className="pl-10 space-y-4 pt-1 ml-15 mr-20">
                <Skeleton height={30} width={420} className="mb-5" />
                <Skeleton height={200} />
                <Skeleton height={290} />
                <Skeleton height={80} />
            </div>
        );
    }

    if (!invoice) {
        return (
            <div className="pt-2 text-sm text-red-700">
                Kunde inte ladda fakturan.
                <button
                    type="button"
                    onClick={() => navigate('/finance/searchinvoice')}
                    className="ml-4 rounded-sm border border-gray-300 bg-white px-3 py-1 text-xs text-gray-700 hover:bg-gray-100"
                >
                    Tillbaka
                </button>
            </div>
        );
    }

    const dueDate = invoice?.invoiceDate && invoice?.invoiceDays != null
        ? new Date(new Date(invoice.invoiceDate).getTime() + (Number(invoice.invoiceDays) * 24 * 60 * 60 * 1000))
        : null;

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA FAKTURA"
                message={`Är du saker på att du vill radera faktura ${invoice.invoiceNumber ?? invoice.id}? Åtgarden kan inte Ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="OSPARADE ANDRINGAR"
                message={
                    unsavedWarningReason === 'print'
                        ? 'Du har osparade ändringar. Vill du skriva ut utan att spara?'
                        : 'Det finns ej sparade andringar, vill du ända fortsätta?'
                }
                confirmText={unsavedWarningReason === 'print' ? 'Skriv ut anda' : 'Fortsatt anda'}
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-90 text-sm pt-2 pb-2 text-gray-700">
                {invoice?.id ? (
                    <>Faktura <span className="ml-2 text-red-500">{invoice.invoiceNumber ?? invoice.id}</span></>
                ) : (
                    'Ny faktura'
                )}
            </h2>

            <div className="flex h-full items-stretch">

                {/* Left panel */}
                <div className="flex flex-col w-80 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {invoice?.created && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Skapad:</span></div>
                                    <div className="col-span-4">{formatDateTime(invoice.created)}</div>
                                </div>
                            )}
                            {invoice?.edited && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Redigerad:</span></div>
                                    <div className="col-span-4">{formatDateTime(invoice.edited)}</div>
                                </div>
                            )}
                            {invoice.invoiceNumber && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Fakt.nr:</span></div>
                                    <div className="col-span-4">{invoice.invoiceNumber}</div>
                                </div>
                            )}
                            {invoice.invoiceDate && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Fakt.dag:</span></div>
                                    <div className="col-span-4">{toDateInputValue(invoice.invoiceDate)}</div>
                                </div>
                            )}
                            {dueDate && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Förfaller:</span></div>
                                    <div className="col-span-4">{dueDate.toLocaleDateString('sv-SE')}</div>
                                </div>
                            )}
                            {invoice.accountDate && (
                                <div className="grid grid-cols-5 gap-4 mx-2">
                                    <div className="col-span-1"><span className="font-medium">Bokförd:</span></div>
                                    <div className="col-span-4">{toDateInputValue(invoice.accountDate)}</div>
                                </div>
                            )}
                        </div>
                    </div>

                    <hr className="mt-5 border-gray-300" />
                    <h2 className="text-sm text-center text-gray-700 mt-5">Meddelanden</h2>
                    {messages.length === 0 ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            {messages.map((message, index) => (
                                <li
                                    key={index}
                                    className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error'
                                            ? 'bg-red-100 text-red-700'
                                            : message.type === 'warning'
                                                ? 'bg-yellow-100 text-yellow-800'
                                                : 'bg-green-100 text-green-700'
                                        }`}
                                >
                                    {message.text}
                                </li>
                            ))}
                        </ul>
                    )}
                </div>

                <div className="flex-grow ps-4 pe-10 py-2 max-w-350">
                    <div className="flex justify-between w-full mb-5">
                        <div className="flex items-center space-x-4">
                            <button
                                type="button"
                                onClick={handleBackClick}
                                className="shadow-md/30 text-xs text-white bg-gray-500 hover:bg-gray-700 px-5 p-[5px]"
                            >
                                Tillbaka
                            </button>
                            <button
                                type="button"
                                onClick={handleSave}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px]"
                            >
                                Spara
                            </button>
                            {invoice?.id !== 0 && (
                                <button
                                    type="button"
                                    onClick={getPdf}
                                    className="shadow-md/30 text-xs text-gray bg-blue-200 hover:bg-blue-300 px-4 py-[5px] ml-10"
                                >
                                    Skriv ut
                                </button>
                            )}
                        </div>

                        <div className="flex items-center space-x-4">
                            {invoice?.id !== 0 && (
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

                    <div className="mt-8 grid grid-cols-[350px_350px_1fr] gap-x-18 gap-y-8">
                        <div>
                            <LabeledInput
                                label="Kund"
                                value={invoice?.customerName || ''}
                                onChange={(value) => handleChange('customerName', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Adress"
                                value={invoice?.address || ''}
                                onChange={(value) => handleChange('address', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Postnr"
                                value={invoice?.postalNr || ''}
                                onChange={(value) => handleChange('postalNr', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Ort"
                                value={invoice?.postalAddress || ''}
                                onChange={(value) => handleChange('postalAddress', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Land"
                                value={invoice?.country || ''}
                                onChange={(value) => handleChange('country', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Momsreg.nr"
                                value={invoice?.vatNr || ''}
                                onChange={(value) => handleChange('vatNr', value)}
                                labelWidth="w-20"
                                margintop="0"
                            />
                        </div>
                        <div>
                            <LabeledInput
                                label="Var referens"
                                value={invoice?.ourReference || ''}
                                onChange={(value) => handleChange('ourReference', value)}
                                labelWidth="w-25"
                                margintop="0"
                            />
                            <LabeledInput
                                label="Er referens"
                                value={invoice?.yourReference || ''}
                                onChange={(value) => handleChange('yourReference', value)}
                                labelWidth="w-25"
                                margintop="0"
                            />
                            {/* <div className="flex items-center space-x-1 w-full pb-[1px] mt-0">
                                <label className="w-25 text-xs text-gray-700">Fakturadatum</label>
                                <input
                                    type="date"
                                    value={toDateInputValue(invoice?.invoiceDate)}
                                    onChange={(e) => handleChange('invoiceDate', e.target.value ? new Date(e.target.value).toISOString() : null)}
                                    className="w-full text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                />
                            </div>
                            <div className="flex items-center space-x-1 w-full pb-[1px] mt-0">
                                <label className="w-25 text-xs text-gray-700">Bokford datum</label>
                                <input
                                    type="date"
                                    value={toDateInputValue(invoice?.accountDate)}
                                    onChange={(e) => handleChange('accountDate', e.target.value ? new Date(e.target.value).toISOString() : null)}
                                    className="w-full text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                />
                            </div> */}
                            <LabeledInput
                                label="Fakturadagar"
                                value={invoice?.invoiceDays ?? ''}
                                onChange={(value) => handleChange('invoiceDays', value)}
                                labelWidth="w-25"
                                margintop="0"
                                type='number'
                                decimals='0'
                                integerOnly='true'
                                inputWidth='w-30'
                            />
                            <LabeledSelect
                                label="Valuta"
                                value={invoice?.salesCurrencyId || ''}
                                onChange={(value) => handleChange('salesCurrencyId', value)}
                                labelWidth="w-25"
                                inputWidth='w-30'
                                margintop="0"
                                items={currencies}
                                disabled={currencies.length === 0}
                            />
                            <LabeledInput
                                label="Konto kundf."
                                value={invoice?.account || ''}
                                onChange={(value) => handleChange('account', value)}
                                labelWidth="w-25"
                                inputWidth='w-30'
                                margintop="0"
                            />
                            <LabeledSwitch
                                field="invoiceOk"
                                name="invoiceOk"
                                label="Godkänd for bokf."
                                value={Boolean(invoice?.invoiceOk)}
                                onChange={(rowId, field, checked) => handleChange(field, checked)}
                                labelWidth="w-25"
                                marginTop={0}
                            />
                        </div>
                    </div>

                    <div className="mt-8">
                        <table className="w-full table-fixed text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <thead>
                                <tr className="border-t border-gray-300">
                                    <th className="w-[4%] px-4 py-2 text-left text-tiny font-medium text-gray-400 uppercase">Sort</th>
                                    <th className="w-[24%] px-4 py-2 text-left text-tiny font-medium text-gray-400 uppercase">Text</th>
                                    <th className="w-[7%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Vikt</th>
                                    <th className="w-[7%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Antal</th>
                                    <th className="w-[8%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">APris</th>
                                    <th className="w-[8%] pr-4 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Enhet</th>
                                    <th className="w-[9%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Summa</th>
                                    <th className="w-[8%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Konto</th>
                                    <th className="w-[8%] pr-2 py-2 text-right text-tiny font-medium text-gray-400 uppercase">Res.enh</th>
                                    <th className="w-[5%] px-2 py-2 text-center text-tiny font-medium text-gray-400 uppercase">Moms</th>
                                    <th className="w-[7%] px-2 py-2 text-center text-tiny font-medium text-gray-400 uppercase">Atgard</th>
                                </tr>
                            </thead>
                            <tbody className="bg-white">
                                {(invoice?.invoiceRows ?? []).length === 0 ? (
                                    <tr>
                                        <td colSpan={10} className="px-2 py-4 text-center text-gray-400">Inga rader registrerade.</td>
                                    </tr>
                                ) : (
                                    (invoice.invoiceRows ?? []).map((row) => {
                                        const idOrTempId = row.id ?? row.tempId;

                                        return (
                                            <tr key={idOrTempId} className="border-b border-gray-100">
                                                <td className="px-2 py-0.5 text-xs text-gray-600">{row.sortOrder}</td>
                                                <td className="p-0">
                                                    <textarea
                                                        value={row.text ?? ''}
                                                        onChange={(e) => {
                                                            handleInvoiceRowChange(idOrTempId, 'text', e.target.value);
                                                            autoResizeTextarea(e.target);
                                                        }}
                                                        onInput={(e) => autoResizeTextarea(e.target)}
                                                        ref={(element) => autoResizeTextarea(element)}
                                                        rows={1}
                                                        className="w-full h-full text-xs px-3 py-1 bg-transparent border-0 focus:outline-none focus:bg-white focus:border focus:border-blue-400 resize-none overflow-hidden leading-4"
                                                    />
                                                </td>
                                                <td className="p-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="weight"
                                                            value={row.weight ?? ''}
                                                            decimals={0}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="p-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="nrOf"
                                                            value={row.nrOf ?? ''}
                                                            decimals={0}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="unitPrice"
                                                            value={row.unitPrice ?? ''}
                                                            decimals={2}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <select
                                                            value={row.unitId || ''}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'unitId', e.target.value)}
                                                            className="w-full h-full text-xs text-right border-0 focus:outline-none"
                                                        >
                                                            <option value=""></option>
                                                            {units.map((unit) => (
                                                                <option key={unit.id} value={unit.id}>{unit.name}</option>
                                                            ))}
                                                        </select>
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <NumberInput
                                                            rowId={idOrTempId}
                                                            field="sum"
                                                            value={row.sum ?? ''}
                                                            decimals={2}
                                                            onChange={(rowId, field, value) => handleInvoiceRowChange(rowId, field, value)}
                                                            className="w-full h-full text-xs border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <input
                                                            value={row.accountNr ?? ''}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'accountNr', e.target.value)}
                                                            className="w-full h-full text-xs pr-2 border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 border border-transparent bg-white focus-within:border-blue-400">
                                                        <input
                                                            value={row.costCenter ?? ''}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'costCenter', e.target.value)}
                                                            className="w-full h-full text-xs pr-2 border-0 text-right focus:outline-none"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 flex items-center justify-center border border-transparent bg-white focus-within:border-blue-400">
                                                        <input
                                                            type="checkbox"
                                                            checked={Boolean(row.vatGround)}
                                                            onChange={(e) => handleInvoiceRowChange(idOrTempId, 'vatGround', e.target.checked)}
                                                            className="h-4 w-4"
                                                        />
                                                    </div>
                                                </td>
                                                <td className="px-0 relative">
                                                    <div className="absolute inset-0 flex items-center justify-center border border-transparent bg-white focus-within:border-blue-400">
                                                        <button
                                                            type="button"
                                                            onClick={() => removeInvoiceRow(idOrTempId)}
                                                            className="text-red-600 hover:underline"
                                                        >
                                                            Ta bort
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                                {/* Empty row for adding new entries */}
                                <tr className="border-b border-gray-100">
                                    <td className="px-2 py-0.5 text-xs text-gray-400"></td>
                                    <td colSpan="10" className="px-2 py-2">
                                        <button
                                            type="button"
                                            onClick={addInvoiceRow}
                                            className="text-xs text-gray-400 hover:text-gray-600"
                                        >
                                            + Lägg till rad
                                        </button>
                                    </td>
                                </tr>
                            </tbody>
                        </table>

                        <div className="flex justify-end border-t border-gray-200 bg-gray-50 px-2 py-2">
                            <button
                                type="button"
                                onClick={addInvoiceRow}
                                className="text-xs text-blue-600 hover:bg-blue-50 px-2 py-1 rounded border border-transparent hover:border-blue-200"
                            >
                                + Lagg till rad
                            </button>
                        </div>
                    </div>

                    <div className="mt-4 grid grid-cols-[1fr_320px] gap-x-8">
                        <div>
                            <div className="text-xs text-gray-700 mb-1">Bet.villkor</div>
                            <textarea
                                value={invoice?.termsOfPayment || ''}
                                onChange={(e) => handleChange('termsOfPayment', e.target.value)}
                                className="text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-16"
                            />
                        </div>
                        <div className="self-end border-t-2 border-red-500 pt-2">
                            <div className="grid grid-cols-2 gap-y-1 text-xs">
                                <span className="text-gray-700">Ex. moms</span>
                                <span className="text-right text-gray-800">{totals.sumExVat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                                <span className="text-gray-700">Moms</span>
                                <span className="text-right text-gray-800">{totals.vat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                                <span className="text-gray-700 font-semibold">Summa</span>
                                <span className="text-right text-red-700 font-semibold">{totals.sumInclVat.toLocaleString('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Invoice;