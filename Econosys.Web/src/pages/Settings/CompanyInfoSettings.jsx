import { useEffect, useState } from 'react';
import apiClient from '../../config/apiClient';
import LabeledInput from '../../components/LabeledInput';
import LabeledTextArea from '../../components/LabeledTextArea';

const COMPANY_ID = 1;

const defaultForm = {
    companyName: '',
    address: '',
    postalAddress: '',
    zipCode: '',
    telephone1: '',
    telephone2: '',
    fax1: '',
    fax2: '',
    bank: '',
    bic: '',
    iban: '',
    bg: '',
    pg: '',
    vatNr: '',
    invoiceMailSWE: '',
    invoiceMailENG: '',
    documentFileBasePath: '',
    email: '',
    web: '',
    mailWrapper: '',
    googleApiKey: '',
    nrOfInquiryAnswerDays: '',
    vatInfo: '',
    settingsCompany: '',
    invoiceLastNr: '',
    defaultCustomerMessage: '',
    euText: '',
    exportText: '',
};

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    companyName: dto?.companyName ?? '',
    address: dto?.address ?? '',
    postalAddress: dto?.postalAddress ?? '',
    zipCode: dto?.zipCode ?? '',
    telephone1: dto?.telephone1 ?? '',
    telephone2: dto?.telephone2 ?? '',
    fax1: dto?.fax1 ?? '',
    fax2: dto?.fax2 ?? '',
    bank: dto?.bank ?? '',
    bic: dto?.bic ?? '',
    iban: dto?.iban ?? '',
    bg: dto?.bg ?? '',
    pg: dto?.pg ?? '',
    vatNr: dto?.vatNr ?? '',
    invoiceMailSWE: dto?.invoiceMailSWE ?? '',
    invoiceMailENG: dto?.invoiceMailENG ?? '',
    documentFileBasePath: dto?.documentFileBasePath ?? '',
    email: dto?.email ?? '',
    web: dto?.web ?? '',
    mailWrapper: dto?.mailWrapper ?? '',
    googleApiKey: dto?.googleApiKey ?? '',
    nrOfInquiryAnswerDays: dto?.nrOfInquiryAnswerDays?.toString() ?? '',
    vatInfo: dto?.vatInfo ?? '',
    settingsCompany: dto?.settingsCompany ?? '',
    invoiceLastNr: dto?.invoiceLastNr?.toString() ?? '',
    defaultCustomerMessage: dto?.defaultCustomerMessage ?? '',
    euText: dto?.euText ?? '',
    exportText: dto?.exportText ?? '',
});

const mapFormToPayload = (form) => ({
    companyName: form.companyName,
    address: form.address,
    postalAddress: form.postalAddress,
    zipCode: form.zipCode,
    telephone1: form.telephone1,
    telephone2: form.telephone2,
    fax1: form.fax1,
    fax2: form.fax2,
    bank: form.bank,
    bic: form.bic,
    iban: form.iban,
    bg: form.bg,
    pg: form.pg,
    vatNr: form.vatNr,
    invoiceMailSWE: form.invoiceMailSWE,
    invoiceMailENG: form.invoiceMailENG,
    documentFileBasePath: form.documentFileBasePath,
    email: form.email,
    web: form.web,
    mailWrapper: form.mailWrapper,
    googleApiKey: form.googleApiKey,
    nrOfInquiryAnswerDays: parseNullableInt(form.nrOfInquiryAnswerDays),
    vatInfo: form.vatInfo,
    settingsCompany: form.settingsCompany,
    invoiceLastNr: parseNullableInt(form.invoiceLastNr),
    defaultCustomerMessage: form.defaultCustomerMessage,
    euText: form.euText,
    exportText: form.exportText,
});

const CompanyInfoSettings = () => {
    const [form, setForm] = useState(defaultForm);
    const [hasExisting, setHasExisting] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState(null);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const loadCompanyInfo = async () => {
        setIsLoading(true);
        setMessage(null);

        try {
            const response = await apiClient.get(`/companysettings/${COMPANY_ID}`);
            setForm(mapDtoToForm(response.data));
            setHasExisting(true);
        } catch (error) {
            if (error.response?.status === 404) {
                setForm(defaultForm);
                setHasExisting(false);
            } else {
                setMessage({ type: 'error', text: 'Kunde inte hamta foretagsinfo.' });
            }
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        loadCompanyInfo();
    }, []);

    const handleSave = async () => {
        setIsSaving(true);
        setMessage(null);

        try {
            const payload = mapFormToPayload(form);
            const response = hasExisting
                ? await apiClient.put(`/companysettings/${COMPANY_ID}`, payload)
                : await apiClient.post('/companysettings', payload);

            setForm(mapDtoToForm(response.data));
            setHasExisting(true);
            setMessage({ type: 'success', text: 'Foretagsinfo sparades.' });
        } catch (error) {
            console.error('Failed to save company settings:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara foretagsinfo.' });
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Företagsinfo</h2>

            <div className="flex h-full items-stretch">
                <div className="flex-grow ps-4 pe-10 py-2 max-w-350">
                    <div className="flex justify-between w-full mb-5">
                        <div className="flex items-center space-x-4">
                            <button
                                type="button"
                                onClick={handleSave}
                                disabled={isSaving || isLoading}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px]"
                            >
                                {isSaving ? 'Sparar...' : 'Spara'}
                            </button>
                            {/* <button
                                type="button"
                                onClick={loadCompanyInfo}
                                disabled={isSaving || isLoading}
                                className="shadow-md/30 text-xs text-white bg-gray-500 hover:bg-gray-700 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px]"
                            >
                                Ladda om
                            </button> */}
                        </div>
                    </div>

                    {isLoading ? (
                        <p className="text-xs text-gray-600">Laddar foretagsinfo...</p>
                    ) : (
                        <div className="mt-8 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-20">
                            <span>
                                <div className="grid grid-cols-[560px_560px] gap-x-20 gap-y-10">
                                    <span>
                                        <LabeledInput label="Företagsnamn" value={form.companyName} onChange={(v) => updateField('companyName', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Adress" value={form.address} onChange={(v) => updateField('address', v)} labelWidth="w-45" margintop="0" maxLength={100} />
                                        <LabeledInput label="Postadress" value={form.postalAddress} onChange={(v) => updateField('postalAddress', v)} labelWidth="w-45" margintop="0" maxLength={100} />
                                        <LabeledInput label="Postnr" value={form.zipCode} onChange={(v) => updateField('zipCode', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Telefon, nationell" value={form.telephone1} onChange={(v) => updateField('telephone1', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Telefon, utland" value={form.telephone2} onChange={(v) => updateField('telephone2', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Email" value={form.email} onChange={(v) => updateField('email', v)} labelWidth="w-45" margintop="0" maxLength={200} />
                                        <LabeledInput label="Web" value={form.web} onChange={(v) => updateField('web', v)} labelWidth="w-45" margintop="0" maxLength={200} />
                                        <LabeledInput label="Fakturamail, Swe" value={form.invoiceMailSWE} onChange={(v) => updateField('invoiceMailSWE', v)} labelWidth="w-45" margintop="0" maxLength={200} />
                                        <LabeledInput label="Fakturamail, Eng" value={form.invoiceMailENG} onChange={(v) => updateField('invoiceMailENG', v)} labelWidth="w-45" margintop="0" maxLength={200} />
                                        <LabeledInput label="Bank" value={form.bank} onChange={(v) => updateField('bank', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="BIC" value={form.bic} onChange={(v) => updateField('bic', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="IBAN" value={form.iban} onChange={(v) => updateField('iban', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Bankgiro" value={form.bg} onChange={(v) => updateField('bg', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="Postgiro" value={form.pg} onChange={(v) => updateField('pg', v)} labelWidth="w-45" margintop="0" maxLength={50} />
                                        <LabeledInput label="VAT-nummer" value={form.vatNr} onChange={(v) => updateField('vatNr', v)} labelWidth="w-45" margintop="0" maxLength={100} />
                                        <LabeledInput label="Sökväg till dokument" value={form.documentFileBasePath} onChange={(v) => updateField('documentFileBasePath', v)} labelWidth="w-45" margintop="0" maxLength={1000} />
                                    </span>

                                    <span>
                                        <LabeledInput label="Antal varsdagar, förfrågan" type="number" integerOnly value={form.nrOfInquiryAnswerDays} onChange={(v) => updateField('nrOfInquiryAnswerDays', v)} labelWidth="w-45" margintop="0" />
                                        <LabeledInput label="Företagsnamn" value={form.settingsCompany} onChange={(v) => updateField('settingsCompany', v)} labelWidth="w-45" margintop="0" maxLength={50} />

                                        <LabeledTextArea label="F-skattesedeltext" value={form.vatInfo} onChange={(v) => updateField('vatInfo', v)} labelWidth="w-45" margintop="2" inputWidth="w-full" height="h-24" maxLength={255} />
                                        <LabeledTextArea label="Kundmeddelande" value={form.defaultCustomerMessage} onChange={(v) => updateField('defaultCustomerMessage', v)} labelWidth="w-45" margintop="2" inputWidth="w-full" height="h-24" maxLength={255} />
                                        <LabeledTextArea label="Faktura EU-kund" value={form.euText} onChange={(v) => updateField('euText', v)} labelWidth="w-45" margintop="2" inputWidth="w-full" height="h-24" maxLength={255} />
                                        <LabeledTextArea label="Export-kund" value={form.exportText} onChange={(v) => updateField('exportText', v)} labelWidth="w-45" margintop="2" inputWidth="w-full" height="h-24" maxLength={255} />

                                        <LabeledInput label="Google API key" value={form.googleApiKey} onChange={(v) => updateField('googleApiKey', v)} labelWidth="w-45" margintop="3" maxLength={500} />
                                        <LabeledTextArea label="Mail wrapper" value={form.mailWrapper} onChange={(v) => updateField('mailWrapper', v)} labelWidth="w-45" margintop="2" inputWidth="w-full" height="h-32" maxLength={5000} />
                                        <LabeledInput label="Senaste fakturanr" type="number" integerOnly value={form.invoiceLastNr} onChange={(v) => updateField('invoiceLastNr', v)} labelWidth="w-45" margintop="0" />
                                    </span>
                                </div>
                            </span>
                        </div>
                    )}
                </div>

                <div className="flex flex-col w-80 shrink-0 border-l border-gray-300 pl-4 py-2 mb-5">
                    <h2 className="text-sm text-center text-gray-700 mt-1">Meddelanden</h2>
                    {!message ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            <li className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                                {message.text}
                            </li>
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
};

export default CompanyInfoSettings;
