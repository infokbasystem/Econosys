import { useEffect, useState } from 'react'
import apiClient from '../../../config/apiClient'
import { getSharedRequest } from '../../../helpers/sharedRequest'
import Select from '../../../components/Select'

const numberFormatter = new Intl.NumberFormat('sv-SE')

const formatCount = (value) => {
    if (value === null || value === undefined) return '–'
    return numberFormatter.format(value)
}

const formatDays = (value) => {
    if (value === null || value === undefined) return '–'
    return `${String(value).replace('.', ',')} dagar`
}

const formatPercent = (value) => {
    if (value === null || value === undefined) return '–'
    return `${String(value).replace('.', ',')} %`
}

const HandlingTimesCreatedBy = () => {
    const [year, setYear] = useState(null)
    const [availableYears, setAvailableYears] = useState([])
    const [goalNrOfDays, setGoalNrOfDays] = useState(null)
    const [rows, setRows] = useState([])
    const [totalRow, setTotalRow] = useState(null)
    const [loading, setLoading] = useState(false)

    useEffect(() => {
        let isActive = true

        const load = async () => {
            setLoading(true)

            try {
                const response = await getSharedRequest(
                    `reporting:handling-times:created-by:${year ?? 'latest'}`,
                    () => apiClient.get('/reporting/handling-times/created-by', { params: year ? { year } : {} })
                )

                if (!isActive) return

                const data = response?.data ?? {}
                setYear(data.year ?? null)
                setAvailableYears(data.availableYears ?? [])
                setGoalNrOfDays(data.goalNrOfDays ?? null)
                setRows(data.rows ?? [])
                setTotalRow(data.total ?? null)
            } catch (error) {
                console.error('Failed to load handling times created-by report:', error)
                if (!isActive) return
                setAvailableYears([])
                setRows([])
                setTotalRow(null)
            } finally {
                if (!isActive) return
                setLoading(false)
            }
        }

        load()

        return () => {
            isActive = false
        }
    }, [year])

    const underGoalLabel = goalNrOfDays
        ? `% inom ${numberFormatter.format(goalNrOfDays)} dagar`
        : '% inom mål'

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="mb-4 w-32">
                <Select
                    value={year ?? ''}
                    items={availableYears.map((availableYear) => ({ id: availableYear, name: String(availableYear) }))}
                    stylePreset="search"
                    onChange={(value) => setYear(Number(value))}
                />
            </div>

            <div className={`w-fit ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <table className="w-full border-collapse text-xs">
                    <thead>
                        <tr className="border-b border-gray-200 text-gray-500">
                            <th className="py-2 pr-3 text-left font-normal">Användare</th>
                            <th className="py-2 px-3 text-right font-normal">Skapade best totalt</th>
                            <th className="py-2 px-3 text-right font-normal">Skapade OE nya/ändrade</th>
                            <th className="py-2 px-3 text-right font-normal">Skapade OE repeat</th>
                            <th className="py-2 px-3 text-right font-normal">Andel nya OE</th>
                            <th className="py-2 px-3 text-right font-normal">Medel dagar repeat</th>
                            <th className="py-2 pl-3 text-right font-normal">{underGoalLabel}</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((row) => (
                            <tr key={row.userId ?? row.userLabel} className="border-b border-gray-100">
                                <td className="py-2 pr-3 text-left text-gray-600">{row.userLabel}</td>
                                <td className="py-2 px-3 text-right text-gray-800">{formatCount(row.totalSupplierOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-800">{formatCount(row.newOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-800">{formatCount(row.repeatOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-800">{formatPercent(row.newOrderSharePercent)}</td>
                                <td className="py-2 px-3 text-right text-gray-800">{formatDays(row.averageRepeatHandlingDays)}</td>
                                <td className="py-2 pl-3 text-right font-semibold text-gray-800">{formatPercent(row.repeatPercentUnderGoalDays)}</td>
                            </tr>
                        ))}
                        {rows.length === 0 && !loading && (
                            <tr>
                                <td colSpan={7} className="py-6 text-center text-gray-400">
                                    Ingen data
                                </td>
                            </tr>
                        )}
                        {totalRow && (
                            <tr className="border-t border-gray-300 font-semibold">
                                <td className="py-2 pr-3 text-left text-gray-900">{totalRow.userLabel}</td>
                                <td className="py-2 px-3 text-right text-gray-900">{formatCount(totalRow.totalSupplierOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-900">{formatCount(totalRow.newOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-900">{formatCount(totalRow.repeatOrderCount)}</td>
                                <td className="py-2 px-3 text-right text-gray-900">{formatPercent(totalRow.newOrderSharePercent)}</td>
                                <td className="py-2 px-3 text-right text-gray-900">{formatDays(totalRow.averageRepeatHandlingDays)}</td>
                                <td className="py-2 pl-3 text-right text-gray-900">{formatPercent(totalRow.repeatPercentUnderGoalDays)}</td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    )
}

export default HandlingTimesCreatedBy
