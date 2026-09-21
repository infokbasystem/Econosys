import { Outlet, useLocation } from "react-router-dom";
import { Boxes, Building2, Calculator, Coins, CreditCard, Mail, Package, Settings, Truck, Users } from "lucide-react";
import Header from "../components/Header";
import LeftMenu from "../components/LeftMenu";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const menuGroups = [
    { items: [{ to: "company-info", label: "Företagsinfo", icon: Building2, activePaths: ["/settings", "/settings/company-info"] }, { to: "users", label: "Användare", icon: Users }] },
    { label: "Register", items: [{ to: "constructions", label: "Konstruktioner", icon: Calculator }, { to: "materials", label: "Material", icon: Boxes }, { to: "pallet-formats", label: "Palltyper", icon: Package }, { to: "units", label: "Enheter", icon: Settings }, { to: "currencies", label: "Valutor", icon: Coins }, { to: "costs", label: "Kostnader", icon: CreditCard }, { to: "delivery-terms", label: "Leveransvillkor", icon: Truck }, { to: "payment-terms", label: "Betalningsvillkor", icon: CreditCard }, { to: "inventories", label: "Lager", icon: Package }, { to: "transportorer", label: "Transportörer", icon: Truck }, { label: "Transportprislistor", icon: CreditCard, disabled: true }] },
    { label: "System", items: [{ to: "calculation-constants", label: "Kalkylkonstanter", icon: Calculator }, { to: "email-settings", label: "Epostinställningar", icon: Mail }, { to: "email-texts", label: "Eposttexter", icon: Mail }, { to: "budget-month-distribution", label: "Budget, månadsfördelning", icon: Coins }, { label: "Pallfaktorer", icon: Package, disabled: true }] },
];

const SettingsLayout = () => {
    const location = useLocation();
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    return (        
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <div className="sticky top-0 z-50">
                    <Navbar />
                </div>
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50 md:px-[clamp(4px,3vw,3vw)]" style={{ backgroundImage: `url(${bg})` }}>
                    {!isDetailPage && (
                    <div className="sticky top-[52px] mt-10 mb-20 overflow-y-auto flex flex-col w-50 shrink-0 border-r border-gray-300">
                        <LeftMenu groups={menuGroups} />
                    </div>
                    )}
                    <div className="flex-grow pt-4 px-5 relative">
                        <div className="outlet-leading-none h-full pt-5">
                            <Outlet />
                        </div>
                        {/* <PdfPanel /> */}
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default SettingsLayout