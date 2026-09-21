import { useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import { BarChart3, CircleDollarSign, ClipboardList, Lightbulb, PanelLeftClose, PanelLeftOpen, ReceiptText } from "lucide-react";
import Header from "../components/Header";
import LeftMenu from "../components/LeftMenu";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";
import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const menuGroups = [
    {
        items: [{ to: "/management", label: "Översikt", icon: BarChart3, end: true }],
    },
    {
        label: "Ledning",
        items: [
            { to: "/reporting/profitability", label: "Lönsamhet", icon: CircleDollarSign, disabled: true },
            { to: "/reporting/invoiced", label: "Fakturerat", icon: ReceiptText, disabled: true },
        ],
    },
    {
        label: "Försäljning",
        items: [
            { label: "Budget", icon: CircleDollarSign, disabled: true },
            { label: "Säljrapport", icon: BarChart3, disabled: true },
        ],
    },
    {
        label: "Kvalitet",
        items: [
            { to: "/management/deviations", label: "Avvikelser", icon: ClipboardList },
            { to: "/management/improvement-propositions", label: "Förbättringsförslag", icon: Lightbulb },
            { to: "/management/supplier-report", label: "Leverantörsrapport", icon: BarChart3 },
            { label: "Kostnader", icon: CircleDollarSign, disabled: true },
        ],
    },
    // {
    //     label: "Register",
    //     items: [{ label: "Budget, månadsfördelning", icon: CircleDollarSign, disabled: true }],
    // },
];

const ManagementLayout = () => {
    const location = useLocation();
    const [menuOpenPath, setMenuOpenPath] = useState(null);
    const isMenuOpen = menuOpenPath === location.pathname;
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    const menuContent = <LeftMenu groups={menuGroups} />;

    return (
        <PdfProvider>
            <div className="relative flex h-screen min-h-0 flex-col overflow-y-auto bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                <div className="shrink-0">
                    <Header />
                </div>
                <div className="sticky top-0 z-50 shrink-0">
                    <Navbar />
                </div>
                <div className="relative min-h-0 grow overflow-hidden">
                    <div className="flex h-full min-h-0 items-stretch overflow-hidden md:px-[clamp(4px,3vw,3vw)]">
                        {!isDetailPage && (
                            <div className="mt-10 mb-20 flex min-h-0 w-50 shrink-0 flex-col overflow-y-auto border-r border-gray-300">
                                {menuContent}
                            </div>
                        )}

                        {isDetailPage && (
                            <>
                            <button
                                type="button"
                                onClick={() => setMenuOpenPath(location.pathname)}
                                className="absolute left-4 top-5 z-20 inline-flex h-7 w-7 items-center justify-center text-gray-700 shadow-sm hover:bg-white"
                                aria-label="Visa meny"
                            >
                                <PanelLeftOpen size={16} />
                            </button>

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
                                        <PanelLeftClose size={16} />
                                    </button>
                                </div>
                                {menuContent}
                            </div>
                            </>
                        )}

                        <div className="flex-grow min-w-0 pt-4 px-0 relative overflow-hidden">
                            <div className="outlet-leading-none h-full pt-0">
                                <Outlet />
                            </div>
                        </div>
                    </div>

                    <PdfPanel />
                </div>
            </div>
        </PdfProvider>
    );
};

export default ManagementLayout;