import { NavLink } from 'react-router-dom'
import { BarChart3, CircleDollarSign, ClipboardList, Landmark, Leaf, Truck, TrendingUp, CircleAlert } from 'lucide-react'
import bg from '../../assets/content.png'

const items = [
  { to: '/reporting/overview', label: 'Översikt', icon: BarChart3, end: true },
  { to: '/reporting/order', label: 'Order', icon: ClipboardList, leftMargin: 'ml-12' },
  { to: '/reporting/sales', label: 'Försäljning', icon: TrendingUp },
  { to: '/reporting/logistics', label: 'Logistik', icon: Truck },
  { to: '/reporting/finance', label: 'Ekonomi', icon: CircleDollarSign },
  { to: '/reporting/environment', label: 'K&M', icon: Leaf, leftMargin: 'ml-12' },
  { to: '/reporting/authorities', label: 'Avvikelser', icon: CircleAlert },
]

export default function ReportingSubMenu() {
  return (
    <nav className="sticky top-[72px] z-40 flex items-end justify-center gap-0 px-8 pt-1 pb-[6px] border-b border-gray-300" style={{ backgroundColor: 'rgb(235, 234, 230)' }}>
      {/* backgroundImage: `url(${bg})` */}
      {items.map((item) => {
        const Icon = item.icon
        const wrapperClass = ['flex justify-center', item.leftMargin || 'ml-4', item.rightMargin || 'mr-4']
          .filter(Boolean)
          .join(' ')

        return (
          <div className={wrapperClass} key={item.to}>
            <NavLink
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                [
                  'flex select-none flex-col items-center border-b-[2px] pb-[1px] pt-1 text-[9px] leading-tight tracking-[0.12em] transition-colors',
                  isActive
                    ? 'text-stone-900'
                    : 'border-transparent text-stone-600 hover:text-stone-900',
                ].join(' ')
              }
              style={({ isActive }) => (isActive ? { borderColor: 'rgb(122,143,131)' } : undefined)}
            >
              <Icon className="h-5 w-5" />
              <span className="mt-1 whitespace-nowrap font-medium">{item.label}</span>
            </NavLink>
          </div>
        )
      })}
    </nav>
  )
}
