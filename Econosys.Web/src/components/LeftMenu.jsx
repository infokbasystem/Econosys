import { NavLink, useLocation } from 'react-router-dom';

export const leftMenuActiveClass = 'bg-lime-50 text-stone-900 shadow-sm shadow-lime-900/10';

export const LeftMenuItem = ({ item }) => {
    const location = useLocation();
    const Icon = item.icon;
    const leftPaddingClass = item.leftPadding ?? item.paddingLeft ?? 'pl-5';
    const hasActivePath = Array.isArray(item.activePaths) && item.activePaths.includes(location.pathname);
    const content = (isActive = false) => (
        <span className={[
            `group relative flex w-full items-center gap-3 rounded-r-md py-2 ${leftPaddingClass} pr-3 text-left transition`,
            item.disabled
                ? 'cursor-not-allowed text-stone-400 opacity-60'
                : isActive
                    ? leftMenuActiveClass
                    : 'text-stone-600 hover:bg-stone-100/70 hover:text-stone-900',
        ].join(' ')}>
            <span
                aria-hidden="true"
                className={[
                    'absolute left-0 top-1/2 h-6 w-[4px] -translate-y-1/2 rounded-r-full transition',
                    isActive ? 'bg-lime-500' : item.disabled ? 'bg-transparent' : 'bg-transparent group-hover:bg-lime-200',
                ].join(' ')}
            />
            {Icon && <Icon className="h-4 w-4 shrink-0" />}
            <span className={['text-xs leading-none', isActive ? 'font-semibold' : 'font-normal'].join(' ')}>{item.label}</span>
        </span>
    );

    if (item.disabled) return <div className="block w-full" aria-disabled="true">{content()}</div>;

    return <NavLink to={item.to} end={item.end} className="block w-full">{({ isActive }) => content(isActive || hasActivePath)}</NavLink>;
};

const LeftMenu = ({ groups = [], showGroupLabels = false }) => (
    <nav className="space-y-8 pt-5 pe-3">
        {groups.map((group, groupIndex) => {
            const GroupIcon = group.labelIcon ?? group.icon;

            return (
                <div key={group.label ?? 'overview'}>
                    {showGroupLabels && group.label && (
                        <div className="mb-0 flex items-center gap-2 px-5 pb-2 text-xs font-medium uppercase tracking-[0.12em] text-stone-500">
                            {GroupIcon && <GroupIcon className="h-3.5 w-3.5 shrink-0" />}
                            <span>{group.label}</span>
                        </div>
                    )}
                    <div className="space-y-1">
                        {group.items.map((item) => <LeftMenuItem key={item.to ?? item.label} item={item} />)}
                    </div>
                    {groupIndex < groups.length - 1 && <div className="mt-5" />}
                </div>
            );
        })}
    </nav>
);

export default LeftMenu;
