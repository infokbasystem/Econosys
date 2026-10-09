import { useRef, useState } from 'react'
import { ArrowLeftCircle, ArrowRightCircle, Save, Search } from 'lucide-react'
import ActionButton from '../../components/ActionButton'
import LabeledInput from '../../components/LabeledInput'
import LabeledTextArea from '../../components/LabeledTextArea'
import apiClient from '../../config/apiClient'

const pageSize = 100
const tableFontStyle = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }

const emptyFilters = {
    lengthFrom: null,
    lengthTo: null,
    widthFrom: null,
    widthTo: null,
}

const PalletFactors = () => {
    const [filters, setFilters] = useState(emptyFilters)
    const [activeFilters, setActiveFilters] = useState(null)
    const [items, setItems] = useState([])
    const [pageNumber, setPageNumber] = useState(1)
    const [totalPages, setTotalPages] = useState(0)
    const [totalCount, setTotalCount] = useState(0)
    const [loading, setLoading] = useState(false)
    const [savingIds, setSavingIds] = useState([])
    const [message, setMessage] = useState(null)
    const requestNumber = useRef(0)

    const loadPage = async (searchFilters, nextPage) => {
        const currentRequestNumber = ++requestNumber.current
        setLoading(true)
        setMessage(null)

        try {
            const response = await apiClient.post('/palletfactors/search', {
                ...searchFilters,
                pageNumber: nextPage,
                pageSize,
            })

            if (currentRequestNumber !== requestNumber.current) return

            const result = response.data
            setItems((result.items ?? []).map((item) => ({
                ...item,
                palletFactors: item.palletFactors ?? '',
                originalPalletFactors: item.palletFactors ?? '',
            })))
            setActiveFilters(searchFilters)
            setPageNumber(result.pageNumber)
            setTotalPages(result.totalPages)
            setTotalCount(result.totalCount)
        } catch (error) {
            if (currentRequestNumber !== requestNumber.current) return
            setMessage({
                type: 'error',
                text: error?.response?.data?.message || 'Kunde inte hämta pallfaktorer.',
            })
        } finally {
            if (currentRequestNumber === requestNumber.current) {
                setLoading(false)
            }
        }
    }

    const handleSearch = (event) => {
        event.preventDefault()

        const values = Object.values(filters)
        if (values.some((value) => !Number.isInteger(value) || value < 0)) {
            setMessage({ type: 'error', text: 'Ange ett giltigt värde i alla intervallfält.' })
            return
        }

        if (filters.lengthFrom > filters.lengthTo || filters.widthFrom > filters.widthTo) {
            setMessage({ type: 'error', text: 'Från-värdet måste vara mindre än eller lika med till-värdet.' })
            return
        }

        void loadPage(filters, 1)
    }

    const updateFilter = (name, value) => {
        setFilters((current) => ({ ...current, [name]: value }))
    }

    const updatePalletFactors = (id, value) => {
        setItems((current) => current.map((item) =>
            item.id === id ? { ...item, palletFactors: value } : item
        ))
    }

    const savePalletFactors = async (item) => {
        setSavingIds((current) => [...current, item.id])
        setMessage(null)

        try {
            const response = await apiClient.put(`/palletfactors/${item.id}`, {
                palletFactors: item.palletFactors,
            })
            const savedValue = response.data.palletFactors ?? ''
            setItems((current) => current.map((currentItem) =>
                currentItem.id === item.id
                    ? { ...currentItem, palletFactors: savedValue, originalPalletFactors: savedValue }
                    : currentItem
            ))
            setMessage({ type: 'success', text: 'Pallfaktorer sparades.' })
        } catch (error) {
            setMessage({
                type: 'error',
                text: error?.response?.data?.message || 'Kunde inte spara pallfaktorer.',
            })
        } finally {
            setSavingIds((current) => current.filter((id) => id !== item.id))
        }
    }

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Pallfaktorer</h2>

            <form onSubmit={handleSearch} className="ml-5 mt-2 flex items-center gap-x-10 pb-2 ">
                <div>
                    <LabeledInput
                        label="Längd från"
                        labelWidth="w-16"
                        inputWidth="w-30"
                        type="number"
                        integerOnly
                        min={0}
                        step={1}
                        value={filters.lengthFrom}
                        onChange={(value) => updateFilter('lengthFrom', value)}
                    />
                    <LabeledInput
                        label="Längd till"
                        labelWidth="w-16"
                        inputWidth="w-30"
                        type="number"
                        integerOnly
                        min={0}
                        step={1}
                        value={filters.lengthTo}
                        onChange={(value) => updateFilter('lengthTo', value)}
                    />
                </div>
                <div>
                    <LabeledInput
                        label="Bredd från"
                        labelWidth="w-16"
                        inputWidth="w-30"
                        type="number"
                        integerOnly
                        min={0}
                        step={1}
                        value={filters.widthFrom}
                        onChange={(value) => updateFilter('widthFrom', value)}
                    />
                    <LabeledInput
                        label="Bredd till"
                        labelWidth="w-16"
                        inputWidth="w-30"
                        type="number"
                        integerOnly
                        min={0}
                        step={1}
                        value={filters.widthTo}
                        onChange={(value) => updateFilter('widthTo', value)}
                    />
                </div>

                <div className="ml-4">
                    <ActionButton label="Filtrera" icon={Search} type="submit" disabled={loading} accent="lime" />
                </div>
            </form>

            {message && (
                <p role="status" className={`text-xs ${message.type === 'error' ? 'text-red-700' : 'text-green-700'}`}>
                    {message.text}
                </p>
            )}

            <section className="ml-5 min-w-0">
                <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-0 mt-0">
                    {/* <p className="text-xs text-gray-600">
                        {loading ? 'Söker...' : activeFilters ? <>Rader <strong>{totalCount}</strong></> : 'Ange intervall och filtrera.'}
                    </p> */}
                    <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={tableFontStyle}>
                        <span>Sida {pageNumber} av {Math.max(1, totalPages)}</span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                aria-label="Föregående sida"
                                title="Föregående sida"
                                onClick={() => activeFilters && void loadPage(activeFilters, pageNumber - 1)}
                                disabled={loading || !activeFilters || pageNumber <= 1}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                            <button
                                type="button"
                                aria-label="Nästa sida"
                                title="Nästa sida"
                                onClick={() => activeFilters && void loadPage(activeFilters, pageNumber + 1)}
                                disabled={loading || !activeFilters || pageNumber >= totalPages}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                        </div>
                    </div>
                </div>

                <div className="border-t border-gray-300 py-1 mt-3 min-h-0 min-w-0 flex-1 overflow-auto">
                    <table className="table-fixed w-full min-w-[850px] border-collapse text-xs" style={tableFontStyle}>
                        <colgroup>
                            <col style={{ width: '9%' }} />
                            <col style={{ width: '9%' }} />
                            <col style={{ width: '9%' }} />
                            <col style={{ width: '9%' }} />
                            <col style={{ width: '50%' }} />
                            <col style={{ width: '14%' }} />
                        </colgroup>
                        <thead>
                            <tr className="text-tiny text-gray-500 text-left">
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Längd från</th>
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Längd till</th>
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Bredd från</th>
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Bredd till</th>
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Pallfaktorer</th>
                                <th scope="col" className="px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">Åtgärd</th>
                            </tr>
                        </thead>
                        <tbody className={`${!loading && items.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                            {items.map((item) => {
                                const isSaving = savingIds.includes(item.id)
                                const hasChanges = item.palletFactors !== item.originalPalletFactors

                                return (
                                    <tr key={item.id} className="h-6 border-b border-gray-100 hover:bg-lime-200/70">
                                        <td className="truncate px-2 py-1 align-middle text-gray-800">{item.lengthFrom.toLocaleString('sv-SE')}</td>
                                        <td className="truncate px-2 py-1 align-middle text-gray-800">{item.lengthTo.toLocaleString('sv-SE')}</td>
                                        <td className="truncate px-2 py-1 align-middle text-gray-800">{item.widthFrom.toLocaleString('sv-SE')}</td>
                                        <td className="truncate px-2 py-1 align-middle text-gray-800">{item.widthTo.toLocaleString('sv-SE')}</td>
                                        <td className="px-2 py-1 align-middle">
                                            <LabeledTextArea
                                                label=""
                                                labelWidth="w-0"
                                                inputWidth="w-full"
                                                height="h-10"
                                                rows={2}
                                                maxLength={500}
                                                disabled={isSaving}
                                                value={item.palletFactors}
                                                onChange={(value) => updatePalletFactors(item.id, value)}
                                                aria-label={`Pallfaktorer för post ${item.id}`}
                                            />
                                        </td>
                                        <td className="truncate px-2 py-1 align-middle text-gray-800">
                                            <ActionButton
                                                label="Spara"
                                                icon={Save}
                                                accent="lime"
                                                disabled={!hasChanges || isSaving}
                                                onClick={() => void savePalletFactors(item)}
                                            />
                                        </td>
                                    </tr>
                                )
                            })}
                            {!loading && activeFilters && items.length === 0 && (
                                <tr>
                                    <td colSpan={6} className="px-3 py-8 text-center text-xs text-gray-500">
                                        Inga pallfaktorer matchar filtren.
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    )
}

export default PalletFactors