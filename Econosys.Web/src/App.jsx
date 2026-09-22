import { Navigate, Route, createBrowserRouter, createRoutesFromElements, RouterProvider } from 'react-router-dom'

import './App.css'
import LoginPage from './pages/LoginPage'
import { AuthProvider, useAuth } from './contexts/AuthContext'
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
import CustomerSearch from './pages/Order/CustomerSearch'
import SuppliersOverview from './pages/Order/SuppliersOverview'
import Customer from './pages/Order/Customer'
import Supplier from './pages/Order/Supplier'
import InquirySearch from './pages/Order/InquirySearch'
import QuotationSearch from './pages/Order/QuotationSearch'
import SupplierOrderSearch from './pages/Order/SupplierOrderSearch'
import CustomerOrderSearch from './pages/Order/CustomerOrderSearch'

import FinanceOverview from './pages/Finance/FinanceOverview'
import InvoiceSearch from './pages/Finance/InvoiceSearch'
import Invoice from './pages/Finance/Invoice'
import OrderCostAttest from './pages/Finance/OrderCostAttest'
import InvoiceDeliveriesAndOrderCosts from './pages/Finance/InvoiceDeliveriesAndOrderCosts'
import InvoicesToAccount from './pages/Finance/InvoicesToAccount'
import CustomersToJeeves from './pages/Finance/CustomersToJeeves'

import TransportOrderOverview from './pages/Logistics/TransportOrderOverview'
import TransportOrder from './pages/Logistics/TransportOrder'
import CalloffOverview from './pages/Logistics/CalloffOverview'
import Calloff from './pages/Logistics/Calloff'
import NewDelivery from './pages/Logistics/NewDelivery'
import Deliveries from './pages/Logistics/Deliveries'
import StockTakings from './pages/Logistics/StockTakings'
import RegistersLayout from './pages/Logistics/Registers/RegistersLayout'
import Warehouses from './pages/Logistics/Registers/Warehouses'
import Transporters from './pages/Logistics/Registers/Transporters'
import TransportPricelists from './pages/Logistics/Registers/TransportPricelists'
import PalletTypes from './pages/Logistics/Registers/PalletTypes'
import PalletFactors from './pages/Logistics/Registers/PalletFactors'

import DeviationsOverview from './pages/Management/DeviationsOverview'
import Deviation from './pages/Management/Deviation'
import Inquiry from './pages/Management/Inquiry'
import Quotation from './pages/Management/Quotation'
import SupplierOrder from './pages/Management/SupplierOrder'
import CustomerOrder from './pages/Management/CustomerOrder'
import ImprovementPropositionsOverview from './pages/Management/ImprovementPropositionsOverview'
import SupplierReport from './pages/Management/SupplierReport'

import ReportingOverview from './pages/Reporting/Overview/ReportingOverview'
import OrderReportingLayout from './pages/Reporting/Order/OrderReportingLayout'
import FinanceReportingLayout from './pages/Reporting/Finance/FinanceReportingLayout'
import FinanceReportingOverview from './pages/Reporting/Finance/FinanceReportingOverview'
import LogisticsReportingLayout from './pages/Reporting/Logistics/LogisticsReportingLayout'
import LogisticsReportingOverview from './pages/Reporting/Logistics/LogisticsReportingOverview'
import EnvironmentReportingLayout from './pages/Reporting/Environment/EnvironmentReportingLayout'
import EnvironmentReportingOverview from './pages/Reporting/Environment/EnvironmentReportingOverview'
import AuthoritiesReportingLayout from './pages/Reporting/Authorities/AuthoritiesReportingLayout'
import AuthoritiesReportingOverview from './pages/Reporting/Authorities/AuthoritiesReportingOverview'
import InventoryReport from './pages/Reporting/Logistics/InventoryReport'
import MonthlyOrderCostsReport from './pages/Reporting/Finance/MonthlyOrderCostsReport'
import InvoicedArticles from './pages/Reporting/Finance/InvoicedArticles'
import SlowMoversReport from './pages/Reporting/Logistics/SlowMoversReport'
import PalletFollowupReport from './pages/Reporting/Logistics/PalletFollowupReport'
import PackagingReport from './pages/Reporting/Logistics/PackagingReport'
import RevenuePerOrder from './pages/Reporting/Finance/RevenuePerOrder'
import NonConfirmedSupplierOrders from './pages/Reporting/Order/NonConfirmedSupplierOrders'
import NonInvoicedOrders from './pages/Reporting/Order/NonInvoicedOrders'
import NonDeliveredWarehouseOrders from './pages/Reporting/Logistics/NonDeliveredWarehouseOrders'
import HandlingTimesSettings from './pages/Reporting/Order/HandlingTimesSettings'
import HandlingTimesData from './pages/Reporting/Order/HandlingTimesData'
import HandlingTimesRepeatOrders from './pages/Reporting/Order/HandlingTimesRepeatOrders'
import HandlingTimesNewOrders from './pages/Reporting/Order/HandlingTimesNewOrders'
import HandlingTimesCreatedBy from './pages/Reporting/Order/HandlingTimesCreatedBy'

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
        <Route path="materials" element={<ProtectedRoute><MaterialsSettings /></ProtectedRoute>} />
        <Route path="constructions" element={<ProtectedRoute><ConstructionsSettings /></ProtectedRoute>} />
        <Route path="customers" element={<ProtectedRoute><CustomerSearch /></ProtectedRoute>} />
        <Route path="suppliers" element={<ProtectedRoute><SuppliersOverview /></ProtectedRoute>} />
        <Route path="customers/new" element={<ProtectedRoute><Customer /></ProtectedRoute>} />
        <Route path="customers/:id" element={<ProtectedRoute><Customer /></ProtectedRoute>} />
        <Route path="suppliers/new" element={<ProtectedRoute><Supplier /></ProtectedRoute>} />
        <Route path="suppliers/:id" element={<ProtectedRoute><Supplier /></ProtectedRoute>} />
        <Route path="quotations" element={<ProtectedRoute><QuotationSearch /></ProtectedRoute>} />
        <Route path="supplierorders" element={<ProtectedRoute><SupplierOrderSearch /></ProtectedRoute>} />
        <Route path="customerorders" element={<ProtectedRoute><CustomerOrderSearch /></ProtectedRoute>} />
        <Route path="supplierorders/new" element={<ProtectedRoute><SupplierOrder /></ProtectedRoute>} />
        <Route path="supplierorders/:id" element={<ProtectedRoute><SupplierOrder /></ProtectedRoute>} />
        <Route path="customerorders/new" element={<ProtectedRoute><CustomerOrder /></ProtectedRoute>} />
        <Route path="customerorders/:id" element={<ProtectedRoute><CustomerOrder /></ProtectedRoute>} />
        <Route path="quotations/new" element={<ProtectedRoute><Quotation /></ProtectedRoute>} />
        <Route path="quotations/:id" element={<ProtectedRoute><Quotation /></ProtectedRoute>} />
        <Route path="inquiries" element={<ProtectedRoute><InquirySearch /></ProtectedRoute>} />
        <Route path="inquiries/new" element={<ProtectedRoute><Inquiry /></ProtectedRoute>} />
        <Route path="inquiries/:id" element={<ProtectedRoute><Inquiry /></ProtectedRoute>} />
      </Route>
      <Route path="logistics" element={< LogisticsLayout />}>
        <Route index element={<ProtectedRoute><Navigate to="transportorderoverivew" replace /></ProtectedRoute>} />
        <Route path="transportorderoverivew" element={<ProtectedRoute>< TransportOrderOverview /></ProtectedRoute>} />
        <Route path="transportorder/:id" element={<ProtectedRoute>< TransportOrder /></ProtectedRoute>} />
        <Route path="calloffoverview" element={<ProtectedRoute>< CalloffOverview /></ProtectedRoute>} />
        <Route path="calloff/new" element={<ProtectedRoute>< Calloff /></ProtectedRoute>} />
        <Route path="calloff/:id" element={<ProtectedRoute>< Calloff /></ProtectedRoute>} />
        <Route path="newdelivery" element={<ProtectedRoute>< NewDelivery /></ProtectedRoute>} />
        <Route path="deliveries" element={<ProtectedRoute>< Deliveries /></ProtectedRoute>} />
        <Route path="stocktakings" element={<ProtectedRoute>< StockTakings /></ProtectedRoute>} />
        <Route path="registers" element={<ProtectedRoute>< RegistersLayout /></ProtectedRoute>}>
          <Route index element={<Navigate to="warehouses" replace />} />
          <Route path="warehouses" element={<ProtectedRoute>< Warehouses /></ProtectedRoute>} />
          <Route path="transporters" element={<ProtectedRoute>< Transporters /></ProtectedRoute>} />
          <Route path="transportpricelists" element={<ProtectedRoute>< TransportPricelists /></ProtectedRoute>} />
          <Route path="pallettypes" element={<ProtectedRoute>< PalletTypes /></ProtectedRoute>} />
          <Route path="palletfactors" element={<ProtectedRoute>< PalletFactors /></ProtectedRoute>} />
        </Route>
        <Route path="inventoryreport" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
      </Route>
      <Route path="management" element={< ManagementLayout />}>
        <Route index element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations" element={<ProtectedRoute><DeviationsOverview /></ProtectedRoute>} />
        <Route path="deviations/:id" element={<ProtectedRoute><Deviation /></ProtectedRoute>} />
        <Route path="improvement-propositions" element={<ProtectedRoute><ImprovementPropositionsOverview /></ProtectedRoute>} />
        <Route path="supplier-report" element={<ProtectedRoute><SupplierReport /></ProtectedRoute>} />
      </Route>
      <Route path="finance" element={< FinanceLayout />}>
        <Route index element={<ProtectedRoute>< FinanceOverview /></ProtectedRoute>} />
        <Route path="searchinvoice" element={<ProtectedRoute>< InvoiceSearch /></ProtectedRoute>} />
        <Route path="attest" element={<ProtectedRoute>< OrderCostAttest /></ProtectedRoute>} />
        <Route path="invoicedeliveriesandordercosts" element={<ProtectedRoute><InvoiceDeliveriesAndOrderCosts /></ProtectedRoute>} />
        <Route path="invoicestoaccount" element={<ProtectedRoute><InvoicesToAccount /></ProtectedRoute>} />
        <Route path="customerstojeeves" element={<ProtectedRoute><CustomersToJeeves /></ProtectedRoute>} />
        <Route path="invoice/new" element={<ProtectedRoute><Invoice /></ProtectedRoute>} />
        <Route path="invoice/:id" element={<ProtectedRoute><Invoice /></ProtectedRoute>} />
        <Route path="costs" element={<ProtectedRoute><CostsSettings /></ProtectedRoute>} />
        <Route path="currencies" element={<ProtectedRoute><CurrenciesSettings /></ProtectedRoute>} />
      </Route>
      <Route path="reporting" element={< ReportingLayout />}>
        <Route index element={<ProtectedRoute><Navigate to="overview" replace /></ProtectedRoute>} />
        <Route path="overview" element={<ProtectedRoute>< ReportingOverview /></ProtectedRoute>} />

        <Route path="order" element={<ProtectedRoute>< OrderReportingLayout /></ProtectedRoute>}>
          <Route index element={<ProtectedRoute><Navigate to="nonconfirmedsupplierorders" replace /></ProtectedRoute>} />
          <Route path="nonconfirmedsupplierorders" element={<ProtectedRoute><NonConfirmedSupplierOrders /></ProtectedRoute>} />
          <Route path="noninvoicedorders" element={<ProtectedRoute><NonInvoicedOrders /></ProtectedRoute>} />
          <Route path="handlingtimes-data" element={<ProtectedRoute><HandlingTimesData /></ProtectedRoute>} />
          <Route path="handlingtimes-repeat-orders" element={<ProtectedRoute><HandlingTimesRepeatOrders /></ProtectedRoute>} />
          <Route path="handlingtimes-new-orders" element={<ProtectedRoute><HandlingTimesNewOrders /></ProtectedRoute>} />
          <Route path="handlingtimes-created-by" element={<ProtectedRoute><HandlingTimesCreatedBy /></ProtectedRoute>} />
          <Route path="handlingtimes-settings" element={<ProtectedRoute><HandlingTimesSettings /></ProtectedRoute>} />
        </Route>

        <Route path="finance" element={<ProtectedRoute>< FinanceReportingLayout /></ProtectedRoute>}>
          <Route index element={<ProtectedRoute>< FinanceReportingOverview /></ProtectedRoute>} />
          <Route path="ordercostmonth" element={<ProtectedRoute>< MonthlyOrderCostsReport /></ProtectedRoute>} />
          <Route path="revenueperorder" element={<ProtectedRoute>< RevenuePerOrder /></ProtectedRoute>} />
          <Route path="invoicedarticles" element={<ProtectedRoute>< InvoicedArticles /></ProtectedRoute>} />
        </Route>

        <Route path="logistics" element={<ProtectedRoute>< LogisticsReportingLayout /></ProtectedRoute>}>
          <Route index element={<ProtectedRoute><Navigate to="inventory" replace /></ProtectedRoute>} />
          <Route path="inventory" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
          <Route path="slowmovers" element={<ProtectedRoute>< SlowMoversReport /></ProtectedRoute>} />
          <Route path="palletfollowup" element={<ProtectedRoute>< PalletFollowupReport /></ProtectedRoute>} />
          <Route path="packagingreport" element={<ProtectedRoute>< PackagingReport /></ProtectedRoute>} />
          <Route path="nondeliveredwarehouseorders" element={<ProtectedRoute><NonDeliveredWarehouseOrders /></ProtectedRoute>} />
          <Route path="overview" element={<ProtectedRoute>< LogisticsReportingOverview /></ProtectedRoute>} />
        </Route>

        <Route path="environment" element={<ProtectedRoute>< EnvironmentReportingLayout /></ProtectedRoute>}>
          <Route index element={<ProtectedRoute>< EnvironmentReportingOverview /></ProtectedRoute>} />
        </Route>

        <Route path="authorities" element={<ProtectedRoute>< AuthoritiesReportingLayout /></ProtectedRoute>}>
          <Route index element={<ProtectedRoute>< AuthoritiesReportingOverview /></ProtectedRoute>} />
        </Route>

        <Route path="slowmovers" element={<ProtectedRoute><Navigate to="logistics/slowmovers" replace /></ProtectedRoute>} />
        <Route path="ordercostmonth" element={<ProtectedRoute><Navigate to="finance/ordercostmonth" replace /></ProtectedRoute>} />
        <Route path="palletfollowup" element={<ProtectedRoute><Navigate to="logistics/palletfollowup" replace /></ProtectedRoute>} />
        <Route path="packagingreport" element={<ProtectedRoute><Navigate to="logistics/packagingreport" replace /></ProtectedRoute>} />
        <Route path="revenueperorder" element={<ProtectedRoute><Navigate to="finance/revenueperorder" replace /></ProtectedRoute>} />
        <Route path="nonconfirmedsupplierorders" element={<ProtectedRoute><Navigate to="order/nonconfirmedsupplierorders" replace /></ProtectedRoute>} />
        <Route path="noninvoicedorders" element={<ProtectedRoute><Navigate to="order/noninvoicedorders" replace /></ProtectedRoute>} />
        <Route path="nondeliveredwarehouseorders" element={<ProtectedRoute><Navigate to="logistics/nondeliveredwarehouseorders" replace /></ProtectedRoute>} />
        <Route path="oversikt" element={<ProtectedRoute><Navigate to="overview" replace /></ProtectedRoute>} />
        <Route path="ekonomi" element={<ProtectedRoute><Navigate to="finance" replace /></ProtectedRoute>} />
        <Route path="lager" element={<ProtectedRoute><Navigate to="logistics" replace /></ProtectedRoute>} />
        <Route path="inventory" element={<ProtectedRoute><Navigate to="logistics" replace /></ProtectedRoute>} />
        <Route path="other/inventory" element={<ProtectedRoute><Navigate to="logistics/inventory" replace /></ProtectedRoute>} />
        <Route path="other/slowmovers" element={<ProtectedRoute><Navigate to="logistics/slowmovers" replace /></ProtectedRoute>} />
        <Route path="other/palletfollowup" element={<ProtectedRoute><Navigate to="logistics/palletfollowup" replace /></ProtectedRoute>} />
        <Route path="other/packagingreport" element={<ProtectedRoute><Navigate to="logistics/packagingreport" replace /></ProtectedRoute>} />
        <Route path="other/nondeliveredwarehouseorders" element={<ProtectedRoute><Navigate to="logistics/nondeliveredwarehouseorders" replace /></ProtectedRoute>} />
        <Route path="other/ordercostmonth" element={<ProtectedRoute><Navigate to="finance/ordercostmonth" replace /></ProtectedRoute>} />
        <Route path="other/revenueperorder" element={<ProtectedRoute><Navigate to="finance/revenueperorder" replace /></ProtectedRoute>} />
        <Route path="other/nonconfirmedsupplierorders" element={<ProtectedRoute><Navigate to="order/nonconfirmedsupplierorders" replace /></ProtectedRoute>} />
        <Route path="other/noninvoicedorders" element={<ProtectedRoute><Navigate to="order/noninvoicedorders" replace /></ProtectedRoute>} />
        <Route path="miljo" element={<ProtectedRoute><Navigate to="environment" replace /></ProtectedRoute>} />
        <Route path="myndigheter" element={<ProtectedRoute><Navigate to="authorities" replace /></ProtectedRoute>} />
        <Route path="ovriga" element={<ProtectedRoute><Navigate to="overview" replace /></ProtectedRoute>} />
        <Route path="other" element={<ProtectedRoute><Navigate to="overview" replace /></ProtectedRoute>} />
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


// Blocks rendering of the route tree (layouts/pages) until the initial auth check
// completes, so authenticated-only layout chrome never flashes before a login redirect.
function AppContent() {
  const { loading } = useAuth();

  if (loading) {
    return <div className="min-h-screen bg-white" />;
  }

  return <RouterProvider router={router} />;
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  )
}

export default App
