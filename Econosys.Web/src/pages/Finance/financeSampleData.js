// Sample finance data for FinanceOverview
export const ordersWaitingToInvoice = [
  { id: 1, customer: "Acme AB", product: "Widget A", value: 12000, date: "2026-02-20" },
  { id: 2, customer: "BetaCorp", product: "Widget B", value: 8500, date: "2026-02-21" },
  { id: 3, customer: "Gamma LLC", product: "Widget C", value: 4300, date: "2026-02-22" },
];

export const invoicesWaitingToAccount = [
  { id: 101, customer: "Acme AB", value: 12000, sent: "2026-02-20", due: "2026-03-20" },
  { id: 102, customer: "BetaCorp", value: 8500, sent: "2026-02-21", due: "2026-03-21" },
];

export const monthlyInvoiced = [
  { month: "Jan", year: 2025, value: 32000 },
  { month: "Feb", year: 2025, value: 28000 },
  { month: "Mar", year: 2025, value: 35000 },
  { month: "Apr", year: 2025, value: 30000 },
  { month: "May", year: 2025, value: 37000 },
  { month: "Jun", year: 2025, value: 39000 },
  { month: "Jul", year: 2025, value: 41000 },
  { month: "Aug", year: 2025, value: 38000 },
  { month: "Sep", year: 2025, value: 36000 },
  { month: "Oct", year: 2025, value: 40000 },
  { month: "Nov", year: 2025, value: 42000 },
  { month: "Dec", year: 2025, value: 44000 },
  { month: "Jan", year: 2026, value: 35000 },
  { month: "Feb", year: 2026, value: 37000 },
]

export const productsNotYetInvoicedValue = 24800;
export const invoicesSentNotDueValue = 20500;
