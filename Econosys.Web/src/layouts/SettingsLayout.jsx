import { Outlet, NavLink, useLocation } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const SettingsLayout = () => {
    const location = useLocation();
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    const getNavLinkClass = (path) => {
        let sub = location.pathname.split('/')[2];
        if (!isNaN(sub)) {
            sub = "company-info";
        }
        if (!sub) {
            sub = "company-info";
        }
        if (sub === path) {
            return "text-red-600";
        }
        return "text-gray-600 hover:text-gray-950";
    };

    return (        
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <div className="sticky top-0 z-50">
                    <Navbar />
                </div>
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    {!isDetailPage && (
                    <div className="sticky top-[52px] h-[calc(100vh-52px)] overflow-y-auto flex flex-col w-50 shrink-0 border-r border-gray-300">
                        <ul className="flex flex-col pt-5">
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('company-info')}><NavLink to="company-info">Företagsinfo</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('users')}><NavLink to="users">Användare</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Register</p>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('constructions')}><NavLink to="constructions">Konstruktioner</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('materials')}><NavLink to="materials">Material</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('pallet-formats')}><NavLink to="pallet-formats">Palltyper</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('units')}><NavLink to="units">Enheter</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('currencies')}><NavLink to="currencies">Valutor</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('costs')}><NavLink to="costs">Kostnader</NavLink></li>

                            <li className={"text-xs font-semibold px-6 py-1.5 pt-3 " + getNavLinkClass('delivery-terms')}><NavLink to="delivery-terms">Leveransvillkor</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('payment-terms')}><NavLink to="payment-terms">Betalningsvillkor</NavLink></li>

                            <li className={"text-xs font-semibold px-6 py-1.5 pt-3 " + getNavLinkClass('inventories')}><NavLink to="inventories">Lager</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('transportorer')}><NavLink to="transportorer">Transportörer</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Transportprislistor</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">System</p>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('calculation-constants')}><NavLink to="calculation-constants">Kalkylkonstanter</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('email-settings')}><NavLink to="email-settings">Epostinställningar</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('email-texts')}><NavLink to="email-texts">Eposttexter</NavLink></li>
                            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('budget-month-distribution')}><NavLink to="budget-month-distribution">Budget, månadsfördelning</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Pallfaktorer</NavLink></li>
                        </ul>
                    </div>
                    )}
                    <div className="flex-grow pt-4 px-5 relative">
                        <Outlet />
                        {/* <PdfPanel /> */}
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default SettingsLayout