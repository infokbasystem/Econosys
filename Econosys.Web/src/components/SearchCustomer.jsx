import { useEffect, useRef, useState } from 'react'
import { ExternalLink, Search } from 'lucide-react'
import { searchCustomers } from '../helpers/customerApi'

function normalizeCustomerRows(payload) {
    if (Array.isArray(payload)) {
        return payload
    }

    if (payload && Array.isArray(payload.items)) {
        return payload.items
    }

    return []
}

export default function SearchCustomer({
    onCustomerSelect,
    onPendingSelectionChange,
    selectedCustomerName,
    selectedCustomerId,
    placeholder = 'Sök kund',
    minQueryLength = 1,
    debounceMs = 200,
    take = 50,
    width = '100px',
    className = '',
}) {
    const containerRef = useRef(null)
    const requestRef = useRef(0)
    const [query, setQuery] = useState(selectedCustomerName ?? '')
    const [hasEditedSinceSelection, setHasEditedSinceSelection] = useState(false)
    const [matches, setMatches] = useState([])
    const [isSearching, setIsSearching] = useState(false)
    const [isOpen, setIsOpen] = useState(false)
    const mayHaveMoreMatches = !isSearching && take > 0 && matches.length >= take

    useEffect(() => {
        function handleDocumentMouseDown(event) {
            if (!containerRef.current?.contains(event.target)) {
                setIsOpen(false)
            }
        }

        document.addEventListener('mousedown', handleDocumentMouseDown)
        return () => document.removeEventListener('mousedown', handleDocumentMouseDown)
    }, [])

    useEffect(() => {
        if (!hasEditedSinceSelection) {
            return
        }

        const trimmedQuery = query.trim()
        const shouldShowAll = trimmedQuery === '**'

        if (!shouldShowAll && trimmedQuery.length < minQueryLength) {
            return
        }

        const requestId = ++requestRef.current

        const timer = setTimeout(() => {
            setIsSearching(true)
            Promise.resolve(searchCustomers({
                searchTerm: shouldShowAll ? '' : trimmedQuery,
                pageNumber: 1,
                pageSize: take,
            }))
                .then((result) => {
                    if (requestRef.current !== requestId) {
                        return
                    }

                    setMatches(normalizeCustomerRows(result))
                    setIsOpen(true)
                })
                .catch(() => {
                    if (requestRef.current !== requestId) {
                        return
                    }

                    setMatches([])
                    setIsOpen(true)
                })
                .finally(() => {
                    if (requestRef.current === requestId) {
                        setIsSearching(false)
                    }
                })
        }, debounceMs)

        return () => clearTimeout(timer)
    }, [debounceMs, hasEditedSinceSelection, minQueryLength, query, take])

    function handlePick(customer) {
        onCustomerSelect?.(customer)
        onPendingSelectionChange?.(false)
        setQuery(customer?.name ?? '')
        setHasEditedSinceSelection(false)
        setMatches([])
        setIsOpen(false)
    }

    return (
        <div ref={containerRef} className={`relative ${className} ${width}`}>
            <div className="relative">
                <input
                    value={query}
                    onChange={(event) => {
                        const nextQuery = event.target.value
                        setQuery(nextQuery)
                        setHasEditedSinceSelection(true)
                        setMatches([])
                        setIsSearching(false)
                        setIsOpen(false)

                        const hasQuery = nextQuery.trim() !== ''
                        onPendingSelectionChange?.(hasQuery)
                        if (!hasQuery && selectedCustomerId) {
                            onCustomerSelect?.(null)
                            onPendingSelectionChange?.(false)
                            setHasEditedSinceSelection(false)
                        }
                    }}
                    onFocus={() => {
                        if (hasEditedSinceSelection && query.trim().length >= minQueryLength) {
                            setIsOpen(true)
                        }
                    }}
                    placeholder={placeholder}
                    autoComplete="off"
                    className={`h-6 w-full rounded-full border border-lime-600 bg-white px-4 pt-[1px] ${selectedCustomerId ? 'pr-14' : 'pr-9'} text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700`}
                />
                {selectedCustomerId && (
                    <button
                        type="button"
                        aria-label="Öppna vald kund i ny flik"
                        title="Öppna vald kund i ny flik"
                        className="absolute right-8 top-1/2 -translate-y-1/2 text-gray-500 transition hover:text-gray-800"
                        onClick={() => window.open(`/order/customers/${encodeURIComponent(selectedCustomerId)}`, '_blank', 'noopener,noreferrer')}
                    >
                        <ExternalLink className="h-3.5 w-3.5" />
                    </button>
                )}
                <Search className="pointer-events-none absolute right-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-500" />
            </div>

            {isOpen && (query.trim() === '**' || query.trim().length >= minQueryLength) && (
                <div className="absolute left-[calc(100%+8px)] top-0 z-50 max-h-90 w-[400px] overflow-hidden overscroll-none rounded-sm border border-stone-400 bg-stone-50 shadow-lg isolate">
                    <div className="max-h-90 overflow-x-hidden overflow-y-auto overscroll-contain bg-stone-50 [-webkit-overflow-scrolling:touch] [overscroll-behavior-y:contain]">
                        <ul className="divide-y divide-stone-300">
                        {matches.map((customer) => {
                            const orgNr = customer.orgNr?.trim() || 'Inget org.nr'
                            const telephone = customer.telephone1?.trim()
                            const postalAddress = customer.postalAddress?.trim()

                            return (
                                <li key={customer.id}>
                                    <button
                                        type="button"
                                        className="w-full cursor-pointer px-4 py-2 text-left text-gray-800 transition hover:bg-stone-200/60"
                                        onClick={() => handlePick(customer)}
                                    >
                                        <div className="truncate text-xs font-medium tracking-[0.08em] text-slate-800 uppercase">
                                            {customer.name ?? '-'}
                                        </div>
                                        <div className="mt-0.5 flex flex-wrap items-center gap-x-8 gap-y-0.5 text-tiny leading-4 tracking-[0.08em] text-slate-600">
                                            <span>{orgNr}</span>
                                            {telephone && <span>{telephone}</span>}
                                            {!telephone && postalAddress && <span>{postalAddress}</span>}
                                        </div>
                                    </button>
                                </li>
                            )
                        })}
                        </ul>

                        {!isSearching && matches.length === 0 && (
                            <div className="px-4 py-2 text-xs text-gray-500">
                                Inga träffar
                            </div>
                        )}

                        {isSearching && (
                            <div className="px-4 py-2 text-xs text-gray-500">
                                Söker...
                            </div>
                        )}

                        {mayHaveMoreMatches && (
                            <div className="border-t border-amber-200 bg-amber-50 px-4 py-1.5 text-xs text-amber-800">
                                Visar de första {take} träffarna. Förfina sökningen för att se fler.
                            </div>
                        )}
                    </div>
                </div>

            )}
        </div>
    )
}