import { Outlet, NavLink } from "react-router-dom";
import Header from "../components/Header";
import Navbar from "../components/Navbar";
import bg from "../assets/content.png";

import { PdfProvider } from "../contexts/PdfContext";
import PdfPanel from "../components/PdfPanel";


const ReportingLayout = () => {
    return (
        <PdfProvider>
            <div className="flex flex-col min-h-screen">
                <Header />
                <Navbar />
                <div className="flex grow items-stretch bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    <div className="flex flex-col w-60 border-r border-gray-300">
                        <ul className="flex flex-col pt-5">
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance">ÖVERSIKT</NavLink></li>
                            {/* <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/searchinvoice">SÖK FAKTURA</NavLink></li> */}
                            {/* <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/order/inquiry">ÖPPNA</NavLink></li> */}
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Order</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Ordereingång</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Ej ordererkända beställningar</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/order/inquiry">Ej fakturerade ordererkännanden</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Logistik</p>
                            <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/inventory">Lagerrapport</NavLink></li>
                            <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/shelfwarmers">Hyllvärmare</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Ekonomi</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Lönsamhet</NavLink></li>
                            <li className="text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/reporting/invoicedpercustomer">Fakturerat mot kund</NavLink></li>
                            <p className="bg-gray-200 text-xs px-6 py-1.5 mt-3 mb-1">Excelexporter</p>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Uppföljning transportkalkyler</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Uppföljning fakturerade pallar</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Utfakturerade med vikter</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Kalkyler med lagring</NavLink></li>
                            <li className="opacity-50 pointer-events-none text-xs text-gray-600 hover:text-gray-950 font-semibold px-6 py-1.5"><NavLink to="/finance/invoicestoaccount">Fakturor med rader</NavLink></li>
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

export default ReportingLayout