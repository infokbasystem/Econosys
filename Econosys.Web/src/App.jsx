import { useState } from 'react'
import { Route, createBrowserRouter, createRoutesFromElements, RouterProvider } from 'react-router-dom'

import './App.css'
import LoginPage from './pages/LoginPage'
import { AuthProvider } from './contexts/AuthContext'
import ProtectedRoute from './components/ProtectedRoute'

import MainLayout from './layouts/MainLayout'
import OrderLayout from './layouts/OrderLayout'
import LogisticsLayout from './layouts/LogisticsLayout'
import ManagementLayout from './layouts/ManagementLayout'
import FinanceLayout from './layouts/FinanceLayout'
import ReportingLayout from './layouts/ReportingLayout'
import SettingsLayout from './layouts/SettingsLayout'

import Overview from './pages/Overview'

import OrderOverview from './pages/Order/OrderOverview'

import FinanceOverview from './pages/Finance/FinanceOverview'
import InvoiceSearch from './pages/Finance/InvoiceSearch'
import Invoice from './pages/Finance/Invoice'

import LogisticsOverview from './pages/Logistics/LogisticsOverview'

import DeviationsOverview from './pages/Management/DeviationsOverview'
import Deviation from './pages/Management/Deviation'

import ReportingOverview from './pages/Reporting/ReportingOverview'
import InventoryReport from './pages/Reporting/InventoryReport'
import MonthlyOrderCostsReport from './pages/Reporting/MonthlyOrderCostsReport'
import SlowMoversReport from './pages/Reporting/SlowMoversReport'
import PalletFollowupReport from './pages/Reporting/PalletFollowupReport'
import PackagingReport from './pages/Reporting/PackagingReport'
import RevenuePerOrder from './pages/Reporting/RevenuePerOrder'
import NonConfirmedSupplierOrders from './pages/Reporting/NonConfirmedSupplierOrders'
import NonInvoicedOrders from './pages/Reporting/NonInvoicedOrders'
import NonDeliveredWarehouseOrders from './pages/Reporting/NonDeliveredWarehouseOrders'

import SettingsOverview from './pages/Settings/LogisticsOverview'

import { PdfProvider } from './contexts/PdfContext';
import PdfPanel from './components/PdfPanel';

const PdfEnabledPage = ({ children }) => (
  <PdfProvider>
    {children}
    <PdfPanel />
  </PdfProvider>
);

const router = createBrowserRouter(
  createRoutesFromElements(
    <Route>
      <Route path="/" element={< MainLayout />}>
        <Route index element={<ProtectedRoute>< Overview /></ProtectedRoute>} />
      </Route>
      <Route path="order" element={< OrderLayout />}>
        <Route index element={<ProtectedRoute>< OrderOverview /></ProtectedRoute>} />
      </Route>
      <Route path="logistics" element={< LogisticsLayout />}>
        <Route index element={<ProtectedRoute>< LogisticsOverview /></ProtectedRoute>} />
        <Route path="inventoryreport" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
      </Route>
      <Route path="management" element={< ManagementLayout />}>
        <Route index element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations" element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations/:id" element={<ProtectedRoute><PdfEnabledPage><Deviation /></PdfEnabledPage></ProtectedRoute>} />
      </Route>
      <Route path="finance" element={< FinanceLayout />}>
        <Route index element={<ProtectedRoute>< FinanceOverview /></ProtectedRoute>} />
        <Route path="searchinvoice" element={<ProtectedRoute>< InvoiceSearch /></ProtectedRoute>} />
        <Route path="invoice/new" element={<ProtectedRoute><Invoice /></ProtectedRoute>} />
        <Route path="invoice/:id" element={<ProtectedRoute><PdfEnabledPage><Invoice /></PdfEnabledPage></ProtectedRoute>} />
      </Route>
      <Route path="reporting" element={< ReportingLayout />}>
        <Route index element={<ProtectedRoute>< ReportingOverview /></ProtectedRoute>} />
        <Route path="inventory" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
        <Route path="slowmovers" element={<ProtectedRoute>< SlowMoversReport /></ProtectedRoute>} />
        <Route path="ordercostmonth" element={<ProtectedRoute>< MonthlyOrderCostsReport /></ProtectedRoute>} />
        <Route path="palletfollowup" element={<ProtectedRoute>< PalletFollowupReport /></ProtectedRoute>} />
        <Route path="packagingreport" element={<ProtectedRoute>< PackagingReport /></ProtectedRoute>} />
        <Route path="revenueperorder" element={<ProtectedRoute>< RevenuePerOrder /></ProtectedRoute>} />
        <Route path="nonconfirmedsupplierorders" element={<ProtectedRoute><NonConfirmedSupplierOrders /></ProtectedRoute>} />
        <Route path="noninvoicedorders" element={<ProtectedRoute><NonInvoicedOrders /></ProtectedRoute>} />
        <Route path="nondeliveredwarehouseorders" element={<ProtectedRoute><NonDeliveredWarehouseOrders /></ProtectedRoute>} />
      </Route>
      <Route path="settings" element={< SettingsLayout />}>
        <Route index element={<ProtectedRoute>< SettingsOverview /></ProtectedRoute>} />
      </Route>
      <Route path="/login" element={<LoginPage />} />
      {/* Add more routes here */}
    </Route>
  )
)


function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}

export default App
