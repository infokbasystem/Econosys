import { useLocation, NavLink } from 'react-router-dom'
import './Navbar.css'
import bg from '../assets/menu-bg.png'
import bgdashboard from '../assets/menu-dashboard.png'
import bgcalculation from '../assets/appbar.box.svg'
import bgorder from '../assets/appbar.draw.pen.svg'
import bglogistics from '../assets/appbar.forklift.svg'
import bgfinance from '../assets/menu-finance.png'
import bgmanagement from '../assets/appbar.clothes.tie.svg'
import bgquality from '../assets/appbar.alien.svg'
import bgreporting from '../assets/menu-reporting.png'
import bgsettings from '../assets/appbar.settings.svg'

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
                    {/* <li className={getNavLinkClass('calculation')}><NavLink to='/calculation'><div className={'div-general calculation-item'}><img src={bgcalculation} alt='' aria-hidden='true' />KALKYL</div></NavLink></li> */}
                    <li className={getNavLinkClass('order')}><NavLink to='/order'><div className={'div-general order-item'}><img src={bgorder} alt='' aria-hidden='true' />ORDER</div></NavLink></li>
                    <li className={getNavLinkClass('logistics')}><NavLink to='/logistics'><div className={'div-general logistics-item'}><img src={bglogistics} alt='' aria-hidden='true' />LOGISTIK</div></NavLink></li>
                    <li className={getNavLinkClass('finance')}><NavLink to='/finance'><div className={'div-general'} style={{ backgroundImage: `url(${bgfinance})` }}>EKONOMI</div></NavLink></li>
                    <li className={getNavLinkClass('management')}><NavLink to='/management'><div className={'div-general management-item'}><img src={bgmanagement} alt='' aria-hidden='true' />LEDNING</div></NavLink></li>
                    {/* <li className={getNavLinkClass('quality')}><NavLink to='/quality'><div className={'div-general quality-item'}><img src={bgquality} alt='' aria-hidden='true' />KVALITET</div></NavLink></li> */}
                    <li className={getNavLinkClass('reporting')}><NavLink to='/reporting'><div className={'div-general'} style={{ backgroundImage: `url(${bgreporting})` }}>RAPPORTER</div></NavLink></li>
                    <li className={getNavLinkClass('settings')}><NavLink to='/settings'><div className={'div-general settings-item'}><img src={bgsettings} alt='' aria-hidden='true' />INSTÄLLNINGAR</div></NavLink></li>
                </ul>
            </div>
        </nav>
    )
}

export default Navbar