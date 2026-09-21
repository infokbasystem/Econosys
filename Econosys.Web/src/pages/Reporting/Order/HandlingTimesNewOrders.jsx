import { Fragment, useEffect, useMemo, useState } from 'react'
import apiClient from '../../../config/apiClient'
import { getSharedRequest } from '../../../helpers/sharedRequest'
import { getSwedishTodayDateString } from '../../../helpers/dateUtils'

const numberFormatter = new Intl.NumberFormat('sv-SE')
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'Maj', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dec']

const formatDaysShort = (value) => {
    if (value === null || value === undefined) return ''
    return String(value).replace('.', ',')
}

const HandlingTimesNewOrders = () => {
    const [years, setYears] = useState([])
    const [loading, setLoading] = useState(false)
    const [showAllYears, setShowAllYears] = useState(false)

    useEffect(() => {
        let isActive = true

        const load = async () => {
            setLoading(true)

            try {
                const response = await getSharedRequest(
                    'reporting:handling-times:orders-yearly-monthly:new',
                    () => apiClient.get('/reporting/handling-times/orders-yearly-monthly', { params: { orderType: 'new' } })
                )

                if (!isActive) return
                const fetchedYears = response?.data?.years ?? []
                setYears(fetchedYears.filter((year) => year.totalAverageHandlingTimeDays !== null && year.totalAverageHandlingTimeDays !== undefined))
            } catch (error) {
                console.error('Failed to load handling times yearly/monthly report:', error)
                if (!isActive) return
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
    }, [])

    const yearComparisons = useMemo(
        () => years.slice(1).map((yearRow, index) => {
            const previousYearRow = years[index]
            const previousByMonth = new Map(previousYearRow.months.map((month) => [month.monthNumber, month]))
            const currentByMonth = new Map(yearRow.months.map((month) => [month.monthNumber, month]))

            const monthDiffs = MONTH_LABELS.map((_, monthIndex) => {
                const monthNumber = monthIndex + 1
                const previousAverage = previousByMonth.get(monthNumber)?.averageHandlingTimeDays
                const currentAverage = currentByMonth.get(monthNumber)?.averageHandlingTimeDays

                if (previousAverage === null || previousAverage === undefined || currentAverage === null || currentAverage === undefined) {
                    return null
                }

                return Math.round((previousAverage - currentAverage) * 10) / 10
            })

            const totalDiff = previousYearRow.totalAverageHandlingTimeDays !== null && previousYearRow.totalAverageHandlingTimeDays !== undefined
                && yearRow.totalAverageHandlingTimeDays !== null && yearRow.totalAverageHandlingTimeDays !== undefined
                ? Math.round((previousYearRow.totalAverageHandlingTimeDays - yearRow.totalAverageHandlingTimeDays) * 10) / 10
                : null

            return {
                year: yearRow.year,
                previousYear: previousYearRow.year,
                monthDiffs,
                totalDiff,
            }
        }),
        [years]
    )

    const visibleYears = showAllYears ? years : years.slice(-1)
    const visibleComparisons = showAllYears ? yearComparisons : yearComparisons.slice(-1)

    const handleExportToExcel = () => {
        const escapeCsv = (value) => `"${String(value ?? '').replace(/"/g, '""')}"`
        const monthHeaders = [...MONTH_LABELS, 'Totalt']

        const mainRows = visibleYears.flatMap((yearRow) => {
            const monthByNumber = new Map(yearRow.months.map((month) => [month.monthNumber, month]))
            const countValues = MONTH_LABELS.map((_, index) => monthByNumber.get(index + 1)?.orderCount ?? '')
            const avgValues = MONTH_LABELS.map((_, index) => formatDaysShort(monthByNumber.get(index + 1)?.averageHandlingTimeDays))

            return [
                [`${yearRow.year} Antal order`, ...countValues, numberFormatter.format(yearRow.totalOrderCount)],
                [`${yearRow.year} Medel Hanteringstid`, ...avgValues, formatDaysShort(yearRow.totalAverageHandlingTimeDays)],
            ]
        })

        const comparisonRows = visibleComparisons.map((comparison) => [
            `${comparison.year} vs ${comparison.previousYear}`,
            ...comparison.monthDiffs.map((diff) => formatDaysShort(diff)),
            formatDaysShort(comparison.totalDiff),
        ])

        const lines = [
            ['Nya/ändrade order', ...monthHeaders].map(escapeCsv).join(';'),
            ...mainRows.map((row) => row.map(escapeCsv).join(';')),
            [],
            ['Förändring', ...monthHeaders].map(escapeCsv).join(';'),
            ...comparisonRows.map((row) => row.map(escapeCsv).join(';')),
        ].map((line) => (Array.isArray(line) ? line.join(';') : line))

        const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' })
        const url = URL.createObjectURL(blob)
        const link = document.createElement('a')
        const datePart = getSwedishTodayDateString()

        link.href = url
        link.download = `hanteringstider-nya-order-${datePart}.csv`
        document.body.appendChild(link)
        link.click()
        document.body.removeChild(link)
        URL.revokeObjectURL(url)
    }

    const hasData = years.length > 0

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="mb-5 flex items-center gap-10 text-xs mx-auto">
                {years.length > 1 && (
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault()
                            setShowAllYears((current) => !current)
                        }}
                        className="inline-flex items-center whitespace-nowrap font-medium text-slate-500 transition-colors hover:text-slate-700"
                    >
                        {showAllYears ? 'Visa endast senaste perioden' : 'Visa alla år'}
                    </a>
                )}
                <a
                    href="#"
                    onClick={(event) => {
                        event.preventDefault()
                        if (!hasData) return
                        handleExportToExcel()
                    }}
                    aria-disabled={!hasData}
                    className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${hasData
                        ? 'text-slate-500 hover:text-slate-700'
                        : 'text-gray-300 cursor-not-allowed pointer-events-none'
                        }`}
                >
                    Exportera till Excel
                </a>
            </div>
            <div className={`flex items-start gap-10 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="flex-1 overflow-auto">
                    <table className="min-w-full border-collapse text-xs">
                        <thead>
                            <tr className="border-b border-gray-200 text-gray-500">
                                <th className="sticky left-0 px-3 py-2 text-left font-medium" colSpan={2}>Nya/ändrade order</th>
                                {MONTH_LABELS.map((month) => (
                                    <th key={month} className="px-2 py-2 text-right font-medium">{month}</th>
                                ))}
                                <th className="px-2 py-2 text-right font-medium">Totalt</th>
                            </tr>
                        </thead>
                        <tbody>
                            {visibleYears.map((yearRow) => {
                                const monthByNumber = new Map(yearRow.months.map((month) => [month.monthNumber, month]))

                                return (
                                    <Fragment key={yearRow.year}>
                                        <tr className="border-b border-gray-100">
                                            <td rowSpan={2} className="sticky left-0 align-middle px-3 py-2 text-left font-medium text-gray-700">
                                                {yearRow.year}
                                            </td>
                                            <td className="px-3 py-2 text-left text-gray-600">Antal order</td>
                                            {MONTH_LABELS.map((_, index) => {
                                                const month = monthByNumber.get(index + 1)
                                                return (
                                                    <td key={`${yearRow.year}-count-${index}`} className="px-2 py-2 text-right text-gray-800">
                                                        {month ? numberFormatter.format(month.orderCount ?? 0) : ''}
                                                    </td>
                                                )
                                            })}
                                            <td className="px-2 py-2 text-right font-semibold text-gray-900">
                                                {numberFormatter.format(yearRow.totalOrderCount)}
                                            </td>
                                        </tr>
                                        <tr className="border-b border-gray-200">
                                            <td className="px-3 py-2 text-left text-gray-600">Medel Hanteringstid</td>
                                            {MONTH_LABELS.map((_, index) => {
                                                const month = monthByNumber.get(index + 1)
                                                return (
                                                    <td key={`${yearRow.year}-avg-${index}`} className="px-2 py-2 text-right text-gray-800">
                                                        {month ? formatDaysShort(month.averageHandlingTimeDays) : ''}
                                                    </td>
                                                )
                                            })}
                                            <td className="px-2 py-2 text-right font-semibold text-gray-900">
                                                {formatDaysShort(yearRow.totalAverageHandlingTimeDays)}
                                            </td>
                                        </tr>
                                    </Fragment>
                                )
                            })}
                            {visibleYears.length === 0 && !loading && (
                                <tr>
                                    <td colSpan={MONTH_LABELS.length + 3} className="py-6 text-center text-gray-400">
                                        Ingen data
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>

                <div className="flex-1 overflow-auto">
                    <table className="min-w-full border-collapse text-xs">
                        <thead>
                            <tr className="border-b border-gray-200 text-gray-500">
                                <th className="sticky left-0 px-3 py-2 text-left font-medium">Förändring</th>
                                {MONTH_LABELS.map((month) => (
                                    <th key={month} className="px-2 py-2 text-right font-medium">{month}</th>
                                ))}
                                <th className="px-2 py-2 text-right font-medium">Totalt</th>
                            </tr>
                        </thead>
                        <tbody>
                            {visibleComparisons.map((comparison) => (
                                <tr key={comparison.year} className="border-b border-gray-200 last:border-b-0">
                                    <td className="sticky left-0 px-3 py-2 text-left font-medium text-gray-700">
                                        {`${comparison.year} vs ${comparison.previousYear}`}
                                    </td>
                                    {comparison.monthDiffs.map((diff, index) => {
                                        const hasDiff = diff !== null && diff !== undefined
                                        const isImprovement = hasDiff && diff >= 0

                                        return (
                                            <td
                                                key={`${comparison.year}-diff-${index}`}
                                                className={`px-2 py-2 text-right font-semibold ${hasDiff ? (isImprovement ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}
                                            >
                                                {hasDiff ? formatDaysShort(diff) : ''}
                                            </td>
                                        )
                                    })}
                                    <td className={`px-2 py-2 text-right font-semibold ${comparison.totalDiff !== null ? (comparison.totalDiff >= 0 ? 'text-green-600' : 'text-red-600') : 'text-gray-400'}`}>
                                        {formatDaysShort(comparison.totalDiff)}
                                    </td>
                                </tr>
                            ))}
                            {visibleComparisons.length === 0 && !loading && (
                                <tr>
                                    <td colSpan={MONTH_LABELS.length + 2} className="py-6 text-center text-gray-400">
                                        Ingen data
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    )
}

export default HandlingTimesNewOrders
