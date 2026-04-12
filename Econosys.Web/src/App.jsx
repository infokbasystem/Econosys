import { useState } from 'react'
import { Route, createBrowserRouter, createRoutesFromElements, RouterProvider } from 'react-router-dom'

import './App.css'
import LoginPage from './pages/LoginPage'
import { AuthProvider } from './contexts/AuthContext'
import ProtectedRoute from './components/ProtectedRoute'

import MainLayout from './layouts/MainLayout'
import FinanceLayout from './layouts/FinanceLayout'
import ReportingLayout from './layouts/ReportingLayout'

import Overview from './pages/Overview'

import FinanceOverview from './pages/Finance/FinanceOverview'

import ReportingOverview from './pages/Reporting/ReportingOverview'
import InventoryReport from './pages/Reporting/InventoryReport'

const router = createBrowserRouter(
  createRoutesFromElements(
    <Route>
      <Route path="/" element={< MainLayout />}>
        <Route index element={<ProtectedRoute>< Overview /></ProtectedRoute>} />
      </Route>
      <Route path="finance" element={< FinanceLayout />}>
        <Route index element={<ProtectedRoute>< FinanceOverview /></ProtectedRoute>} />
      </Route>
      <Route path="reporting" element={< ReportingLayout />}>
        <Route index element={<ProtectedRoute>< ReportingOverview /></ProtectedRoute>} />
        <Route path="inventory" element={<ProtectedRoute>< InventoryReport /></ProtectedRoute>} />
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
