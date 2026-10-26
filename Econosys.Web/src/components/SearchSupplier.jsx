import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { ExternalLink, Search } from 'lucide-react'
import { searchSuppliers } from '../helpers/supplierApi'
import useSearchPopupPosition from '../hooks/useSearchPopupPosition'

export default function SearchSupplier({
    onSupplierSelect,
    onPendingSelectionChange,
    selectedSupplierName,
    selectedSupplierId,
    placeholder = 'Sök leverantör',
    minQueryLength = 1,
    debounceMs = 200,
    take = 50,
    width = '100px',
    className = '',
}) {
    const containerRef = useRef(null)
    const popupRef = useRef(null)
    const requestRef = useRef(0)
    const [query, setQuery] = useState(selectedSupplierName ?? '')
    const [hasEditedSinceSelection, setHasEditedSinceSelection] = useState(false)
    const [matches, setMatches] = useState([])
    const [isSearching, setIsSearching] = useState(false)
    const [isOpen, setIsOpen] = useState(false)
    const mayHaveMoreMatches = !isSearching && take > 0 && matches.length >= take
    const showMatches = isOpen && (query.trim() === '**' || query.trim().length >= minQueryLength)

    useSearchPopupPosition(showMatches, containerRef, popupRef)

    useEffect(() => {
        function handleDocumentMouseDown(event) {
            if (!containerRef.current?.contains(event.target) && !popupRef.current?.contains(event.target)) {
                setIsOpen(false)
            }
        }

        document.addEventListener('mousedown', handleDocumentMouseDown)
        return () => document.removeEventListener('mousedown', handleDocumentMouseDown)
    }, [])

    useEffect(() => {
        if (!hasEditedSinceSelection) return

        const trimmedQuery = query.trim()
        const shouldShowAll = trimmedQuery === '**'
        if (!shouldShowAll && trimmedQuery.length < minQueryLength) return

        const requestId = ++requestRef.current
        const timer = setTimeout(() => {
            setIsSearching(true)
            Promise.resolve(searchSuppliers({
                searchTerm: shouldShowAll ? '' : trimmedQuery,
                pageNumber: 1,
                pageSize: take,
            }))
                .then((result) => {
                    if (requestRef.current !== requestId) return
                    setMatches(Array.isArray(result) ? result : result?.items ?? [])
                    setIsOpen(true)
                })
                .catch(() => {
                    if (requestRef.current !== requestId) return
                    setMatches([])
                    setIsOpen(true)
                })
                .finally(() => {
                    if (requestRef.current === requestId) setIsSearching(false)
                })
        }, debounceMs)

        return () => clearTimeout(timer)
    }, [debounceMs, hasEditedSinceSelection, minQueryLength, query, take])

    function handlePick(supplier) {
        onSupplierSelect?.(supplier)
        onPendingSelectionChange?.(false)
        setQuery(supplier?.name ?? '')
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
                        if (!hasQuery && selectedSupplierId) {
                            onSupplierSelect?.(null)
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
                    className={`h-6 w-full rounded-full border border-lime-600 bg-white px-4 pt-[1px] ${selectedSupplierId ? 'pr-14' : 'pr-9'} text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700`}
                />
                {selectedSupplierId && (
                    <button
                        type="button"
                        aria-label="Öppna vald leverantör i ny flik"
                        title="Öppna vald leverantör i ny flik"
                        className="absolute right-8 top-1/2 -translate-y-1/2 text-gray-500 transition hover:text-gray-800"
                        onClick={() => window.open(`/order/suppliers/${encodeURIComponent(selectedSupplierId)}`, '_blank', 'noopener,noreferrer')}
                    >
                        <ExternalLink className="h-3.5 w-3.5" />
                    </button>
                )}
                <Search className="pointer-events-none absolute right-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-500" />
            </div>

            {showMatches && createPortal(
                <div ref={popupRef} className="fixed z-50 overflow-x-hidden overflow-y-auto overscroll-contain rounded-sm border border-stone-400 bg-stone-50 shadow-lg isolate [-webkit-overflow-scrolling:touch]">
                    <div className="bg-stone-50">
                        <ul className="divide-y divide-stone-300">
                            {matches.map((supplier) => {
                                const reference = supplier.reference?.trim() || supplier.email?.trim()
                                const telephone = supplier.telephone1?.trim()
                                const postalAddress = supplier.postalAddress?.trim()

                                return (
                                    <li key={supplier.id}>
                                        <button
                                            type="button"
                                            className="w-full cursor-pointer px-4 py-2 text-left text-gray-800 transition hover:bg-stone-200/60"
                                            onClick={() => handlePick(supplier)}
                                        >
                                            <div className="truncate text-xs font-medium tracking-[0.08em] text-slate-800 uppercase">
                                                {supplier.name ?? '-'}
                                            </div>
                                            <div className="mt-0.5 flex flex-wrap items-center gap-x-8 gap-y-0.5 text-tiny leading-4 tracking-[0.08em] text-slate-600">
                                                {reference && <span>{reference}</span>}
                                                {telephone && <span>{telephone}</span>}
                                                {!telephone && postalAddress && <span>{postalAddress}</span>}
                                            </div>
                                        </button>
                                    </li>
                                )
                            })}
                        </ul>

                        {!isSearching && matches.length === 0 && (
                            <div className="px-4 py-2 text-xs text-gray-500">Inga träffar</div>
                        )}
                        {isSearching && (
                            <div className="px-4 py-2 text-xs text-gray-500">Söker...</div>
                        )}
                        {mayHaveMoreMatches && (
                            <div className="border-t border-amber-200 bg-amber-50 px-4 py-1.5 text-xs text-amber-800">
                                Visar de första {take} träffarna. Förfina sökningen för att se fler.
                            </div>
                        )}
                    </div>
                </div>,
                document.body,
            )}
        </div>
    )
}