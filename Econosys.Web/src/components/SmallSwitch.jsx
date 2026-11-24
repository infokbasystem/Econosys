const SmallSwitch = ({ checked, label, onChange }) => (
    <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        onClick={() => onChange(!checked)}
        className={`mt-[2px] relative inline-flex h-4 w-7 items-center rounded-full transition-colors ${checked ? 'bg-lime-600' : 'bg-gray-300'}`}
    >
        <span className={`inline-block h-3 w-3 transform rounded-full bg-white transition-transform ${checked ? 'translate-x-3' : 'translate-x-1'}`} />
    </button>
);

export default SmallSwitch;
