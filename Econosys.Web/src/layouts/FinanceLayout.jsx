import { Outlet, NavLink } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";

const FinanceLayout = () => {
    return (        
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <Navbar />
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    <div className="flex flex-col w-60 border-r border-gray-300">
                        <ul className="flex flex-col pt-5">
                            <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance">ÖVERSIKT</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/searchinvoice">Sök faktura</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Fakturera</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Fakturera leveranser</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/order/inquiry">Skapa fristående faktura</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Bokföring</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Bokför fakturor</NavLink></li>
                        </ul>
                    </div>
                    <div className="flex-grow pt-4 px-5 relative">
                        <Outlet />
                        <PdfPanel />
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default FinanceLayout