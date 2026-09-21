import { ClipboardList, Clock3, ReceiptText } from 'lucide-react'
import ReportingSectionLayout from '../Shared/ReportingSectionLayout'

const groups = [
  {
    items: [
      { to: '/reporting/order/nonconfirmedsupplierorders', label: 'Ej ordererkända beställningar', icon: ClipboardList },
      { to: '/reporting/order/noninvoicedorders', label: 'Ej fullt fakturerade order', icon: ReceiptText },
    ],
  },
  {
    label: 'Hanteringstider',
    labelIcon: Clock3,
    items: [
      { to: '/reporting/order/handlingtimes-repeat-orders', leftPadding: 'pl-11', label: 'Repeat orders' },
      { to: '/reporting/order/handlingtimes-new-orders', leftPadding: 'pl-11', label: 'Nya orders' },
      // { leftPadding: 'pl-11', label: 'Leverantörstider', disabled: true },
      { to: '/reporting/order/handlingtimes-created-by', leftPadding: 'pl-11', label: 'Skapad av' },
      { to: '/reporting/order/handlingtimes-data', leftPadding: 'pl-11', label: 'Data' },
      { to: '/reporting/order/handlingtimes-settings', leftPadding: 'pl-11', label: 'Inställningar' },
    ],
  },
]

function OrderReportingLayout() {
  return <ReportingSectionLayout groups={groups} />
}

export default OrderReportingLayout
