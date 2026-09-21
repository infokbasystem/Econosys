import React, { useEffect, useMemo, useRef, useState } from 'react'
import Skeleton from 'react-loading-skeleton'
import 'react-loading-skeleton/dist/skeleton.css'
import { CirclePlus } from 'lucide-react'
import { Link } from 'react-router-dom'

import apiClient from '../../config/apiClient'
import LabeledCheckbox from '../../components/LabeledCheckbox'
import ReportOrderInfoModal from '../../modals/ReportOrderInfoModal'
import { getSharedRequest } from '../../helpers/sharedRequest'
import bg from '../../assets/content.png'
import './CalloffOverview.css'

const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }
const HEADER_BACKGROUND_STYLE = { backgroundImage: `url(${bg})` }
const calloffOverviewCache = { ui: null, data: null }

const topColumns = [
  { key: 'customerOrderNr', label: 'Nr', width: '5%', filterable: true },
  { key: 'customerName', label: 'Kund', width: '9%', filterable: true },
  { key: 'customerCity', label: 'Ort', width: '9%' },
  { key: 'productName', label: 'Produkt', width: '14%', filterable: true },
  { key: 'deliveryTime', label: 'Lev.tid', width: '7%', filterable: true },
  { key: 'calculationDate', label: 'Kalk.tid', width: '7%' },
  { key: 'inventoryName', label: 'Lager', width: '11%', filterable: true },
  { key: 'editionProduced', label: 'Prod', width: '4%', align: 'right' },
  { key: 'editionCustomerOrder', label: 'OE', width: '4%', align: 'right' },
  { key: 'stockBalanceNow', label: 'Saldo nu', width: '5%', align: 'right' },
  { key: 'stockBalance', label: 'Saldo', width: '5%', align: 'right' },
  { key: 'palletsNow', label: 'Pall nu', width: '75px', align: 'right' },
  { key: 'pallets', label: 'Pall', width: '75px', align: 'right' },
  { key: 'palletFormat', label: 'Pallformat', width: '10%' },
  { key: 'logisticsInfo', label: '', width: '2%' },
  { key: 'report', label: 'Rapp', width: '50px' },
  { key: 'plan', label: 'Planera', width: '50px' },
]

const activeColumns = [
  { key: 'id', label: 'Nr', width: '7%' },
  { key: 'isSentToShipper', label: 'Skickad', width: '9%' },
  { key: 'shipperName', label: 'Speditör', width: '17%' },
  { key: 'deliveryDate', label: 'Lev.datum', width: '13%' },
  { key: 'customerOrderNrs', label: 'Ordernr', width: '18%' },
  { key: 'customerNames', label: 'Kunder', width: '20%' },
  { key: 'note', label: 'Notering', width: '16%' },
]

function normalizeFilterValue(value) {
  return String(value ?? '').trim().toLowerCase()
}

function formatNumber(value, digits = 0) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return ''
  return new Intl.NumberFormat('sv-SE', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(Number(value))
}

function formatPalletBalance(value) {
  const groups = String(value ?? '').split('/').map((group) => group.trim())
  const nonZeroGroups = groups.filter((group) => group !== '0')

  return nonZeroGroups.length > 0 ? nonZeroGroups.join(' / ') : groups.length > 0 ? '0' : ''
}

function formatDate(value) {
  if (!value) return ''
  const date = new Date(`${String(value).slice(0, 10)}T00:00:00`)
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat('sv-SE').format(date)
}

function formatIsoDate(value) {
  return value ? String(value).slice(0, 10) : ''
}

function parseDateInputToken(token, now = new Date()) {
  const normalized = String(token ?? '').trim()
  const iso = normalized.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/)
  if (iso) return new Date(Number(iso[1]), Number(iso[2]) - 1, Number(iso[3]))
  const monthDay = normalized.match(/^(\d{1,2})[/-](\d{1,2})$/)
  if (monthDay) return new Date(now.getFullYear(), Number(monthDay[1]) - 1, Number(monthDay[2]))
  const day = normalized.match(/^(\d{1,2})$/)
  return day ? new Date(now.getFullYear(), now.getMonth(), Number(day[1])) : null
}

function parseDateFromCellValue(value) {
  const match = String(value ?? '').match(/(\d{4})-(\d{2})-(\d{2})/)
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null
}

function matchesSmartDeliveryTimeFilter(cellValue, filterValue) {
  const filter = String(filterValue ?? '').trim()
  if (!filter) return true
  const date = parseDateFromCellValue(cellValue)
  if (!date || Number.isNaN(date.getTime())) return normalizeFilterValue(cellValue).includes(normalizeFilterValue(filter))
  if (filter.includes('+')) {
    const [startToken, endToken] = filter.split('+')
    const start = parseDateInputToken(startToken)
    const end = parseDateInputToken(endToken)
    return Boolean(start || end) && (!start || date >= start) && (!end || date <= end)
  }
  const exact = parseDateInputToken(filter)
  return exact ? date.toDateString() === exact.toDateString() : normalizeFilterValue(cellValue).includes(normalizeFilterValue(filter))
}

const SmallStyledCheckbox = ({ checked }) => (
  <span className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-[#e5e7eb] bg-white p-[1px]">
    <span className={`inline-flex h-full w-full items-center justify-center rounded-full border ${checked ? 'border-teal-500 bg-teal-500 text-white' : 'border-[#aaaaaa] bg-white text-gray-500'}`}>
      <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-2.5 w-2.5 ${checked ? 'opacity-100' : 'opacity-0'}`}>
        <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  </span>
)

const MultiSelectCheckboxFilter = ({ options, selectedValues, onChange }) => {
  const [open, setOpen] = useState(false)
  const rootRef = useRef(null)

  useEffect(() => {
    const close = (event) => {
      if (rootRef.current && !rootRef.current.contains(event.target)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  const allSelected = options.length > 0 && selectedValues.length === options.length
  const label = allSelected ? 'Välj alla' : selectedValues.length > 0 ? `${selectedValues.length} valda` : ''
  const toggle = (option) => onChange(selectedValues.includes(option) ? selectedValues.filter((value) => value !== option) : [...selectedValues, option])

  return (
    <div className="relative" ref={rootRef}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        className="flex h-5 w-full items-center justify-between rounded-sm border border-gray-300 bg-white px-1.5 text-left text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none"
        title={label || 'Välj'}
      >
        <span className={`truncate ${label ? 'text-gray-700' : 'text-gray-400'}`}>{label || 'Välj'}</span>
        <span className="text-tiny text-gray-500">▾</span>
      </button>
      {open ? (
        <div className="absolute left-0 top-[26px] z-50 max-h-72 w-[420px] overflow-y-auto rounded-sm border border-gray-300 bg-white p-1.5 shadow-lg">
          <label
            className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-1 text-tiny text-gray-700 hover:bg-gray-50"
            onClick={() => onChange(allSelected ? [] : options)}
          >
            <SmallStyledCheckbox checked={allSelected} />
            <span>Välj alla</span>
          </label>
          {options.map((option) => (
            <label
              key={option}
              className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-0.5 text-tiny text-gray-700 hover:bg-gray-50"
              onClick={() => toggle(option)}
            >
              <SmallStyledCheckbox checked={selectedValues.includes(option)} />
              <span className="truncate">{option}</span>
            </label>
          ))}
        </div>
      ) : null}
    </div>
  )
}

const CalloffOverview = () => {
  const cachedUi = calloffOverviewCache.ui
  const cachedData = calloffOverviewCache.data
  const topScrollRef = useRef(null)
  const initialTopScrollTopRef = useRef(cachedUi?.topScrollTop ?? 0)
  const initialHasTopSnapshotRef = useRef(Boolean(cachedData?.hasTopSnapshot))
  const initialHasActiveSnapshotRef = useRef(Boolean(cachedData?.hasActiveSnapshot))
  const [topRows, setTopRows] = useState(() => cachedData?.topRows ?? [])
  const [activeRows, setActiveRows] = useState(() => cachedData?.activeRows ?? [])
  const [topLoading, setTopLoading] = useState(() => !initialHasTopSnapshotRef.current)
  const [activeLoading, setActiveLoading] = useState(() => !initialHasActiveSnapshotRef.current)
  const [hasTopSnapshot, setHasTopSnapshot] = useState(() => initialHasTopSnapshotRef.current)
  const [hasActiveSnapshot, setHasActiveSnapshot] = useState(() => initialHasActiveSnapshotRef.current)
  const [includePlanned, setIncludePlanned] = useState(() => cachedUi?.includePlanned ?? true)
  const [includeNotPlanned, setIncludeNotPlanned] = useState(() => cachedUi?.includeNotPlanned ?? true)
  const [customerFilter, setCustomerFilter] = useState(() => cachedUi?.customerFilter ?? [])
  const [productFilter, setProductFilter] = useState(() => cachedUi?.productFilter ?? [])
  const [inventoryFilter, setInventoryFilter] = useState(() => cachedUi?.inventoryFilter ?? [])
  const [deliveryTimeFilter, setDeliveryTimeFilter] = useState(() => cachedUi?.deliveryTimeFilter ?? '')
  const [columnFilters, setColumnFilters] = useState(() => ({
    ...Object.fromEntries(topColumns.filter((column) => column.filterable).map((column) => [column.key, ''])),
    ...(cachedUi?.columnFilters ?? {}),
  }))
  const [selectedTopId, setSelectedTopId] = useState(() => cachedUi?.selectedTopId ?? null)
  const [selectedActiveId, setSelectedActiveId] = useState(() => cachedUi?.selectedActiveId ?? null)
  const [logisticsInfoPopup, setLogisticsInfoPopup] = useState(null)
  const [reportOrderInfo, setReportOrderInfo] = useState(null)
  const [reportOrderInfoLoading, setReportOrderInfoLoading] = useState(false)
  const [reportOrderInfoSaving, setReportOrderInfoSaving] = useState(false)
  const [reportOrderInfoError, setReportOrderInfoError] = useState('')
  const [topTableRefreshToken, setTopTableRefreshToken] = useState(0)
  const customerFilterInitializedRef = useRef(Array.isArray(cachedUi?.customerFilter))
  const productFilterInitializedRef = useRef(Array.isArray(cachedUi?.productFilter))
  const inventoryFilterInitializedRef = useRef(Array.isArray(cachedUi?.inventoryFilter))

  const customerOptions = useMemo(
    () => Array.from(new Set(topRows.map((row) => String(row.customerName ?? '').trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'sv-SE')),
    [topRows]
  )
  const productOptions = useMemo(
    () => Array.from(new Set(topRows.map((row) => String(row.productName ?? '').trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'sv-SE')),
    [topRows]
  )
  const inventoryOptions = useMemo(
    () => Array.from(new Set(topRows.map((row) => String(row.inventoryName ?? '').trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b, 'sv-SE')),
    [topRows]
  )

  useEffect(() => {
    if (topScrollRef.current) topScrollRef.current.scrollTop = initialTopScrollTopRef.current
  }, [])

  useEffect(() => {
    const closeLogisticsInfoPopup = () => setLogisticsInfoPopup(null)

    document.addEventListener('mousedown', closeLogisticsInfoPopup)
    window.addEventListener('scroll', closeLogisticsInfoPopup, true)
    return () => {
      document.removeEventListener('mousedown', closeLogisticsInfoPopup)
      window.removeEventListener('scroll', closeLogisticsInfoPopup, true)
    }
  }, [])

  useEffect(() => {
    if (!customerFilterInitializedRef.current) {
      setCustomerFilter(customerOptions)
      customerFilterInitializedRef.current = true
      return
    }
    setCustomerFilter((previous) => previous.filter((value) => customerOptions.includes(value)))
  }, [customerOptions])

  useEffect(() => {
    if (!productFilterInitializedRef.current) {
      setProductFilter(productOptions)
      productFilterInitializedRef.current = true
      return
    }
    setProductFilter((previous) => previous.filter((value) => productOptions.includes(value)))
  }, [productOptions])

  useEffect(() => {
    if (!inventoryFilterInitializedRef.current) {
      setInventoryFilter(inventoryOptions)
      inventoryFilterInitializedRef.current = true
      return
    }
    setInventoryFilter((previous) => previous.filter((value) => inventoryOptions.includes(value)))
  }, [inventoryOptions])

  useEffect(() => {
    calloffOverviewCache.ui = {
      includePlanned,
      includeNotPlanned,
      customerFilter: [...customerFilter],
      productFilter: [...productFilter],
      inventoryFilter: [...inventoryFilter],
      deliveryTimeFilter,
      columnFilters: { ...columnFilters },
      selectedTopId,
      selectedActiveId,
      topScrollTop: topScrollRef.current?.scrollTop ?? calloffOverviewCache.ui?.topScrollTop ?? 0,
    }
  }, [columnFilters, customerFilter, deliveryTimeFilter, includeNotPlanned, includePlanned, inventoryFilter, productFilter, selectedActiveId, selectedTopId])

  useEffect(() => {
    calloffOverviewCache.data = { topRows, activeRows, hasTopSnapshot, hasActiveSnapshot }
  }, [activeRows, hasActiveSnapshot, hasTopSnapshot, topRows])

  useEffect(() => {
    let isActive = true
    const load = async () => {
      if (!initialHasTopSnapshotRef.current) setTopLoading(true)
      try {
        const response = await getSharedRequest(
          `logistics:calloffoverview:top-table:${includePlanned}:${includeNotPlanned}:${topTableRefreshToken}`,
          () => apiClient.get('/logistics/calloffoverview/top-table', { params: { includePlanned, includeNotPlanned } })
        )
        if (isActive) setTopRows(response?.data ?? [])
      } catch (error) {
        console.error('Failed to load call-off queue:', error)
        if (isActive && !initialHasTopSnapshotRef.current) setTopRows([])
      } finally {
        if (isActive) {
          setHasTopSnapshot(true)
          setTopLoading(false)
        }
      }
    }
    const timer = setTimeout(load, 250)
    return () => {
      isActive = false
      clearTimeout(timer)
    }
  }, [includeNotPlanned, includePlanned, topTableRefreshToken])

  useEffect(() => {
    let isActive = true
    const load = async () => {
      if (!initialHasActiveSnapshotRef.current) setActiveLoading(true)
      try {
        const response = await getSharedRequest(
          'logistics:calloffoverview:active-table',
          () => apiClient.get('/logistics/calloffoverview/active-table')
        )
        if (isActive) setActiveRows(response?.data ?? [])
      } catch (error) {
        console.error('Failed to load active call-offs:', error)
        if (isActive && !initialHasActiveSnapshotRef.current) setActiveRows([])
      } finally {
        if (isActive) {
          setHasActiveSnapshot(true)
          setActiveLoading(false)
        }
      }
    }
    load()
    return () => {
      isActive = false
    }
  }, [])

  const filteredTopRows = useMemo(() => {
    const customerSet = new Set(customerFilter)
    const productSet = new Set(productFilter)
    const inventorySet = new Set(inventoryFilter)
    return topRows.filter((row) => {
      if (customerSet.size > 0 && !customerSet.has(String(row.customerName ?? '').trim())) return false
      if (productSet.size > 0 && !productSet.has(String(row.productName ?? '').trim())) return false
      if (inventorySet.size > 0 && !inventorySet.has(String(row.inventoryName ?? '').trim())) return false
      if (!matchesSmartDeliveryTimeFilter(row.deliveryTime, deliveryTimeFilter)) return false
      return Object.entries(columnFilters).every(([key, value]) => (
        key === 'customerName'
        || key === 'productName'
        || key === 'inventoryName'
        || key === 'deliveryTime'
        || !normalizeFilterValue(value)
        || normalizeFilterValue(row[key]).includes(normalizeFilterValue(value))
      ))
    })
  }, [columnFilters, customerFilter, deliveryTimeFilter, inventoryFilter, productFilter, topRows])

  const openReportOrderInfo = async (customerOrderId) => {
    setReportOrderInfoError('')
    setReportOrderInfoLoading(true)
    setReportOrderInfo({ customerOrderId, orderedEdition: null, producedEdition: null, isCompleted: false })

    try {
      const response = await apiClient.get(`/logistics/transportorderoverview/order-info/${customerOrderId}`)
      setReportOrderInfo({
        customerOrderId,
        orderedEdition: response?.data?.orderedEdition ?? null,
        producedEdition: response?.data?.producedEdition ?? null,
        isCompleted: Boolean(response?.data?.isCompleted),
      })
    } catch (error) {
      console.error('Failed to load order info:', error)
      setReportOrderInfoError('Kunde inte hämta orderinformation.')
    } finally {
      setReportOrderInfoLoading(false)
    }
  }

  const handleSaveReportOrderInfo = async ({ producedEdition, isCompleted }) => {
    if (!reportOrderInfo) return

    setReportOrderInfoSaving(true)
    setReportOrderInfoError('')

    try {
      await apiClient.put(`/logistics/transportorderoverview/order-info/${reportOrderInfo.customerOrderId}`, {
        producedEdition,
        isCompleted,
      })
      setReportOrderInfo(null)
      setTopTableRefreshToken((previous) => previous + 1)
    } catch (error) {
      console.error('Failed to save order info:', error)
      setReportOrderInfoError('Kunde inte spara orderinformation.')
    } finally {
      setReportOrderInfoSaving(false)
    }
  }

  const renderTopCell = (row, column) => {
    if (column.key === 'logisticsInfo') {
      return (
        <td key={column.key} className="px-2 py-1 text-center">
          {row.hasLogisticsInfo ? (
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation()
                const { left, bottom } = event.currentTarget.getBoundingClientRect()
                setLogisticsInfoPopup({ text: row.logisticsInfoInternal, left, top: bottom + 8 })
              }}
              className="inline-flex text-violet-500 hover:text-violet-700 focus:outline-none focus-visible:ring-1 focus-visible:ring-amber-600"
              title="Visa logistikinfo"
              aria-label="Visa logistikinfo"
            >
              <CirclePlus className="h-4 w-4" aria-hidden="true" />
            </button>
          ) : null}
        </td>
      )
    }

    if (column.key === 'report') {
      return (
        <td key={column.key} className="px-2 py-1 text-gray-700">
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation()
              openReportOrderInfo(row.customerOrderId)
            }}
            className="text-slate-600 hover:text-slate-800 hover:underline"
          >
            Rapp.
          </button>
        </td>
      )
    }

    if (column.key === 'plan') {
      return (
        <td key={column.key} className="px-2 py-1 text-gray-700">
          <button type="button" disabled className="cursor-not-allowed text-gray-400" title="Planera kommer i nästa steg">Planera</button>
        </td>
      )
    }

    if (column.key === 'calculationDate') {
      return (
        <td key={column.key} className={`truncate px-2 py-1 ${row.isCalculationDateOverdue ? 'text-red-600' : 'text-gray-800'}`}>
          {formatIsoDate(row.calculationDate)}
        </td>
      )
    }

    const value = column.key === 'stockBalanceNow' || column.key === 'stockBalance' || column.key === 'editionProduced' || column.key === 'editionCustomerOrder'
      ? formatNumber(row[column.key])
      : column.key === 'palletsNow' || column.key === 'pallets'
        ? formatPalletBalance(row[column.key])
        : row[column.key]
    const isEditionColumn = column.key === 'editionProduced' || column.key === 'editionCustomerOrder'
    return (
      <td key={column.key} className={`truncate px-2 py-1 text-gray-800 ${column.align === 'right' ? 'text-right' : ''} ${isEditionColumn ? 'font-semibold' : ''}`}>
        {value}
      </td>
    )
  }

  const getTopRowClassName = (row) => {
    const isSelected = row.customerOrderId === selectedTopId

    if (row.isPlanned) {
      return isSelected ? 'bg-amber-400/80' : 'bg-amber-200/70 hover:bg-amber-300/80'
    }

    if (row.isOnActiveTransportOrder) {
      return isSelected ? 'bg-lime-500/70' : 'bg-lime-300/70 hover:bg-lime-400/80'
    }

    return isSelected ? 'bg-slate-300/80' : 'hover:bg-slate-200/80'
  }

  return (
    <div className="grid grid-rows-[520px_auto] gap-6 pt-1 pb-4 px-[clamp(4px,3vw,6vw)]">
      {logisticsInfoPopup ? (
        <div
          role="status"
          className="fixed z-50 max-w-[420px] rounded-sm border border-gray-200 bg-yellow-50 px-4 py-3 text-left text-xs text-gray-800 shadow-lg"
          style={{ left: logisticsInfoPopup.left, top: logisticsInfoPopup.top }}
        >
          <div className="calloff-logistics-info-text whitespace-pre-line">
            {logisticsInfoPopup.text}
          </div>
        </div>
      ) : null}
      <section className="flex min-h-0 flex-col">
        <div className="mb-1 flex flex-wrap items-center justify-between gap-4 text-xs text-gray-700">
          <div className="w-100 flex items-center gap-4">
            <LabeledCheckbox label="Planerade" name="includePlanned" checked={includePlanned} onChange={setIncludePlanned} disabled={topLoading} className="shrink-0" />
            <LabeledCheckbox label="Ej planerade" name="includeNotPlanned" checked={includeNotPlanned} onChange={setIncludeNotPlanned} disabled={topLoading} className="shrink-0" />
          </div>
          <div className="px-4 text-center text-xs text-gray-500">Ordererkännanden som skall avropas</div>
          <div className="w-100 flex items-center justify-end gap-3 text-xs text-gray-600">
            <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
              <span className="h-3 w-3 border border-amber-300 bg-amber-200/70" aria-hidden="true" />
              Pågående avrop
            </span>
            <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
              <span className="h-3 w-3 border border-lime-400 bg-lime-300/70" aria-hidden="true" />
              Pågående transportorder
            </span>
            <span className="whitespace-nowrap ml-20">Rader {filteredTopRows.length}</span>
          </div>
        </div>
        <div
          ref={topScrollRef}
          onScroll={(event) => {
            setLogisticsInfoPopup(null)
            calloffOverviewCache.ui = {
              ...(calloffOverviewCache.ui ?? {}),
              topScrollTop: event.currentTarget.scrollTop,
            }
          }}
          className="calloff-overview-scroll min-h-0 flex-1 overflow-auto border-t border-gray-300 pt-0"
          style={HEADER_BACKGROUND_STYLE}
        >
          <table className="table-fixed w-full border-separate border-spacing-0 text-xs" style={TABLE_FONT_STYLE}>
            <colgroup>
              {topColumns.map((column) => <col key={column.key} style={{ width: column.width }} />)}
            </colgroup>
            <thead>
              <tr className="sticky top-[-1px] z-30 border-b border-gray-200 text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                {topColumns.map((column) => (
                  <th key={column.key} className={`align-top px-1.5 pt-2 pb-1 text-tiny font-medium ${column.align === 'right' ? 'text-right' : 'text-left'}`} style={HEADER_BACKGROUND_STYLE}>
                    <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                    {column.key === 'customerName' ? (
                      <MultiSelectCheckboxFilter options={customerOptions} selectedValues={customerFilter} onChange={setCustomerFilter} />
                    ) : column.key === 'productName' ? (
                      <MultiSelectCheckboxFilter options={productOptions} selectedValues={productFilter} onChange={setProductFilter} />
                    ) : column.key === 'deliveryTime' ? (
                      <input type="text" value={deliveryTimeFilter} onChange={(event) => setDeliveryTimeFilter(event.target.value)} placeholder={column.label} className="h-5 w-full rounded-sm border border-gray-300 bg-white px-1.5 text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none" />
                    ) : column.key === 'inventoryName' ? (
                      <MultiSelectCheckboxFilter options={inventoryOptions} selectedValues={inventoryFilter} onChange={setInventoryFilter} />
                    ) : column.filterable ? (
                      <input type="text" value={columnFilters[column.key] ?? ''} onChange={(event) => setColumnFilters((previous) => ({ ...previous, [column.key]: event.target.value }))} placeholder={column.label} className="h-5 w-full rounded-sm border border-gray-300 bg-white px-1.5 text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none" />
                    ) : <div className="h-[24px]" aria-hidden="true" />}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {topLoading && !hasTopSnapshot ? (
                Array.from({ length: 10 }).map((_, index) => <tr key={index}><td colSpan={topColumns.length} className="px-2 py-1"><Skeleton height={16} /></td></tr>)
              ) : filteredTopRows.length === 0 ? <tr><td colSpan={topColumns.length} className="px-4 py-8 text-center text-gray-400">Inga ordererkännanden att visa.</td></tr> : filteredTopRows.map((row) => <tr key={row.customerOrderId} role="button" tabIndex={0} aria-selected={row.customerOrderId === selectedTopId} onClick={() => setSelectedTopId(row.customerOrderId)} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelectedTopId(row.customerOrderId) } }} className={['h-6 cursor-pointer', getTopRowClassName(row), 'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600'].join(' ')}>{topColumns.map((column) => renderTopCell(row, column))}</tr>)}
            </tbody>
          </table>
        </div>
      </section>
      <section className="mt-6 grid grid-cols-1 gap-20 lg:grid-cols-[minmax(0,1fr)_minmax(0,5fr)_minmax(0,1fr)]">
        <div className="lg:col-start-2">
          <div className="pb-2 text-center text-xs text-gray-600">Pågående avrop</div>
          <div className="border-t border-gray-300 pt-1">
            <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
              <colgroup>
                {activeColumns.map((column) => <col key={column.key} style={{ width: column.width }} />)}
              </colgroup>
              <thead>
                <tr className="text-tiny text-gray-500" style={HEADER_BACKGROUND_STYLE}>
                  {activeColumns.map((column) => (
                    <th key={column.key} className="align-top px-1.5 pt-2 pb-1 text-left text-tiny font-medium" style={HEADER_BACKGROUND_STYLE}>
                      <div className="px-0.5 pb-1 text-tiny">{column.label}</div>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {activeLoading && !hasActiveSnapshot ? (
                  Array.from({ length: 8 }).map((_, index) => <tr key={index}><td colSpan={activeColumns.length} className="px-2 py-1"><Skeleton height={16} /></td></tr>)
                ) : activeRows.length === 0 ? (
                  <tr><td colSpan={activeColumns.length} className="px-2 py-3 text-center text-gray-400">Inga pågående avrop.</td></tr>
                ) : activeRows.map((row) => (
                  <tr
                    key={row.id}
                    role="button"
                    tabIndex={0}
                    aria-selected={row.id === selectedActiveId}
                    onClick={() => setSelectedActiveId(row.id)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault()
                        setSelectedActiveId(row.id)
                      }
                    }}
                    className={['h-6 cursor-pointer border-b border-gray-100', row.id === selectedActiveId ? 'bg-lime-200/80' : '', 'hover:bg-lime-200/70 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-amber-600'].join(' ')}
                  >
                    <td className="truncate px-2 py-1 text-gray-800">
                      <Link to={`/logistics/calloff/${row.id}`} onClick={(event) => event.stopPropagation()} className="truncate text-slate-700 hover:text-slate-900 hover:underline" title={`Öppna avrop ${row.id}`}>
                        {row.id}
                      </Link>
                    </td>
                    <td className="px-2 py-1 text-gray-800"><SmallStyledCheckbox checked={Boolean(row.isSentToShipper)} /></td>
                    <td className="truncate px-2 py-1 text-gray-800">{row.shipperName}</td>
                    <td className="truncate px-2 py-1 text-gray-800">{formatDate(row.deliveryDate)}</td>
                    <td className="truncate px-2 py-1 text-gray-800">{row.customerOrderNrs}</td>
                    <td className="truncate px-2 py-1 text-gray-800">{row.customerNames}</td>
                    <td className="truncate px-2 py-1 text-gray-800">{row.note}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>
      <ReportOrderInfoModal
        isOpen={reportOrderInfo !== null}
        onClose={() => {
          if (reportOrderInfoSaving) return
          setReportOrderInfo(null)
          setReportOrderInfoError('')
        }}
        onSave={handleSaveReportOrderInfo}
        orderedEdition={reportOrderInfo?.orderedEdition ?? null}
        producedEdition={reportOrderInfo?.producedEdition ?? null}
        isCompleted={reportOrderInfo?.isCompleted ?? false}
        isLoading={reportOrderInfoLoading}
        isSaving={reportOrderInfoSaving}
        errorMessage={reportOrderInfoError}
      />
    </div>
  )
}

export default CalloffOverview
