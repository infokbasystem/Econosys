import { useEffect, useState } from "react";
import { Outlet, NavLink, useLocation } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

const ManagementLayout = () => {
    const location = useLocation();
    const [isMenuOpen, setIsMenuOpen] = useState(false);
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

    useEffect(() => {
        setIsMenuOpen(false);
    }, [location.pathname]);

    const menuContent = (
        <ul className="flex flex-col pt-5">
            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('overview')}><NavLink to="/management" end>Översikt</NavLink></li>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/profitability">Lönsamhet</NavLink></li>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/invoiced">Fakturerat</NavLink></li>
            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Försäljning</p>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Budget</NavLink></li>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Säljrapport</NavLink></li>
            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Kvalitet</p>
            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('deviations')}><NavLink to="/management/deviations">Avvikelser</NavLink></li>
            <li className={"text-xs font-semibold px-6 py-1.5 " + getNavLinkClass('improvement-propositions')}><NavLink to="/management/improvement-propositions">Förbättringsförslag</NavLink></li>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Palluppföljning</NavLink></li>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Kostnader</NavLink></li>
            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Register</p>
            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Budget, månadsfördelning</NavLink></li>
        </ul>
    );

    return (
        <div className="flex flex-col min-h-screen">
            <Header />
            <div className="sticky top-0 z-50">
                <Navbar />
            </div>
            <div className="relative flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                {!isDetailPage && (
                    <div className="sticky top-[52px] h-[calc(100vh-52px)] overflow-y-auto flex flex-col w-50 shrink-0 border-r border-gray-300">
                        {menuContent}
                    </div>
                )}

                {isDetailPage && (
                    <>
                        <button
                            type="button"
                            onClick={() => setIsMenuOpen(true)}
                            className="absolute left-4 top-5 z-20 inline-flex h-7 w-7 items-center justify-center rounded border border-gray-300 bg-white/95 text-gray-700 shadow-sm hover:bg-white"
                            aria-label="Visa meny"
                        >
                            ≡
                        </button>

                        <div
                            className={`absolute inset-0 z-30 bg-black/20 transition-opacity ${isMenuOpen ? 'opacity-100 pointer-events-auto' : 'opacity-0 pointer-events-none'}`}
                            onClick={() => setIsMenuOpen(false)}
                        />

                        <div
                            className={`absolute left-0 top-0 z-40 flex h-full w-50 shrink-0 flex-col border-r border-gray-300 bg-gray-50 transition-transform duration-200 ${isMenuOpen ? 'translate-x-0' : '-translate-x-full'}`}
                        >
                            <div className="flex items-center justify-end border-b border-gray-200 px-2 py-1">
                                <button
                                    type="button"
                                    onClick={() => setIsMenuOpen(false)}
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
                    <Outlet />
                </div>
            </div>
        </div>
    )
}

export default ManagementLayout