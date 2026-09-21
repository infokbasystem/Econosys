import { CircleDollarSign } from 'lucide-react'
import ReportingSectionLayout from '../Shared/ReportingSectionLayout'

const groups = [
  {
    items: [
      { to: '/reporting/finance', label: 'Översikt', icon: CircleDollarSign, end: true },
      { to: '/reporting/finance/ordercostmonth', label: 'Verktygskostnader', icon: CircleDollarSign },
      { to: '/reporting/finance/revenueperorder', label: 'Intäkt per order', icon: CircleDollarSign },
      { to: '/reporting/finance/invoicedarticles', label: 'Fakturerade artiklar', icon: CircleDollarSign },
    ],
  },
]

function FinanceReportingLayout() {
  return <ReportingSectionLayout groups={groups} />
}

export default FinanceReportingLayout
