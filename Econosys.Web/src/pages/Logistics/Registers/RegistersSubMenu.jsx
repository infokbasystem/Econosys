import { Warehouse, Truck, HandCoins, Package, Scale, ClipboardList, FileText, Ruler } from 'lucide-react'
import LeftMenu from '../../../components/LeftMenu'

const items = [
  { to: 'shippers', label: 'Transportörer', icon: Truck },
  { to: 'inventories', label: 'Lager', icon: ClipboardList },
  { to: 'pallet-formats', label: 'Palltyper', icon: Ruler },
  { to: 'palletfactors', label: 'Pallfaktorer', icon: Scale },
  { to: 'transportpricelists', label: 'Transportprislistor', icon: HandCoins },
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
