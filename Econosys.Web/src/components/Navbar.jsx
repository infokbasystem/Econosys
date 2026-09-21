import { useLocation, NavLink } from 'react-router-dom'
import { User, ChartNoAxesCombined, ClipboardList, Truck, Coins, CircleAlert, BarChart3, Settings } from 'lucide-react'
import './Navbar.css'

const menuItems = [
    { path: '', to: '/', label: 'MIN SIDA', icon: User },
    { path: 'order', to: '/order', label: 'ORDER', icon: ClipboardList },
    { path: 'logistics', to: '/logistics', label: 'LOGISTIK', icon: Truck },
    { path: 'finance', to: '/finance', label: 'EKONOMI', icon: ChartNoAxesCombined },
    { path: 'management', to: '/management', label: 'AVVIKELSER', icon: CircleAlert },
    { path: 'reporting', to: '/reporting', label: 'RAPPORTER', icon: BarChart3 },
    { path: 'settings', to: '/settings', label: 'INSTÄLLNINGAR', icon: Settings },
]

const Navbar = () => {

    const location = useLocation();
    const isActivePath = (path) => location.pathname.split('/')[1] === path;

    return (
        <nav className='relative z-10 border-b border-gray-200'>
            {/* shadow-[0_4px_8px_rgba(15,23,42,0.16)] */}
            {/* Primary navigation */}
            <div id='menu' style={{ backgroundColor: 'rgb(245,244,240)' }}>
                <ul className='flex items-center justify-center'>
                    {menuItems.map((item, index) => {
                        const Icon = item.icon;
                        const isActive = isActivePath(item.path);

                        return (
                            <li key={item.to} className={`menu-item ${index > 0 ? 'menu-item-divider' : ''}`}>
                                <NavLink to={item.to} className={`menu-card ${isActive ? 'menu-card-active' : ''}`}>
                                    <Icon className='menu-card-icon' strokeWidth={1.6} />
                                    <span className='menu-card-label'>{item.label}</span>
                                </NavLink>
                            </li>
                        );
                    })}
                </ul>
            </div>
        </nav>
    )
}

export default Navbar