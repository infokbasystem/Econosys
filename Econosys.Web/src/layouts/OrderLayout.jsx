import { Outlet, NavLink, useLocation } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const OrderLayout = () => {
    const location = useLocation();
    const pathSegments = location.pathname.split('/').filter(Boolean);
    const hasIdLikeLastSegment = /^\d+$/.test(pathSegments[pathSegments.length - 1] || '');
    const isDetailPage = pathSegments.length >= 3 || (pathSegments.length === 2 && hasIdLikeLastSegment);

    return (        
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <Navbar />
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    {!isDetailPage && (
                    <div className="flex flex-col w-50 border-r border-gray-300">
                        <ul className="flex flex-col pt-5">
                            <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/order">ÖVERSIKT</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Kalkyl</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink >Kalkyler</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Produkter</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Order</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Ordererkännande</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Beställning</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Affärsförslag</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Förfrågan</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Register</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Kunder</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Levernatörer</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Material</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink>Konstruktioner</NavLink></li>
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

export default OrderLayout