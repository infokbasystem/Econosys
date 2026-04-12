import { useState } from "react";
import {
  AreaChart, Area, BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip,
  Legend, ResponsiveContainer,
} from "recharts";


const monthlyData = [
  { month: "Jan", y2024: 42000, y2023: 35000, y2022: 28000, y2021: 22000, y2020: 18000 },
  { month: "Feb", y2024: 58000, y2023: 41000, y2022: 33000, y2021: 27000, y2020: 21000 },
  { month: "Mar", y2024: 51000, y2023: 47000, y2022: 38000, y2021: 31000, y2020: 25000 },
  { month: "Apr", y2024: 74000, y2023: 52000, y2022: 44000, y2021: 36000, y2020: 29000 },
  { month: "May", y2024: 63000, y2023: 59000, y2022: 50000, y2021: 41000, y2020: 33000 },
  { month: "Jun", y2024: 89000, y2023: 64000, y2022: 57000, y2021: 46000, y2020: 38000 },
  { month: "Jul", y2024: 71000, y2023: 55000, y2022: 48000, y2021: 39000, y2020: 31000 },
  { month: "Aug", y2024: 95000, y2023: 68000, y2022: 61000, y2021: 50000, y2020: 42000 },
  { month: "Sep", y2024: 82000, y2023: 73000, y2022: 65000, y2021: 53000, y2020: 45000 },
  { month: "Oct", y2024: 108000, y2023: 79000, y2022: 70000, y2021: 58000, y2020: 49000 },
  { month: "Nov", y2024: 97000, y2023: 85000, y2022: 76000, y2021: 63000, y2020: 54000 },
  { month: "Dec", y2024: 0, y2023: 91000, y2022: 82000, y2021: 68000, y2020: 58000 },
];

const YEARS = [
  { key: "y2024", label: "2024", color: "#3b82f6" },
  { key: "y2023", label: "2023", color: "#10b981" },
  { key: "y2022", label: "2022", color: "#f59e0b" },
  { key: "y2021", label: "2021", color: "#8b5cf6" },
  { key: "y2020", label: "2020", color: "#94a3b8" },
];

const pendingOrders = [
  { id: "ORD-2024-0891", client: "Nordström & Co", items: 4, value: 18400, date: "2024-11-28" },
  { id: "ORD-2024-0892", client: "Bergström Tech", items: 2, value: 7200, date: "2024-11-29" },
  { id: "ORD-2024-0893", client: "Lindqvist AB", items: 7, value: 31500, date: "2024-12-01" },
  { id: "ORD-2024-0894", client: "Svensson Group", items: 1, value: 4900, date: "2024-12-02" },
  { id: "ORD-2024-0895", client: "Eriksson & Partners", items: 3, value: 12600, date: "2024-12-03" },
];

const pendingAccounting = [
  { id: "INV-2024-1142", client: "Mattsson Industries", amount: 22400, due: "2024-12-05" },
  { id: "INV-2024-1148", client: "Johansson Retail", amount: 8750, due: "2024-12-10" },
  { id: "INV-2024-1151", client: "Persson Logistics", amount: 34100, due: "2024-12-14" },
  { id: "INV-2024-1156", client: "Gustafsson & Sons", amount: 15200, due: "2024-12-18" },
  { id: "INV-2024-1161", client: "Karlsson Media", amount: 6300, due: "2024-12-22" },
];

const fmt = (n) =>
  new Intl.NumberFormat("sv-SE", { style: "currency", currency: "SEK", maximumFractionDigits: 0 }).format(n);

const totalPendingOrders = pendingOrders.reduce((s, o) => s + o.value, 0);
const totalPendingAccounting = pendingAccounting.reduce((s, i) => s + i.amount, 0);
const invoicedNotDue = 142800;
const TODAY = new Date("2024-12-04");

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

const KpiCard = ({ label, value, sub, accentColor, bgColor, textColor }) => (
  <div className="flex-none w-60 relative p-3 overflow-hidden border" style={{ background: bgColor, borderColor: accentColor + "33" }}>
    <div className="absolute top-0 left-0 right-0 h-0.5" style={{ background: `${accentColor}` }} />
    <p className="uppercase tracking-widest mb-1 font-semibold text-tiny" style={{ color: accentColor }}>{label}</p>
    <p className="font-bold text-lg leading-tight" style={{ color: textColor }}>{value}</p>
    <p className="mt-0.5 text-slate-400 text-tiny" style={{}}>{sub}</p>
  </div>
);


const FinanceOverview = () => {
const [activeYears, setActiveYears] = useState(new Set(["y2024", "y2023", "y2022", "y2021", "y2020"]));

  const toggleYear = (key) => {
    setActiveYears((prev) => {
      const next = new Set(prev);
      if (next.has(key) && next.size === 1) return prev; // keep at least one
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  };



  return (
    <div className="flex flex-col h-full mt-2 p-2 gap-2">

      {/* ── KPI Row ── */}
      <div className="flex gap-10 justify-center">
        <KpiCard
          label="Fakturerat YTD"
          value="830 000 SEK"
          sub="+22% vs föregående år"
          accentColor="#10b981"
          bgColor="#f0fdf4"
          textColor="#065f46"
        />
        <KpiCard
          label="Ej fakturerade"
          value={fmt(totalPendingOrders)}
          sub={`${pendingOrders.length} leveranser att fakturera`}
          accentColor="#f59e0b"
          bgColor="#fffbeb"
          textColor="#92400e"
        />
        <KpiCard
          label="Skickat, ännu ej förfallet"
          value={fmt(invoicedNotDue)}
          sub="Väntar på betalning"
          accentColor="#3b82f6"
          bgColor="#eff6ff"
          textColor="#1e3a8a"
        />
        <KpiCard
          label="Ännu ej bokförda fakturor"
          value={fmt(totalPendingAccounting)}
          sub={`${pendingAccounting.length} fakturor att bokföra`}
          accentColor="#f43f5e"
          bgColor="#fff1f2"
          textColor="#9f1239"
        />
      </div>

      {/* ── Chart ── */}
      <div className="mx-20 mt-5 bg-slate-50 border border-slate-200 px-7 py-5 shadow-sm">
        <div className="flex items-center justify-between mb-2">
          <p className="text-tiny text-slate-400 uppercase tracking-widest font-semibold" style={{  }}>
            fakturering per månad (2020-2024)
          </p>
          {/* Year toggles */}
          <div className="flex gap-1.5 items-center">
            {YEARS.map(({ key, label, color }) => {
              const active = activeYears.has(key);
              return (
                <button
                  key={key}
                  onClick={() => toggleYear(key)}
                  className="flex items-center gap-1 px-2 py-0.5 rounded-full border transition-all"
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
        <ResponsiveContainer width="100%" height={255}>
          <BarChart data={monthlyData} margin={{ top: 4, right: 8, left: -22, bottom: 0 }} barGap={1} barCategoryGap="20%">
            <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" vertical={false} />
            <XAxis dataKey="month" tick={{ fill: "#94a3b8", fontSize: 9 }} axisLine={false} tickLine={false} />
            <YAxis tick={{ fill: "#94a3b8", fontSize: 9 }} axisLine={false} tickLine={false} tickFormatter={(v) => `${v / 1000}k`} />
            <Tooltip content={<CustomTooltip />} cursor={{ fill: "#f8fafc" }} />
            {YEARS.map(({ key, label, color }) =>
              activeYears.has(key) ? (
                <Bar key={key} dataKey={key} name={label} fill={color} radius={[2, 2, 0, 0]}
                  opacity={key === "y2024" ? 1 : key === "y2023" ? 0.75 : key === "y2022" ? 0.6 : key === "y2021" ? 0.5 : 0.35}
                />
              ) : null
            )}
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* ── Tables ── */}
      <div className="mx-20 mt-5 grid grid-cols-2 gap-10">

        {/* Orders awaiting invoice */}
        <div className="bg-slate-50 border border-slate-200 px-7 py-5 shadow-sm">
          <p className="text-tiny text-slate-400 uppercase tracking-widest font-semibold mb-2" style={{ }}>
            Leveranser som ännu inte fakturerats
          </p>
          <table className="w-full border-collapse">
            <thead>
              <tr className="text-slate-400 uppercase" style={{ fontSize: 9, letterSpacing: "0.06em" }}>
                <th className="text-left pb-1.5 font-medium">Order</th>
                <th className="text-left pb-1.5 font-medium pl-1">Client</th>
                <th className="text-right pb-1.5 font-medium">Items</th>
                <th className="text-right pb-1.5 font-medium">Value</th>
                <th className="text-right pb-1.5 font-medium">Date</th>
              </tr>
            </thead>
            <tbody>
              {pendingOrders.map((o, i) => (
                <tr
                  key={o.id}
                  className={`hover:bg-slate-50 transition-colors cursor-default ${i % 2 !== 0 ? "bg-slate-50/60" : ""}`}
                >
                  <td className="py-1 text-blue-500" style={{ fontSize: 10, fontFamily: "monospace" }}>{o.id}</td>
                  <td className="py-1 pl-1 text-slate-600">{o.client}</td>
                  <td className="py-1 text-slate-400 text-right">{o.items}</td>
                  <td className="py-1 text-amber-600 text-right font-medium">{fmt(o.value)}</td>
                  <td className="py-1 text-slate-400 text-right">{o.date.slice(5)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-100">
                <td colSpan={3} className="pt-1.5 text-slate-400">Total</td>
                <td colSpan={2} className="pt-1.5 text-amber-600 text-right font-bold" style={{ fontFamily: "'Syne',sans-serif", fontSize: 13 }}>
                  {fmt(totalPendingOrders)}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>

        {/* Invoices pending accounting */}
        <div className="bg-slate-50 border border-slate-200 px-7 py-5 shadow-sm">
          <p className="text-tiny text-slate-400 uppercase tracking-widest font-semibold mb-2" style={{ }}>
            Fakturor som ännu inte bokförts
          </p>
          <table className="w-full border-collapse">
            <thead>
              <tr className="text-slate-400 uppercase" style={{ fontSize: 9, letterSpacing: "0.06em" }}>
                <th className="text-left pb-1.5 font-medium">Invoice</th>
                <th className="text-left pb-1.5 font-medium pl-1">Client</th>
                <th className="text-right pb-1.5 font-medium">Amount</th>
                <th className="text-right pb-1.5 font-medium">Due</th>
                <th className="text-right pb-1.5 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {pendingAccounting.map((inv, i) => {
                const daysLeft = Math.round((new Date(inv.due) - TODAY) / 86400000);
                const urgent = daysLeft <= 7;
                return (
                  <tr
                    key={inv.id}
                    className={`hover:bg-slate-50 transition-colors cursor-default ${i % 2 !== 0 ? "bg-slate-50/60" : ""}`}
                  >
                    <td className="py-1 text-blue-500" style={{ fontSize: 10, fontFamily: "monospace" }}>{inv.id}</td>
                    <td className="py-1 pl-1 text-slate-600">{inv.client}</td>
                    <td className="py-1 text-rose-500 text-right font-medium">{fmt(inv.amount)}</td>
                    <td className="py-1 text-slate-400 text-right">{inv.due.slice(5)}</td>
                    <td className="py-1 text-right">
                      <span
                        className={`inline-block px-1.5 py-0.5 rounded font-medium ${urgent
                            ? "bg-rose-50 text-rose-500 ring-1 ring-rose-200"
                            : "bg-blue-50 text-blue-500 ring-1 ring-blue-100"
                          }`}
                        style={{ fontSize: 9 }}
                      >
                        {daysLeft}d left
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-100">
                <td colSpan={2} className="pt-1.5 text-slate-400">Total to book</td>
                <td colSpan={3} className="pt-1.5 text-rose-500 text-right font-bold" style={{ fontFamily: "'Syne',sans-serif", fontSize: 13 }}>
                  {fmt(totalPendingAccounting)}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>


    </div>
  )

}

export default FinanceOverview