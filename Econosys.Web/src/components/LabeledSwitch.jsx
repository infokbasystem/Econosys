import ToggleSwitch from '../components/ToggleSwitch';

const LabeledSwitch = ({
    id,
    name,
    rowId,
    field,
    value,
    onChange,
    label,
    labelWidth,
    marginTop,
    marginLeft,
    disabled = false,
    containerClassName = '',
}) => {
    const resolvedId = id || name;


    return (
        <div
            className={`flex items-center space-x-1 w-auto pb-[1px] ${disabled ? 'opacity-60' : ''} ${containerClassName}`}
            style={{
                ...(marginTop !== undefined ? { marginTop: `${marginTop}px` } : {}),
                ...(marginLeft !== undefined ? { marginLeft: `${marginLeft}px` } : {}),
            }}
        >
            <label className={`${labelWidth || ''} pr-2 flex-none text-xs text-gray-700`}>{label}</label>
            <ToggleSwitch
                rowId={rowId}
                field={field}
                id={resolvedId}
                name={name}
                checked={value}
                onChange={(rowId, field, checked) => onChange(rowId, field, checked)}
                disabled={disabled}
            />
            {/* <input type="checkbox" value={value} onClick={(e) => onClick(e)} className="sr-only peer" />
            <div value={value} onClick={(e) => onClick(e)} className="relative w-9 h-5 bg-gray-200 peer-focus:outline-none peer-focus:none rounded-full peer dark:bg-gray-700 peer-checked:after:translate-x-full 
                                        rtl:peer-checked:after:-translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] 
                                        after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all dark:border-gray-600 
                                        peer-checked:bg-yellow-400 dark:peer-checked:bg-blue-600"></div> */}
        </div>
    );
};

export default LabeledSwitch;