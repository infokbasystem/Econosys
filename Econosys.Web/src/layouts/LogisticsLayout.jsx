import { Outlet, NavLink, useLocation } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const LogisticsLayout = () => {
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
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    {!isDetailPage && (
                        <div className="sticky top-[52px] h-[calc(100vh-52px)] overflow-y-auto flex flex-col w-50 border-r border-gray-300">
                            <ul className="flex flex-col pt-5">
                                <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/logistics">ÖVERSIKT</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Transportorderöversikt</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Avropsöversikt</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Transport</p>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Nytt avrop</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Nytt leverans</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Lager</p>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Inventering</NavLink></li>
                                <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/logistics/inventoryreport">Lagerrapport</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Hyllvärmare</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/nondeliveredwarehouseorders">Ej levererade lagerorder</NavLink></li>
                                <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Register</p>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Transportprislistor</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Transportörer</NavLink></li>
                                <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Lager</NavLink></li>
                            </ul>
                        </div>
                    )}
                    <div className="flex-grow pt-4 px-5 relative">
                        <Outlet />
                        <PdfPanel />
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default LogisticsLayout