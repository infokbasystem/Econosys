import { Outlet } from 'react-router-dom'
import Header from '../components/Header'
import Navbar from '../components/Navbar'
import bg from '../assets/content.png'
import ReportingSubMenu from '../pages/Reporting/ReportingSubMenu'

import { PdfProvider } from '../contexts/PdfContext'
import PdfPanel from '../components/PdfPanel'

const ReportingLayout = () => {
    return (
        <PdfProvider>
            <div className="flex h-screen min-h-0 flex-col overflow-y-auto">
                <div className="shrink-0">
                    <Header />
                </div>
                <div className="sticky top-0 z-50 shrink-0">
                    <Navbar />
                </div>
                <div className="relative flex grow flex-col bg-gradient-to-br from-gray-50 via-gray-100 to-gray-50" style={{ backgroundImage: `url(${bg})` }}>
                    <ReportingSubMenu />
                    <div className="relative min-w-0 flex-grow">
                        <div className="outlet-leading-none px-5 pt-5">
                            <Outlet />
                        </div>
                        <PdfPanel />
                    </div>
                </div>
            </div>
        </PdfProvider>
    )
}

export default ReportingLayout