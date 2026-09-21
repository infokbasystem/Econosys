import { createContext, useContext, useMemo, useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import {
    Boxes,
    Calculator,
    ClipboardCheck,
    Factory,
    FileText,
    LayoutDashboard,
    Package,
    Send,
    ShoppingCart,
    Users,
} from "lucide-react";
import Header from "../components/Header";
import LeftMenu from "../components/LeftMenu";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const OrderMenuContext = createContext(null);

// Lets pages under this layout show/hide the sliding order register menu themselves.
export const useOrderMenu = () => useContext(OrderMenuContext);


const menuGroups = [
    {
        items: [
            { to: "/order", label: "Översikt", icon: LayoutDashboard, end: true },
        ],
    },
    {
        label: "Kalkyl",
        items: [
            { label: "Kalkyler", icon: Calculator, disabled: true },
            { to: "/order/products", label: "Produkter", icon: Package },
        ],
    },
    {
        label: "Order",
        items: [
            { to: "/order/inquiries", label: "Förfrågningar", icon: Send },
            { to: "/order/quotations", label: "Affärsförslag", icon: FileText },
            { to: "/order/supplierorders", label: "Beställningar", icon: ShoppingCart },
            { to: "/order/customerorders", label: "Ordererkännanden", icon: ClipboardCheck },
        ],
    },
    {
        label: "Register",
        items: [
            { to: "/order/customers", label: "Kunder", icon: Users },
            { to: "/order/suppliers", label: "Leverantörer", icon: Factory },
            { to: "/order/materials", label: "Material", icon: Boxes },
            { to: "/order/constructions", label: "Konstruktioner", icon: Calculator },
        ],
    },
];

const OrderLayout = () => {
    const location = useLocation();
    const [menuOpenPath, setMenuOpenPath] = useState(null);
    const isMenuOpen = menuOpenPath === location.pathname;
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    const orderMenuContextValue = useMemo(() => ({
        isMenuOpen,
        openMenu: () => setMenuOpenPath(location.pathname),
        closeMenu: () => setMenuOpenPath(null),
        toggleMenu: () => setMenuOpenPath((prev) => (prev === location.pathname ? null : location.pathname)),
    }), [isMenuOpen, location.pathname]);

    const menuContent = <LeftMenu groups={menuGroups} />;

    return (
        <PdfProvider>
            <div className="flex h-screen min-h-0 flex-col overflow-y-auto bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                <div className="shrink-0">
                    <Header />
                </div>
                <div className="sticky top-0 z-50 shrink-0">
                    <Navbar />
                </div>
                <div className="relative flex min-h-0 grow items-stretch md:px-[clamp(4px,3vw,3vw)] overflow-hidden">
                    {!isDetailPage && (
                        <div className="flex min-h-0 w-50 mt-10 mb-20 shrink-0 flex-col overflow-y-auto border-r border-gray-300">
                            {menuContent}
                        </div>
                    )}

                    {isDetailPage && (
                        <>
                            <div
                                className={`absolute inset-0 z-30 bg-black/20 transition-opacity ${isMenuOpen ? 'opacity-100 pointer-events-auto' : 'opacity-0 pointer-events-none'}`}
                                onClick={() => setMenuOpenPath(null)}
                            />

                            <div
                                className={`absolute left-0 top-0 z-40 flex h-full w-50 shrink-0 flex-col border-r border-gray-300 bg-gray-50 transition-transform duration-200 ${isMenuOpen ? 'translate-x-0' : '-translate-x-full'}`}
                            >
                                <div className="flex items-center justify-end border-b border-gray-200 px-2 py-1">
                                    <button
                                        type="button"
                                        onClick={() => setMenuOpenPath(null)}
                                        className="inline-flex h-6 w-6 items-center justify-center rounded text-gray-600 hover:bg-gray-200"
                                        aria-label="Dölj meny"
                                    >
                                        ×
                                    </button>
                                </div>
                                {menuContent}
                            </div>
                        </>
                    )}

                    <div className="flex-grow min-w-0 pt-4 px-0 relative overflow-hidden">
                        <div className="outlet-leading-none h-full pt-0">
                            <OrderMenuContext.Provider value={orderMenuContextValue}>
                                <Outlet />
                            </OrderMenuContext.Provider>
                        </div>
                    </div>

                    <PdfPanel />
                </div>
            </div>
        </PdfProvider>
    )
}

export default OrderLayout