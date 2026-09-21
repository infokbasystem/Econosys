import { useEffect, useMemo, useState } from 'react'
import { TrendingDown, TrendingUp } from 'lucide-react'
import apiClient from '../../../config/apiClient'
import { getSharedRequest } from '../../../helpers/sharedRequest'

const numberFormatter = new Intl.NumberFormat('sv-SE')
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'Maj', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dec']

const formatDays = (value) => {
    if (value === null || value === undefined) return '–'
    return `${String(value).replace('.', ',')} dagar`
}

const formatDaysShort = (value) => {
    if (value === null || value === undefined) return ''
    return String(value).replace('.', ',')
}

const formatPercent = (value) => {
    if (value === null || value === undefined) return '–'
    return `${String(value).replace('.', ',')} %`
}

const formatPercentShort = (value) => {
    if (value === null || value === undefined) return ''
    return `${String(value).replace('.', ',')} %`
}

const formatVariance = (value) => {
    if (value === null || value === undefined) return '–'
    const sign = value > 0 ? '+' : value < 0 ? '−' : ''
    const absValue = String(Math.abs(value)).replace('.', ',')
    return `${sign}${absValue} %`
}

const weightedAverage = (rows, valueKey, weightKey) => {
    let totalWeight = 0
    let weightedSum = 0

    rows.forEach((row) => {
        const value = row[valueKey]
        const weight = row[weightKey] ?? 0
        if (value === null || value === undefined || weight <= 0) return
        weightedSum += value * weight
        totalWeight += weight
    })

    return totalWeight > 0 ? weightedSum / totalWeight : null
}

const HandlingTimesOrderTypeReport = ({ orderType }) => {
    const [goalNrOfDays, setGoalNrOfDays] = useState(null)
    const [goalPercent, setGoalPercent] = useState(null)
    const [years, setYears] = useState([])
    const [loading, setLoading] = useState(false)

    const [monthlyReport, setMonthlyReport] = useState(null)
    const [monthlyLoading, setMonthlyLoading] = useState(false)

    useEffect(() => {
        let isActive = true

        const load = async () => {
            setLoading(true)

            try {
                const response = await getSharedRequest(
                    `reporting:handling-times:repeat-orders-by-year:${orderType}`,
                    () => apiClient.get('/reporting/handling-times/repeat-orders-by-year', { params: { orderType } })
                )

                if (!isActive) return
                setGoalNrOfDays(response?.data?.goalNrOfDays ?? null)
                setGoalPercent(response?.data?.goalPercentHandledUnderGoalNrOfDays ?? null)
                const fetchedYears = response?.data?.years ?? []
                setYears(fetchedYears.filter((row) => row.averageHandlingTimeDays !== null && row.averageHandlingTimeDays !== undefined))
            } catch (error) {
                console.error('Failed to load handling times report:', error)
                if (!isActive) return
                setGoalNrOfDays(null)
                setGoalPercent(null)
                setYears([])
            } finally {
                if (!isActive) return
                setLoading(false)
            }
        }

        load()

        return () => {
            isActive = false
        }
    }, [orderType])

    useEffect(() => {
        let isActive = true

        const loadMonthly = async () => {
            setMonthlyLoading(true)

            try {
                const response = await getSharedRequest(
                    `reporting:handling-times:repeat-orders-monthly:${orderType}`,
                    () => apiClient.get('/reporting/handling-times/repeat-orders-monthly', { params: { orderType } })
                )

                if (!isActive) return
                setMonthlyReport(response?.data ?? null)
            } catch (error) {
                console.error('Failed to load handling times monthly report:', error)
                if (!isActive) return
                setMonthlyReport(null)
            } finally {
                if (!isActive) return
                setMonthlyLoading(false)
            }
        }

        loadMonthly()

        return () => {
            isActive = false
        }
    }, [orderType])

    const underGoalLabel = goalNrOfDays
        ? `% under ${numberFormatter.format(goalNrOfDays)} dagar`
        : '% under mål'

    const months = monthlyReport?.months ?? []
    const currentYear = monthlyReport?.currentYear
    const previousYear = monthlyReport?.previousYear

    const monthsWithVariance = useMemo(
        () => months.map((month) => ({
            ...month,
            varianceDays: month.previousYearAverageHandlingTimeDays !== null
                && month.previousYearAverageHandlingTimeDays !== undefined
                && month.currentYearAverageHandlingTimeDays !== null
                && month.currentYearAverageHandlingTimeDays !== undefined
                ? Math.round((month.previousYearAverageHandlingTimeDays - month.currentYearAverageHandlingTimeDays) * 10) / 10
                : null,
        })),
        [months]
    )

    const currentYearTotalDays = useMemo(
        () => weightedAverage(months, 'currentYearAverageHandlingTimeDays', 'currentYearOrderCount'),
        [months]
    )
    const currentYearTotalPercent = useMemo(
        () => weightedAverage(months, 'currentYearPercentUnderGoalDays', 'currentYearOrderCount'),
        [months]
    )
    const previousYearTotalDays = useMemo(
        () => weightedAverage(months, 'previousYearAverageHandlingTimeDays', 'previousYearOrderCount'),
        [months]
    )
    const totalVarianceDays = previousYearTotalDays !== null && currentYearTotalDays !== null
        ? Math.round((previousYearTotalDays - currentYearTotalDays) * 10) / 10
        : null

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className={`flex items-start gap-20 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-100">
                    <table className="w-full border-collapse text-xs">
                        <thead>
                            <tr className="border-b border-gray-200 text-gray-500">
                                <th className="w-[50px] py-2 pr-3 text-left font-normal"></th>
                                <th className="py-2 px-3 text-right font-normal">Antal order</th>
                                <th className="py-2 px-3 text-right font-normal">Medel arbetsdagar</th>
                                <th className="py-2 px-3 text-right font-normal">{underGoalLabel}</th>
                                <th className="w-22 py-2 pl-3 text-right font-normal">Utfall vs KPI</th>
                            </tr>
                        </thead>
                        <tbody>
                            {years.map((row) => {
                                const variance = row.varianceVsKpi
                                const hasVariance = variance !== null && variance !== undefined
                                const isPositive = hasVariance && variance >= 0

                                return (
                                    <tr key={row.year} className="border-b border-gray-200 last:border-b-0">
                                        <td className="py-2 px-3 text-left text-gray-600">{row.year}</td>
                                        <td className="py-2 px-3 text-right text-gray-800">
                                            {numberFormatter.format(row.orderCount)}
                                        </td>
                                        <td className="py-2 px-3 text-right text-gray-800">
                                            {formatDays(row.averageHandlingTimeDays)}
                                        </td>
                                        <td className="py-2 px-3 text-right font-semibold text-gray-800">
                                            {formatPercent(row.percentUnderGoalDays)}
                                        </td>
                                        <td className={`py-2 px-3 text-right font-semibold ${hasVariance ? (isPositive ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}>
                                            <span className="inline-flex items-center gap-1">
                                                {hasVariance && (isPositive
                                                    ? <TrendingUp className="h-3.5 w-3.5" />
                                                    : <TrendingDown className="h-3.5 w-3.5" />)}
                                                {formatVariance(variance)}
                                            </span>
                                        </td>
                                    </tr>
                                )
                            })}
                            {years.length === 0 && !loading && (
                                <tr>
                                    <td colSpan={5} className="py-6 text-center text-gray-400">
                                        Ingen data
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>

                <div className={`mt-5 flex-1 overflow-auto ${monthlyLoading ? 'opacity-70 pointer-events-none' : ''}`}>
                    <table className="min-w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                        <thead>
                            <tr className="border-b border-gray-200 text-tiny text-gray-500">
                                <th className="sticky left-0 text-left font-medium"></th>
                                {MONTH_LABELS.map((month) => (
                                    <th key={month} className="px-2 py-1 text-right font-medium">{month}</th>
                                ))}
                                <th className="px-2 py-1 text-right font-medium">Totalt</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr className="border-b border-gray-200">
                                <td className="sticky left-0 px-3 py-2 text-left text-gray-700">
                                    {`Hanteringstid ${currentYear ?? ''}`}
                                </td>
                                {monthsWithVariance.map((month) => (
                                    <td key={`current-days-${month.monthNumber}`} className="px-2 py-2 text-right text-gray-800">
                                        {formatDaysShort(month.currentYearAverageHandlingTimeDays)}
                                    </td>
                                ))}
                                <td className="px-2 py-2 text-right font-semibold text-gray-900">
                                    {formatDaysShort(currentYearTotalDays !== null ? Math.round(currentYearTotalDays * 10) / 10 : null)}
                                </td>
                            </tr>
                            <tr className="border-b border-gray-200">
                                <td className="sticky left-0 px-3 py-2 text-left text-gray-700">{underGoalLabel}</td>
                                {monthsWithVariance.map((month) => {
                                    const percent = month.currentYearPercentUnderGoalDays
                                    const hasPercent = percent !== null && percent !== undefined
                                    const meetsGoal = hasPercent && goalPercent !== null && goalPercent !== undefined && percent >= goalPercent

                                    return (
                                        <td
                                            key={`current-percent-${month.monthNumber}`}
                                            className={`px-2 py-2 text-right font-semibold ${hasPercent ? (meetsGoal ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}
                                        >
                                            {formatPercentShort(percent)}
                                        </td>
                                    )
                                })}
                                <td className={`px-2 py-2 text-right font-semibold ${currentYearTotalPercent !== null
                                    ? (goalPercent !== null && goalPercent !== undefined && currentYearTotalPercent >= goalPercent ? 'text-green-600' : 'text-red-600')
                                    : 'text-gray-400'
                                    }`}>
                                    {formatPercentShort(currentYearTotalPercent !== null ? Math.round(currentYearTotalPercent * 10) / 10 : null)}
                                </td>
                            </tr>
                            <tr className="border-b border-gray-200">
                                <td className="sticky left-0 px-3 py-2 text-left text-gray-400">
                                    {`Hanteringstid ${previousYear ?? ''}`}
                                </td>
                                {monthsWithVariance.map((month) => (
                                    <td key={`previous-days-${month.monthNumber}`} className="px-2 py-2 text-right text-gray-400">
                                        {formatDaysShort(month.previousYearAverageHandlingTimeDays)}
                                    </td>
                                ))}
                                <td className="px-2 py-2 text-right font-semibold text-gray-400">
                                    {formatDaysShort(previousYearTotalDays !== null ? Math.round(previousYearTotalDays * 10) / 10 : null)}
                                </td>
                            </tr>
                            <tr>
                                <td className="sticky left-0 px-3 py-2 text-left text-gray-700">
                                    {`${currentYear ?? ''} vs ${previousYear ?? ''}`}
                                </td>
                                {monthsWithVariance.map((month) => {
                                    const variance = month.varianceDays
                                    const hasVariance = variance !== null && variance !== undefined
                                    const isPositive = hasVariance && variance >= 0

                                    return (
                                        <td
                                            key={`variance-${month.monthNumber}`}
                                            className={`px-2 py-2 text-right font-semibold ${hasVariance ? (isPositive ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}
                                        >
                                            {formatDaysShort(variance)}
                                        </td>
                                    )
                                })}
                                <td className={`px-2 py-2 text-right font-semibold ${totalVarianceDays !== null ? (totalVarianceDays >= 0 ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}>
                                    {formatDaysShort(totalVarianceDays)}
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    )
}

export default HandlingTimesOrderTypeReport
