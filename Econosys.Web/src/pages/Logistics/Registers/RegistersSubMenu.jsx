import { Warehouse, Truck, HandCoins, Package, Scale } from 'lucide-react'
import LeftMenu from '../../../components/LeftMenu'

const items = [
  { to: 'warehouses', label: 'Lager', icon: Warehouse },
  { to: 'transporters', label: 'Transportörer', icon: Truck },
  { to: 'transportpricelists', label: 'Transportprislistor', icon: HandCoins },
  { to: 'pallettypes', label: 'Palltyper', icon: Package },
  { to: 'palletfactors', label: 'Pallfaktorer', icon: Scale },
]

function RegistersSubMenu() {
  return (
    <aside className="w-60 shrink-0 self-stretch mt-4 pr-6">
      <div className="sticky top-[50px] max-h-[calc(100dvh-120px)] overflow-y-auto">
        <LeftMenu groups={[{ items }]} />
      </div>
    </aside>
  )
}

export default RegistersSubMenu
