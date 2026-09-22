import { NavLink } from 'react-router-dom'
import {
  Truck,
  ListChecks,
  CirclePlus,
  PackagePlus,
  Package,
  ClipboardCheck,
  BookOpenText,
} from 'lucide-react'

const items = [
  { to: '/logistics/transportorderoverivew', label: 'Transportorder', icon: Truck },
  { to: '/logistics/calloffoverview', label: 'Avrop', icon: ListChecks },
  { to: '/logistics/calloff/new', label: 'Nytt avrop', icon: CirclePlus, leftMargin: 'ml-16' },
  { to: '/logistics/newdelivery', label: 'Ny leverans', icon: PackagePlus },
  { to: '/logistics/deliveries', label: 'Leveranser', icon: Package },
  { to: '/logistics/stocktakings', label: 'Inventeringar', icon: ClipboardCheck, leftMargin: 'ml-16' },
  { to: '/logistics/registers', label: 'Register', icon: BookOpenText },
]

export default function LogisticsSubMenu() {
  return (
    <nav className="sticky top-[72px] z-40 flex items-end justify-center gap-0 px-8 pt-1 pb-[6px] border-b border-gray-300" style={{ backgroundColor: 'rgb(235, 234, 230)' }}>
      {items.map((item) => {
        const Icon = item.icon
        const wrapperClass = ['flex justify-center', item.leftMargin || 'ml-4 mr-4'].filter(Boolean).join(' ')

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
                ]
                  .filter(Boolean)
                  .join(' ')
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
