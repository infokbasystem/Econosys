import ToggleSwitch from '../components/ToggleSwitch';

const LabeledSwitch = ({ label, labelWidth, margintop, name, value, onClick, ...props }) => {
    return (
        <div className={`flex items-center space-x-1 w-full pb-[1px] mt-${margintop} cursor-pointer`}>
            <label className={`${labelWidth || ''} flex-none text-xs text-gray-700`}>{label}</label>
            <ToggleSwitch
                id={name}
                name={name}
                checked={value || false}
                onChange={(e) => onClick(e)}
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