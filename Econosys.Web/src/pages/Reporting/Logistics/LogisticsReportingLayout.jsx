import { Archive, Boxes, PackageSearch, Truck } from 'lucide-react'
import ReportingSectionLayout from '../Shared/ReportingSectionLayout'

const groups = [
  {
    items: [
      { to: '/reporting/logistics/inventory', label: 'Lagerrapport', icon: Archive },
      { to: '/reporting/logistics/slowmovers', label: 'Hyllvärmare', icon: PackageSearch },
      { to: '/reporting/logistics/palletfollowup', label: 'Palluppföljning', icon: Boxes },
      { to: '/reporting/logistics/packagingreport', label: 'Förpackningsrapport', icon: Boxes },
      { to: '/reporting/logistics/nondeliveredwarehouseorders', label: 'Ej inlevererade lagerorder', icon: Truck },
    ],
  },
]

function LogisticsReportingLayout() {
  return <ReportingSectionLayout groups={groups} />
}

export default LogisticsReportingLayout
