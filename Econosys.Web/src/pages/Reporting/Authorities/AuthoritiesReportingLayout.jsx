import { Landmark } from 'lucide-react'
import ReportingSectionLayout from '../Shared/ReportingSectionLayout'

const groups = [
  {
    items: [
      { to: '/reporting/authorities', label: 'Översikt', icon: Landmark, end: true },
    ],
  },
]

function AuthoritiesReportingLayout() {
  return <ReportingSectionLayout groups={groups} />
}

export default AuthoritiesReportingLayout
