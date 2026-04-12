import { useLocation, NavLink } from 'react-router-dom'
import './Navbar.css'
import bg from '../assets/menu-bg.png'
import bgdashboard from '../assets/menu-dashboard.png'
// import bgoperations from '../assets/menu-operations.png'
import bgfinance from '../assets/menu-finance.png'
// import bgmanagement from '../assets/menu-management.png'
import bgreporting from '../assets/menu-reporting.png'
// import bgregisters from '../assets/menu-registers.png'

const Navbar = () => {

    const location = useLocation();
    const getNavLinkClass = (path) => {
        // console.log(location.pathname.split('/')[1], path);
        const isActivePart = (location.pathname.split('/')[1] === path) ? ((path ==='' ? 'overview' : path) + '-backcolor') :'';
        const className = (path === '' ? 'overview' : '') + path + ' ' + isActivePart;
        return className;
    };

    return (
        <nav className=''>
            {/* Primary navigation */}
            <div id='menu' className='' style={{ backgroundImage: `url(${bg})` }}>
                <ul className='flex justify-center'>
                    <li className={getNavLinkClass('')}><NavLink to='/'><div className={'div-general'} style={{ backgroundImage: `url(${bgdashboard})` }}>ÖVERSIKT</div></NavLink></li>
                    {/* <li className={getNavLinkClass('operations')}><NavLink to='/operations'><div className={'div-general'} style={{ backgroundImage: `url(${bgoperations})` }}>UTHYRNING</div></NavLink></li> */}
                    <li className={getNavLinkClass('finance')}><NavLink to='/finance'><div className={'div-general'} style={{ backgroundImage: `url(${bgfinance})` }}>EKONOMI</div></NavLink></li>
                    {/* <li className={'opacity-50 pointer-events-none ' + getNavLinkClass('management')}><NavLink to='/management'><div className={'div-general'} style={{ backgroundImage: `url(${bgmanagement})` }}>LEDNING</div></NavLink></li> */}
                    <li className={getNavLinkClass('reporting')}><NavLink to='/reporting'><div className={'div-general'} style={{ backgroundImage: `url(${bgreporting})` }}>RAPPORTER</div></NavLink></li>
                    {/* <li className={'' + getNavLinkClass('settings')}><NavLink to='/settings'><div className={'div-general'} style={{ backgroundImage: `url(${bgdashboard})` }}>INSTÄLLNINGAR</div></NavLink></li> */}

                    {/* <li className={getNavLinkClass('product')}><NavLink to='/product'><div className='div-general' style={{ backgroundImage: `url(${bgorder})` }}>PRODUKT</div></NavLink></li>
                    <li className={getNavLinkClass('sales')}><NavLink to='/sales'><div className='div-general' style={{ backgroundImage: `url(${bgcalculation})` }}>FÖRSÄLJNING</div></NavLink></li> */}
                    {/* <li className={getNavLinkClass('order')}><NavLink to='/order'><div className='div-general' style={{ backgroundImage: `url(${bgorder})` }}>ORDER</div></NavLink></li> */}
                    {/* <li className={getNavLinkClass('transport')}><NavLink to='/transport'><div className='div-general' style={{ backgroundImage: `url(${bgtransport})` }}>TRANSPORT</div></NavLink></li>
                    <li className={getNavLinkClass('finance')}><NavLink to='/finance'><div className='div-general finance2' style={{ backgroundImage: `url(${bgfinance})` }}>EKONOMI</div></NavLink></li>
                    <li className={getNavLinkClass('reporting')}><NavLink to='/reporting'><div className='div-general' style={{ backgroundImage: `url(${bgreporting})` }}>RAPPORTER</div></NavLink></li>
                    <li className={getNavLinkClass('registers')}><NavLink to='/settings'><div className='div-general' style={{ backgroundImage: `url(${bgregisters})` }}>INSTÄLLNINGAR</div></NavLink></li> */}
                </ul>
            </div>
        </nav>
    )
}

export default Navbar