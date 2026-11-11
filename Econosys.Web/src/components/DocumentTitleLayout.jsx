import { useCallback, useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import DocumentTitleContext from '../contexts/DocumentTitleContext';

const routeTitles = {
    '': 'Min sida',
    login: 'Inloggning',
    order: 'Ej bekräftade beställningar',
    calculations: 'Kalkyler',
    products: 'Produkter',
    materials: 'Material',
    constructions: 'Konstruktioner',
    customers: 'Kunder',
    suppliers: 'Leverantörer',
    inquiries: 'Förfrågningar',
    quotations: 'Affärsförslag',
    supplierorders: 'Beställningar',
    customerorders: 'Ordererkännanden',
    logistics: 'Logistik',
    transportorderoverivew: 'Transportorder',
    transportorder: 'Transportorder',
    calloffoverview: 'Avrop',
    calloff: 'Avrop',
    newdelivery: 'Ny leverans',
    deliveries: 'Leveranser',
    stocktakings: 'Inventering',
    registers: 'Register',
    warehouses: 'Lager',
    transporters: 'Transportörer',
    pallettypes: 'Palltyper',
    palletfactors: 'Pallfaktorer',
    'pallet-factors': 'Pallfaktorer',
    'pallet-formats': 'Pallformat',
    transportpricelists: 'Transportprislistor',
    inventories: 'Lager',
    'delivery-terms': 'Leveransvillkor',
    shippers: 'Speditörer',
    inventoryreport: 'Lager',
    management: 'Avvikelser',
    deviations: 'Avvikelser',
    'improvement-propositions': 'Förbättringsförslag',
    'supplier-report': 'Leverantörsrapport',
    finance: 'Ekonomi',
    searchinvoice: 'Fakturasökning',
    invoice: 'Faktura',
    attest: 'Attest',
    invoicedeliveriesandordercosts: 'Fakturaleveranser och orderkostnader',
    invoicestoaccount: 'Fakturor till bokföring',
    customerstojeeves: 'Kunder till Jeeves',
    costs: 'Kostnader',
    currencies: 'Valutor',
    budget: 'Budget',
    salesreport: 'Försäljningsrapport',
    'budget-month-distribution': 'Budget, månadsfördelning',
    reporting: 'Rapporter',
    overview: 'Översikt',
    nonconfirmedsupplierorders: 'Ej bekräftade beställningar',
    noninvoicedorders: 'Ej fakturerade order',
    nondeliveredwarehouseorders: 'Ej levererade lagerorder',
    'handlingtimes-data': 'Ledtider',
    'handlingtimes-created-by': 'Ledtider per användare',
    'handlingtimes-new-orders': 'Ledtider för nya order',
    'handlingtimes-repeat-orders': 'Ledtider för ombeställningar',
    'handlingtimes-settings': 'Inställningar för ledtider',
    invoicedarticles: 'Fakturerade artiklar',
    ordercostmonth: 'Orderkostnader per månad',
    packagingreport: 'Förpackningsrapport',
    palletfollowup: 'Palluppföljning',
    revenueperorder: 'Omsättning per order',
    slowmovers: 'Långsamt omsatta artiklar',
    authorities: 'Myndigheter',
    environment: 'Miljö',
    other: 'Övriga rapporter',
    settings: 'Inställningar',
    'company-info': 'Företagsinformation',
    units: 'Enheter',
    users: 'Användare',
    'calculation-constants': 'Kalkylkonstanter',
    'email-settings': 'E-postinställningar',
    'email-texts': 'E-posttexter',
    'payment-terms': 'Betalningsvillkor',
    transportorer: 'Speditörer',
    oversikt: 'Översikt',
    ekonomi: 'Ekonomi',
    lager: 'Lager',
    inventory: 'Lager',
    miljo: 'Miljö',
    myndigheter: 'Myndigheter',
    ovriga: 'Övriga rapporter',
};

const humanizeSegment = (segment) => {
    const text = segment.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[-_]/g, ' ');
    return text.charAt(0).toLocaleUpperCase('sv-SE') + text.slice(1);
};

const getRouteTitle = (pathname) => {
    const segments = pathname.split('/').filter(Boolean);
    const lastSegment = segments.at(-1);
    const contentSegments = segments.filter((segment) => segment !== 'new' && !/^\d+$/.test(segment));
    const pageSegment = contentSegments.at(-1);

    if (lastSegment === 'new') {
        const pageTitle = routeTitles[pageSegment] || humanizeSegment(pageSegment || '');
        return `Ny ${pageTitle.toLocaleLowerCase('sv-SE')}`;
    }

    if (!pageSegment) return routeTitles[''];
    return routeTitles[pageSegment] || humanizeSegment(pageSegment);
};

const DocumentTitleLayout = () => {
    const location = useLocation();
    const [override, setOverride] = useState(null);
    const setPageTitle = useCallback((key, title) => {
        setOverride({ key, pathname: location.pathname, title });
    }, [location.pathname]);
    const clearPageTitle = useCallback((key) => {
        setOverride((current) => (current?.key === key ? null : current));
    }, []);
    const title = override?.pathname === location.pathname ? override.title : getRouteTitle(location.pathname);

    useEffect(() => {
        document.title = title || 'Econosys';
    }, [title]);

    return (
        <DocumentTitleContext.Provider value={{ setPageTitle, clearPageTitle }}>
            <Outlet />
        </DocumentTitleContext.Provider>
    );
};

export default DocumentTitleLayout;
