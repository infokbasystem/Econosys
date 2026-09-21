import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer,
} from "recharts";
import { ArrowLeftCircle, ArrowRightCircle } from "lucide-react";
import Skeleton from "react-loading-skeleton";
import "react-loading-skeleton/dist/skeleton.css";

import apiClient from "../../config/apiClient";
import { getSharedRequest } from "../../helpers/sharedRequest";
import { formatDateShort } from "../../helpers/dateUtils";

const TABLE_PAGE_SIZE = 10;
const DEFAULT_SELECTED_YEARS = 5;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const UNINVOICED_COLUMNS = [
  { key: "customerOrderNr", label: "Order", align: "left", width: "14%" },
  { key: "customerName", label: "Kund", align: "left", width: "32%" },
  { key: "quantity", label: "Antal", align: "right", width: "12%" },
  { key: "value", label: "Värde", align: "right", width: "22%" },
  { key: "deliveryDate", label: "Datum", align: "right", width: "20%" },
];

const UNBOOKED_COLUMNS = [
  { key: "invoiceNumber", label: "Faktura", align: "left", width: "13%" },
  { key: "customerName", label: "Kund", align: "left", width: "30%" },
  { key: "amount", label: "Belopp", align: "right", width: "20%" },
  { key: "invoiceDate", label: "Fakturadatum", align: "right", width: "19%" },
  { key: "dueDate", label: "Förfaller", align: "right", width: "18%" },
];

const MONTH_LABELS = ["Jan", "Feb", "Mar", "Apr", "Maj", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"];
const YEAR_COLORS = ["#3b82f6", "#10b981", "#f59e0b", "#8b5cf6", "#94a3b8", "#ec4899", "#14b8a6", "#ef4444"];

const EMPTY_PAGE = {
  items: [],
  pageNumber: 1,
  totalPages: 0,
  totalCount: 0,
  hasPreviousPage: false,
  hasNextPage: false,
  totals: null,
};

const fmt = (n) =>
  new Intl.NumberFormat("sv-SE", { style: "currency", currency: "SEK", maximumFractionDigits: 0 }).format(Number(n) || 0);

const formatAxisTick = (value) => {
  const amount = Number(value) || 0;
  if (Math.abs(amount) >= 1000000) return `${Math.round(amount / 100000) / 10}M`;
  return `${Math.round(amount / 1000)}k`;
};

const toPageState = (data) => ({
  items: data?.items ?? [],
  pageNumber: data?.pageNumber ?? 1,
  totalPages: data?.totalPages ?? 0,
  totalCount: data?.totalCount ?? 0,
  hasPreviousPage: Boolean(data?.hasPreviousPage),
  hasNextPage: Boolean(data?.hasNextPage),
  totals: data?.totals?.values ?? null,
});

const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-slate-200 rounded-lg px-3 py-2 shadow-xl text-xs">
      <p className="text-slate-400 mb-1 font-medium">{label}</p>
      {payload.map((p) => (
        <p key={p.name} style={{ color: p.color }} className="my-0.5">{p.name}: {fmt(p.value)}</p>
      ))}
    </div>
  );
};

const KPI_VARIANTS = {
  emerald: {
    card: "bg-lime-50 border-lime-500/20",
    accent: "bg-lime-500",
    label: "text-lime-500",
    value: "text-lime-800",
  },
  amber: {
    card: "bg-sky-50 border-sky-500/20",
    accent: "bg-sky-500",
    label: "text-sky-500",
    value: "text-sky-800",
  },
  rose: {
    card: "bg-rose-50 border-rose-500/20",
    accent: "bg-rose-500",
    label: "text-rose-500",
    value: "text-rose-800",
  },
};

const KpiCard = ({ label, value, sub, variant }) => {
  const colors = KPI_VARIANTS[variant] ?? KPI_VARIANTS.emerald;

  return (
    <div className={`flex-none w-60 relative py-3 px-5 overflow-hidden border shadow-lg ${colors.card}`}>
      <div className={`absolute top-0 left-0 right-0 h-0.5 ${colors.accent}`} />
      <p className={`mt-1 uppercase tracking-widest mb-1 font-semibold text-tiny ${colors.label}`}>{label}</p>
      <p className={`mt-2 font-bold text-lg leading-tight ${colors.value}`}>{value}</p>
      <p className="mt-1.5 text-slate-700 text-tiny">{sub}</p>
    </div>
  );
};

const Pager = ({ page, loading, onPageChange }) => {
  if (!page.totalCount) return null;

  return (
    <div className="flex items-center gap-4 text-xs text-gray-600" style={TABLE_FONT_STYLE}>
      <span>Rader <strong>{page.totalCount}</strong></span>
      <span>Sida {page.pageNumber} av {Math.max(1, page.totalPages)}</span>
      <div className="flex gap-1">
        <button
          type="button"
          onClick={() => onPageChange(page.pageNumber - 1)}
          disabled={loading || !page.hasPreviousPage}
          className="disabled:cursor-not-allowed disabled:opacity-40"
        >
          <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
        </button>
        <button
          type="button"
          onClick={() => onPageChange(page.pageNumber + 1)}
          disabled={loading || !page.hasNextPage}
          className="disabled:cursor-not-allowed disabled:opacity-40"
        >
          <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
        </button>
      </div>
    </div>
  );
};


const FinanceOverview = () => {
  const [overview, setOverview] = useState(null);
  const [overviewLoading, setOverviewLoading] = useState(true);
  const [activeYears, setActiveYears] = useState(() => new Set());

  const [uninvoiced, setUninvoiced] = useState(EMPTY_PAGE);
  const [uninvoicedLoading, setUninvoicedLoading] = useState(true);
  const [uninvoicedPage, setUninvoicedPage] = useState(1);

  const [unbooked, setUnbooked] = useState(EMPTY_PAGE);
  const [unbookedLoading, setUnbookedLoading] = useState(true);
  const [unbookedPage, setUnbookedPage] = useState(1);

  const didSelectDefaultYears = useRef(false);

  useEffect(() => {
    let isActive = true;

    const load = async () => {
      setOverviewLoading(true);

      try {
        const response = await getSharedRequest("finance:overview", () => apiClient.get("/finance/overview"));
        if (!isActive) return;
        setOverview(response?.data ?? null);
      } catch (error) {
        console.error("Failed to load finance overview:", error);
        if (isActive) setOverview(null);
      } finally {
        if (isActive) setOverviewLoading(false);
      }
    };

    load();

    return () => {
      isActive = false;
    };
  }, []);

  useEffect(() => {
    let isActive = true;

    const load = async () => {
      setUninvoicedLoading(true);

      try {
        const requestBody = { pageNumber: uninvoicedPage, pageSize: TABLE_PAGE_SIZE };
        const response = await getSharedRequest(
          `finance:uninvoiced-deliveries:${uninvoicedPage}`,
          () => apiClient.post("/finance/uninvoiced-deliveries", requestBody)
        );
        if (!isActive) return;
        setUninvoiced(toPageState(response?.data));
      } catch (error) {
        console.error("Failed to load uninvoiced deliveries:", error);
        if (isActive) setUninvoiced(EMPTY_PAGE);
      } finally {
        if (isActive) setUninvoicedLoading(false);
      }
    };

    load();

    return () => {
      isActive = false;
    };
  }, [uninvoicedPage]);

  useEffect(() => {
    let isActive = true;

    const load = async () => {
      setUnbookedLoading(true);

      try {
        const requestBody = { pageNumber: unbookedPage, pageSize: TABLE_PAGE_SIZE };
        const response = await getSharedRequest(
          `finance:unbooked-invoices:${unbookedPage}`,
          () => apiClient.post("/finance/unbooked-invoices", requestBody)
        );
        if (!isActive) return;
        setUnbooked(toPageState(response?.data));
      } catch (error) {
        console.error("Failed to load unbooked invoices:", error);
        if (isActive) setUnbooked(EMPTY_PAGE);
      } finally {
        if (isActive) setUnbookedLoading(false);
      }
    };

    load();

    return () => {
      isActive = false;
    };
  }, [unbookedPage]);

  // The API returns years newest first, so the palette index doubles as a recency rank.
  const years = useMemo(() => {
    return (overview?.years ?? []).map((year, index) => ({
      key: `y${year}`,
      year,
      label: String(year),
      color: YEAR_COLORS[index % YEAR_COLORS.length],
      opacity: Math.max(0.35, 1 - index * 0.13),
    }));
  }, [overview]);

  // Chart and toggles render oldest-to-newest; `years` stays newest-first for the default selection.
  const displayYears = useMemo(() => [...years].reverse(), [years]);

  useEffect(() => {
    if (didSelectDefaultYears.current || years.length === 0) return;
    didSelectDefaultYears.current = true;
    setActiveYears(new Set(years.slice(0, DEFAULT_SELECTED_YEARS).map((entry) => entry.key)));
  }, [years]);

  const monthlyData = useMemo(() => {
    const rows = MONTH_LABELS.map((month) => ({ month }));

    (overview?.monthly ?? []).forEach((point) => {
      const row = rows[point.month - 1];
      if (row) {
        row[`y${point.year}`] = Number(point.amount) || 0;
      }
    });

    return rows;
  }, [overview]);

  const chartTitle = useMemo(() => {
    const selected = years.filter((entry) => activeYears.has(entry.key)).map((entry) => entry.year);
    if (selected.length === 0) return "fakturering per månad";

    const min = Math.min(...selected);
    const max = Math.max(...selected);
    return `fakturering per månad (${min === max ? min : `${min}-${max}`})`;
  }, [years, activeYears]);

  const toggleYear = (key) => {
    setActiveYears((prev) => {
      const next = new Set(prev);
      if (next.has(key) && next.size === 1) return prev; // keep at least one
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  };

  const ytdChange = overview?.ytdChangePercent;
  const ytdSub = ytdChange === null || ytdChange === undefined
    ? "Ingen jämförelse mot föregående år"
    : `${ytdChange > 0 ? "+" : ""}${ytdChange}% vs föregående år`;

  const uninvoicedTotal = uninvoiced.totals?.totalValue ?? overview?.notInvoicedTotal ?? 0;
  const uninvoicedCount = uninvoiced.totals?.totalDeliveries ?? overview?.notInvoicedCount ?? 0;
  const unbookedTotal = unbooked.totals?.totalAmount ?? overview?.notBookedTotal ?? 0;
  const unbookedCount = unbooked.totals?.totalInvoices ?? overview?.notBookedCount ?? 0;

  return (
    <div className="flex flex-col h-full mt-2 p-5 gap-2">

      {/* ── KPI Row ── */}
      <div className="flex gap-10 justify-center">
        <KpiCard
          label="Fakturerat YTD"
          value={overviewLoading ? "—" : fmt(overview?.invoicedYtd)}
          sub={overviewLoading ? "Laddar…" : ytdSub}
          variant="emerald"
        />
        <KpiCard
          label="Ej fakturerade"
          value={fmt(uninvoicedTotal)}
          sub={`${uninvoicedCount} leveranser att fakturera`}
          variant="amber"
        />
        <KpiCard
          label="Ännu ej bokförda fakturor"
          value={fmt(unbookedTotal)}
          sub={`${unbookedCount} fakturor att bokföra`}
          variant="rose"
        />
      </div>

      {/* ── Chart ── */}
      <div className="mx-20 mt-8 bg-stone-200/50 border border-slate-200 px-7 py-5">
        <div className="flex items-center justify-between mb-2">
          <p className="text-tiny text-slate-700 uppercase tracking-widest font-semibold">
            {chartTitle}
          </p>
          {/* Year toggles */}
          <div className="flex gap-1.5 items-center flex-wrap justify-end">
            {displayYears.map(({ key, label, color }) => {
              const active = activeYears.has(key);
              return (
                <button
                  key={key}
                  onClick={() => toggleYear(key)}
                  className="flex items-center gap-1 px-2 py-1 rounded-full border transition-all"
                  style={{
                    fontSize: 9,
                    borderColor: active ? color : "#e2e8f0",
                    background: active ? color + "18" : "transparent",
                    color: active ? color : "#94a3b8",
                    fontWeight: active ? 600 : 400,
                  }}
                >
                  <span
                    className="inline-block w-1.5 h-1.5 rounded-full"
                    style={{ background: active ? color : "#cbd5e1" }}
                  />
                  {label}
                </button>
              );
            })}
          </div>
        </div>
        {overviewLoading ? (
          <Skeleton height={300} />
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={monthlyData} margin={{ top: 4, right: 8, left: 4, bottom: 0 }} barGap={1} barCategoryGap="20%">
              <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" vertical={false} />
              <XAxis dataKey="month" tick={{ fill: "#94a3b8", fontSize: 9 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: "#94a3b8", fontSize: 9 }} axisLine={false} tickLine={false} width={44} tickFormatter={formatAxisTick} />
              <Tooltip content={<CustomTooltip />} cursor={{ fill: "#f8fafc" }} />
              {displayYears.map(({ key, label, color, opacity }) =>
                activeYears.has(key) ? (
                  <Bar key={key} dataKey={key} name={label} fill={color} radius={[2, 2, 0, 0]} opacity={opacity} />
                ) : null
              )}
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>

      {/* ── Tables ── */}
      <div className="mx-20 mt-5 grid grid-cols-2 gap-15">

        {/* Deliveries awaiting invoice */}
        <div className="px-7 py-5">
          <div className="flex items-center justify-between mb-2">
            <p className="pl-2 text-tiny text-slate-700 uppercase tracking-widest font-semibold">
              Leveranser som ännu inte fakturerats
            </p>
            <Pager page={uninvoiced} loading={uninvoicedLoading} onPageChange={setUninvoicedPage} />
          </div>
          <div className="border-t border-gray-300 py-1 mt-0 overflow-auto">
            <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
              <colgroup>
                {UNINVOICED_COLUMNS.map((column) => (
                  <col key={column.key} style={{ width: column.width }} />
                ))}
              </colgroup>
              <thead>
                <tr className="text-tiny text-gray-500">
                  {UNINVOICED_COLUMNS.map((column) => (
                    <th
                      key={column.key}
                      className={`px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === "right" ? "text-right" : "text-left"}`}
                    >
                      {column.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className={!uninvoicedLoading && uninvoiced.items.length > 0 ? "bg-white" : "bg-transparent"}>
                {uninvoicedLoading ? (
                  Array.from({ length: TABLE_PAGE_SIZE }).map((_, index) => (
                    <tr key={index}>
                      <td colSpan={UNINVOICED_COLUMNS.length} className="px-2 py-1"><Skeleton height={16} /></td>
                    </tr>
                  ))
                ) : uninvoiced.items.length === 0 ? (
                  <tr>
                    <td colSpan={UNINVOICED_COLUMNS.length} className="px-4 py-8 text-center text-gray-400">Inga leveranser att fakturera.</td>
                  </tr>
                ) : (
                  uninvoiced.items.map((row) => (
                    <tr key={row.deliveryId} className="h-6 border-b border-gray-100 hover:bg-lime-200/70">
                      <td className="truncate px-2 py-1 text-gray-800">{row.customerOrderNr ?? "-"}</td>
                      <td className="truncate px-2 py-1 text-gray-800">{row.customerName ?? "-"}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{row.quantity ?? 0}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{fmt(row.value)}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{formatDateShort(row.deliveryDate)}</td>
                    </tr>
                  ))
                )}
              </tbody>
              <tfoot>
                <tr className="border-t border-gray-300 text-gray-800">
                  <td colSpan={3} className="px-2 py-1 text-gray-500">Totalt</td>
                  <td colSpan={2} className="px-2 py-1 text-right font-semibold">{fmt(uninvoicedTotal)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        {/* Invoices pending accounting */}
        <div className="px-7 py-5">
          <div className="flex items-center justify-between mb-2">
            <p className="pl-2 text-tiny text-slate-700 uppercase tracking-widest font-semibold">
              Fakturor som ännu inte bokförts
            </p>
            <Pager page={unbooked} loading={unbookedLoading} onPageChange={setUnbookedPage} />
          </div>
          <div className="border-t border-gray-300 py-1 mt-0 overflow-auto">
            <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
              <colgroup>
                {UNBOOKED_COLUMNS.map((column) => (
                  <col key={column.key} style={{ width: column.width }} />
                ))}
              </colgroup>
              <thead>
                <tr className="text-tiny text-gray-500">
                  {UNBOOKED_COLUMNS.map((column) => (
                    <th
                      key={column.key}
                      className={`px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === "right" ? "text-right" : "text-left"}`}
                    >
                      {column.label}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className={!unbookedLoading && unbooked.items.length > 0 ? "bg-white" : "bg-transparent"}>
                {unbookedLoading ? (
                  Array.from({ length: TABLE_PAGE_SIZE }).map((_, index) => (
                    <tr key={index}>
                      <td colSpan={UNBOOKED_COLUMNS.length} className="px-2 py-1"><Skeleton height={16} /></td>
                    </tr>
                  ))
                ) : unbooked.items.length === 0 ? (
                  <tr>
                    <td colSpan={UNBOOKED_COLUMNS.length} className="px-4 py-8 text-center text-gray-400">Inga fakturor att bokföra.</td>
                  </tr>
                ) : (
                  unbooked.items.map((row) => (
                    <tr key={row.invoiceId} className="h-6 border-b border-gray-100 hover:bg-lime-200/70">
                      <td className="truncate px-2 py-1">
                        <Link
                          to={`/finance/invoice/${row.invoiceId}`}
                          className="text-slate-700 hover:text-slate-900 hover:underline"
                        >
                          {row.invoiceNumber ?? "-"}
                        </Link>
                      </td>
                      <td className="truncate px-2 py-1 text-gray-800">{row.customerName ?? "-"}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{fmt(row.amount)}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{formatDateShort(row.invoiceDate)}</td>
                      <td className="truncate px-2 py-1 text-right text-gray-800">{formatDateShort(row.dueDate)}</td>
                    </tr>
                  ))
                )}
              </tbody>
              <tfoot>
                <tr className="border-t border-gray-300 text-gray-800">
                  <td colSpan={2} className="px-2 py-1 text-gray-500">Totalt att bokföra</td>
                  <td colSpan={3} className="px-2 py-1 text-right font-semibold">{fmt(unbookedTotal)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      </div>


    </div>
  )

}

export default FinanceOverview