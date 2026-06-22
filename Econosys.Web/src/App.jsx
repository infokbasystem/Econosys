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
import ProductSearch from './pages/Order/ProductSearch'
import Product from './pages/Order/Product'
import InquirySearch from './pages/Order/InquirySearch'
import QuotationSearch from './pages/Order/QuotationSearch'

import FinanceOverview from './pages/Finance/FinanceOverview'
import InvoiceSearch from './pages/Finance/InvoiceSearch'
import Invoice from './pages/Finance/Invoice'
import OrderCostAttest from './pages/Finance/OrderCostAttest'
import InvoiceDeliveriesAndOrderCosts from './pages/Finance/InvoiceDeliveriesAndOrderCosts'

import LogisticsOverview from './pages/Logistics/LogisticsOverview'

import DeviationsOverview from './pages/Management/DeviationsOverview'
import Deviation from './pages/Management/Deviation'
import Inquiry from './pages/Management/Inquiry'
import Quotation from './pages/Management/Quotation'
import ImprovementPropositionsOverview from './pages/Management/ImprovementPropositionsOverview'

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

import CompanyInfoSettings from './pages/Settings/CompanyInfoSettings'
import ConstructionsSettings from './pages/Settings/ConstructionsSettings'
import MaterialsSettings from './pages/Settings/MaterialsSettings'
import UnitsSettings from './pages/Settings/UnitsSettings'
import PalletFormatsSettings from './pages/Settings/PalletFormatsSettings'
import CurrenciesSettings from './pages/Settings/CurrenciesSettings'
import CostsSettings from './pages/Settings/CostsSettings'
import UsersSettings from './pages/Settings/UsersSettings'
import DeliveryTermsSettings from './pages/Settings/DeliveryTermsSettings'
import PaymentTermsSettings from './pages/Settings/PaymentTermsSettings'
import InventoriesSettings from './pages/Settings/InventoriesSettings'
import ShippersSettings from './pages/Settings/ShippersSettings'
import CalculationConstantsSettings from './pages/Settings/CalculationConstantsSettings'
import EmailSettings from './pages/Settings/EmailSettings'
import EmailTextsSettings from './pages/Settings/EmailTextsSettings'
import BudgetMonthDistributionSettings from './pages/Settings/BudgetMonthDistributionSettings'

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
        <Route path="products" element={<ProtectedRoute><ProductSearch /></ProtectedRoute>} />
        <Route path="products/:id" element={<ProtectedRoute><Product /></ProtectedRoute>} />
        <Route path="quotations" element={<ProtectedRoute><QuotationSearch /></ProtectedRoute>} />
        <Route path="quotations/new" element={<ProtectedRoute><PdfEnabledPage><Quotation /></PdfEnabledPage></ProtectedRoute>} />
        <Route path="quotations/:id" element={<ProtectedRoute><PdfEnabledPage><Quotation /></PdfEnabledPage></ProtectedRoute>} />
        <Route path="inquiries" element={<ProtectedRoute><InquirySearch /></ProtectedRoute>} />
        <Route path="inquiries/new" element={<ProtectedRoute><PdfEnabledPage><Inquiry /></PdfEnabledPage></ProtectedRoute>} />
        <Route path="inquiries/:id" element={<ProtectedRoute><PdfEnabledPage><Inquiry /></PdfEnabledPage></ProtectedRoute>} />
      </Route>
      <Route path="logistics" element={< LogisticsLayout />}>
        <Route index element={<ProtectedRoute>< LogisticsOverview /></ProtectedRoute>} />
        <Route path="inventoryreport" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
      </Route>
      <Route path="management" element={< ManagementLayout />}>
        <Route index element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations" element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations/:id" element={<ProtectedRoute><PdfEnabledPage><Deviation /></PdfEnabledPage></ProtectedRoute>} />
        <Route path="improvement-propositions" element={<ProtectedRoute><ImprovementPropositionsOverview /></ProtectedRoute>} />
      </Route>
      <Route path="finance" element={< FinanceLayout />}>
        <Route index element={<ProtectedRoute>< FinanceOverview /></ProtectedRoute>} />
        <Route path="searchinvoice" element={<ProtectedRoute>< InvoiceSearch /></ProtectedRoute>} />
        <Route path="attest" element={<ProtectedRoute>< OrderCostAttest /></ProtectedRoute>} />
        <Route path="invoicedeliveriesandordercosts" element={<ProtectedRoute><InvoiceDeliveriesAndOrderCosts /></ProtectedRoute>} />
        <Route path="invoice/new" element={<ProtectedRoute><PdfEnabledPage><Invoice /></PdfEnabledPage></ProtectedRoute>} />
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
        <Route index element={<ProtectedRoute>< CompanyInfoSettings /></ProtectedRoute>} />
        <Route path="company-info" element={<ProtectedRoute>< CompanyInfoSettings /></ProtectedRoute>} />
        <Route path="constructions" element={<ProtectedRoute>< ConstructionsSettings /></ProtectedRoute>} />
        <Route path="materials" element={<ProtectedRoute>< MaterialsSettings /></ProtectedRoute>} />
        <Route path="units" element={<ProtectedRoute>< UnitsSettings /></ProtectedRoute>} />
        <Route path="pallet-formats" element={<ProtectedRoute>< PalletFormatsSettings /></ProtectedRoute>} />
        <Route path="currencies" element={<ProtectedRoute>< CurrenciesSettings /></ProtectedRoute>} />
        <Route path="costs" element={<ProtectedRoute>< CostsSettings /></ProtectedRoute>} />
        <Route path="users" element={<ProtectedRoute>< UsersSettings /></ProtectedRoute>} />
        <Route path="delivery-terms" element={<ProtectedRoute>< DeliveryTermsSettings /></ProtectedRoute>} />
        <Route path="payment-terms" element={<ProtectedRoute>< PaymentTermsSettings /></ProtectedRoute>} />
        <Route path="inventories" element={<ProtectedRoute>< InventoriesSettings /></ProtectedRoute>} />
        <Route path="transportorer" element={<ProtectedRoute>< ShippersSettings /></ProtectedRoute>} />
        <Route path="calculation-constants" element={<ProtectedRoute><CalculationConstantsSettings /></ProtectedRoute>} />
        <Route path="email-settings" element={<ProtectedRoute><EmailSettings /></ProtectedRoute>} />
        <Route path="email-texts" element={<ProtectedRoute><EmailTextsSettings /></ProtectedRoute>} />
        <Route path="budget-month-distribution" element={<ProtectedRoute><BudgetMonthDistributionSettings /></ProtectedRoute>} />
      </Route>
      <Route path="/login" element={<LoginPage />} />
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
