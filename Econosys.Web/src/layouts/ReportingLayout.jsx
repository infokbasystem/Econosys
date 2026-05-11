import { Outlet, NavLink, useLocation } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";


const ReportingLayout = () => {
    const location = useLocation();
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    const getNavLinkClass = (path) => {
        let sub = location.pathname.split('/')[2];
        if (!isNaN(sub))
            sub = "overview";
        if (!sub)
            sub = "overview";
        if (sub == path)
            return "text-red-600";
        return "text-gray-600 hover:text-gray-950";
    };

    return (
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <Navbar />
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    {!isDetailPage && (
                        <div className="flex flex-col w-50 shrink-0 border-r border-gray-300">
                            <ul className="flex flex-col pt-5">
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance">Översikt</NavLink></li>
                                {/* <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('overview')}><NavLink to="/finance/searchinvoice">SÖK FAKTURA</NavLink></li> */}
                                {/* <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/order/inquiry">ÖPPNA</NavLink></li> */}
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Ledning</p>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/newcustomers">Nya kunder (senare när sales/budget görs)</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/co2tonneskm">Co2 och TonKm (senare projekt, kräver omtänk på på produkt)</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Order</p>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('revenueperorder')}><NavLink to="/reporting/revenueperorder">Intäkt per order</NavLink></li>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('nonconfirmedsupplierorders')}><NavLink to="/reporting/nonconfirmedsupplierorders">Ej ordererkända beställningar</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/handlingtime">Hanteringstid</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Logistik</p>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('inventory')}><NavLink to="/reporting/inventory">Lagerrapport</NavLink></li>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('slowmovers')}><NavLink to="/reporting/slowmovers">Hyllvärmare</NavLink></li>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('nondeliveredwarehouseorders')}><NavLink to="/reporting/nondeliveredwarehouseorders">Ej inlevererade lagerorder</NavLink></li>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('packagingreport')}><NavLink to="/reporting/packagingreport">Förpackningsrapport</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Ekonomi</p>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('noninvoicedorders')}><NavLink to="/reporting/noninvoicedorders">Ej fullt fakturerade order</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/invoicedpercustomer">Fakturerat per kund</NavLink></li>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('palletfollowup')}><NavLink to="/reporting/palletfollowup">Palluppföljning</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Månadsrapportering</p>
                                <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('ordercostmonth')}><NavLink to="/reporting/ordercostmonth">Verktygskostnader</NavLink></li>
                            </ul>
                        </div>
                    )}
                    <div className="flex-grow min-w-0 pt-4 px-5 relative overflow-hidden">
                        <Outlet />
                        <PdfPanel />
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default ReportingLayout