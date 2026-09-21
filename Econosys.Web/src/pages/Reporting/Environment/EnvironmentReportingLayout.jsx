import { Leaf } from 'lucide-react'
import ReportingSectionLayout from '../Shared/ReportingSectionLayout'

const groups = [
  {
    items: [
      { to: '/reporting/environment', label: 'Översikt', icon: Leaf, end: true },
    ],
  },
]

function EnvironmentReportingLayout() {
  return <ReportingSectionLayout groups={groups} />
}

export default EnvironmentReportingLayout
