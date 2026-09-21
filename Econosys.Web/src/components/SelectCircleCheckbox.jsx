export default function SelectCircleCheckbox({ checked, onChange, ariaLabel, disabled = false }) {
    function handleKeyDown(event) {
        if (disabled) {
            return
        }

        if (event.key === ' ' || event.key === 'Enter') {
            event.preventDefault()
            onChange()
        }
    }

    return (
        <button
            type="button"
            role="checkbox"
            aria-checked={checked}
            aria-label={ariaLabel}
            onClick={onChange}
            onKeyDown={handleKeyDown}
            disabled={disabled}
            className="inline-flex h-[15px] w-[15px] shrink-0 cursor-pointer items-center justify-center rounded-full bg-[#f1f3f2] transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-lime-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
            <span
                className={`inline-flex h-[15px] w-[15px] items-center justify-center rounded-full border shadow-[inset_0_0px_0_rgba(255,255,255,0.95)] transition-all ${
                    checked ? 'border-[#368b3f] bg-[#3f9848]' : 'border-[#c8cfcb] bg-white'
                }`}
            >
                <svg
                    viewBox="0 0 16 16"
                    aria-hidden="true"
                    className={`h-2.5 w-2.5 text-white transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}
                >
                    <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </span>
        </button>
    )
}
