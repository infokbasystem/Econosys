import { useState, useCallback, useRef, useEffect } from "react";

/* ─── Preset definitions ─── */
const ALL_PRESETS = {
  "this-month":     "Denna månad",
  "last-month":     "Förra månaden",
  "last-3-months":  "Senaste 3 månaderna",
  "last-12-months": "Senaste 12 månaderna",
  "last-year":      "Förra året",
  "year-to-date":   "År till dags dato",
};
const DEFAULT_PRESET_KEYS = Object.keys(ALL_PRESETS);

function getPresetRange(key) {
  const now = new Date(); now.setHours(0,0,0,0);
  const Y = now.getFullYear(), M = now.getMonth();
  switch (key) {
    case "this-month":     return { start: new Date(Y,M,1),      end: new Date(Y,M+1,0) };
    case "last-month":     return { start: new Date(Y,M-1,1),    end: new Date(Y,M,0) };
    case "last-3-months":  return { start: new Date(Y,M-3,1),    end: new Date(Y,M+1,0) };
    case "last-12-months": return { start: new Date(Y,M-12,1),   end: new Date(Y,M+1,0) };
    case "last-year":      return { start: new Date(Y-1,0,1),    end: new Date(Y-1,11,31) };
    case "year-to-date":   return { start: new Date(Y,0,1),      end: now };
    default: return null;
  }
}

/* ─── Date helpers ─── */
const MONTHS = ["Januari","Februari","Mars","April","Maj","Juni","Juli","Augusti","September","Oktober","November","December"];
const DAYS   = ["Sö","Må","Ti","On","To","Fr","Lö"];

const sameDay = (a,b) => a && b && a.toDateString() === b.toDateString();
const between = (d,s,e) => {
  if (!d||!s||!e) return false;
  return d > new Date(Math.min(s,e)) && d < new Date(Math.max(s,e));
};
const fmt = d => d ? `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}` : null;
const fmtShort = d => d ? `${MONTHS[d.getMonth()]} ${d.getDate()}` : null;

/* ─── Month grid ─── */
function MonthGrid({ year, month, startDate, endDate, hoverDate, onDay, onHover }) {
  const first = new Date(year, month, 1).getDay();
  const total = new Date(year, month+1, 0).getDate();
  const rangeEnd = endDate || hoverDate;
  const today = new Date(); today.setHours(0,0,0,0);

  const cells = [
    ...Array(first).fill(null),
    ...Array.from({ length: total }, (_,i) => new Date(year, month, i+1))
  ];

  return (
    <div className="flex-1 min-w-0">
      {/* Day headers */}
      <div className="grid grid-cols-7 mb-1">
        {DAYS.map(d => (
          <div key={d} className="text-center text-tiny font-semibold text-gray-400 py-1 tracking-wide">{d}</div>
        ))}
      </div>

      {/* Date cells */}
      <div className="grid grid-cols-7 gap-y-0.5">
        {cells.map((date, i) => {
          if (!date) return <div key={`e${i}`} />;

          const isStart    = sameDay(date, startDate);
          const isEnd      = sameDay(date, endDate || (hoverDate || null));
          const inRange    = between(date, startDate, rangeEnd);
          const isToday    = sameDay(date, today);
          const isSelected = isStart || isEnd;

          return (
            <div
              key={date.toISOString()}
              className="relative flex items-center justify-center h-8 cursor-pointer select-none group"
              onClick={() => onDay(date)}
              onMouseEnter={() => onHover(date)}
            >
              {/* Range fill */}
              {inRange && (
                <div className="absolute inset-y-[3px] -inset-x-px bg-lime-100 z-0" />
              )}
              {/* Pill */}
              <div className={[
                "relative z-10 w-8 h-8 rounded-full flex items-center justify-center text-xs transition-colors duration-75",
                isSelected
                  ? "bg-lime-100 text-lime-900 font-semibold"
                  : inRange
                    ? "text-lime-900"
                    : isToday
                      ? "ring-[2px] ring-teal-500 text-gray-900 font-bold"
                      : "text-gray-700 group-hover:bg-gray-100",
              ].join(" ")}>
                {date.getDate()}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ─── Chevron icons ─── */
const ChevronLeft  = () => <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>;
const ChevronRight = () => <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>;
const CalIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
  </svg>
);

/* ─── Main component ─── */
/**
 * DateRangePicker — popup select style
 *
 * Props:
 *   presets   {string[]}  keys from: "this-month" | "last-month" | "last-3-months"
 *                         | "last-12-months" | "last-year" | "year-to-date"
 *                         Default: all. Pass [] to hide sidebar.
 *   onApply   {fn}        called with { startDate, endDate }
 *   placeholder {string}  trigger button label when nothing selected
 */
export default function DateRangePicker({
  presets = DEFAULT_PRESET_KEYS,
  onApply,
  placeholder = "Select date range",
  initialPresetKey = null,
}) {
  const today = new Date(); today.setHours(0,0,0,0);

  const [open,        setOpen]        = useState(false);
  const [leftYear,    setLeftYear]    = useState(today.getFullYear());
  const [leftMonth,   setLeftMonth]   = useState(today.getMonth() === 0 ? 11 : today.getMonth() - 1);
  const [startDate,   setStartDate]   = useState(null);
  const [endDate,     setEndDate]     = useState(null);
  const [hoverDate,   setHoverDate]   = useState(null);
  const [selecting,   setSelecting]   = useState(false);
  const [activeKey,   setActiveKey]   = useState(null);

  /* committed values shown in trigger */
  const [committed, setCommitted] = useState({ start: null, end: null });

  const findMatchingPresetKey = useCallback((start, end) => {
    if (!start || !end) return null;
    for (const key of presets) {
      const range = getPresetRange(key);
      if (range && sameDay(range.start, start) && sameDay(range.end, end)) {
        return key;
      }
    }
    return null;
  }, [presets]);

  useEffect(() => {
    if (!initialPresetKey) return;
    const initialRange = getPresetRange(initialPresetKey);
    if (!initialRange) return;

    setStartDate(initialRange.start);
    setEndDate(initialRange.end);
    setCommitted({ start: initialRange.start, end: initialRange.end });
    setActiveKey(initialPresetKey);
    setSelecting(false);
    setHoverDate(null);
    setLeftMonth(initialRange.start.getMonth());
    setLeftYear(initialRange.start.getFullYear());
  }, [initialPresetKey]);

  const wrapRef = useRef(null);
  const rightMonth = leftMonth === 11 ? 0 : leftMonth + 1;
  const rightYear  = leftMonth === 11 ? leftYear + 1 : leftYear;

  /* close on outside click */
  useEffect(() => {
    const handler = e => { if (wrapRef.current && !wrapRef.current.contains(e.target)) setOpen(false); };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const goLeft = () => {
    if (leftMonth === 0) { setLeftMonth(11); setLeftYear(y => y-1); }
    else setLeftMonth(m => m-1);
  };
  const goRight = () => {
    if (leftMonth === 11) { setLeftMonth(0); setLeftYear(y => y+1); }
    else setLeftMonth(m => m+1);
  };

  const handleDay = useCallback(date => {
    if (!selecting || !startDate) {
      setStartDate(date); setEndDate(null); setSelecting(true); setActiveKey(null);
    } else {
      const [s, e] = date < startDate ? [date, startDate] : [startDate, date];
      setStartDate(s); setEndDate(e); setSelecting(false); setHoverDate(null);
    }
  }, [selecting, startDate]);

  const handleHover = useCallback(date => {
    if (selecting) setHoverDate(date);
  }, [selecting]);

  const handlePreset = key => {
    const r = getPresetRange(key); if (!r) return;
    setStartDate(r.start); setEndDate(r.end);
    setActiveKey(key); setSelecting(false); setHoverDate(null);
    setLeftMonth(r.start.getMonth()); setLeftYear(r.start.getFullYear());
    setCommitted({ start: r.start, end: r.end });
    onApply?.({ startDate: r.start, endDate: r.end });
    setOpen(false);
  };

  const handleApply = () => {
    if (!startDate || !endDate) return;
    setCommitted({ start: startDate, end: endDate });
    onApply?.({ startDate, endDate });
    setOpen(false);
  };

  const handleClear = e => {
    e.stopPropagation();
    setStartDate(null); setEndDate(null); setCommitted({ start: null, end: null });
    setActiveKey(null); setSelecting(false);
  };

  const hasCommitted = committed.start && committed.end;
  const canApply = Boolean(startDate && endDate);

  /* reset internal state to committed when reopening */
  const handleOpen = () => {
    setStartDate(committed.start); setEndDate(committed.end);
    setSelecting(false); setHoverDate(null);
    setActiveKey(findMatchingPresetKey(committed.start, committed.end));
    if (committed.start) {
      setLeftMonth(committed.start.getMonth());
      setLeftYear(committed.start.getFullYear());
    }
    setOpen(v => !v);
  };

  const showSidebar = presets.length > 0;

  return (
    <div
      ref={wrapRef}
      className="relative inline-block"
    >
      {/* ── Trigger button ── */}
      <button
        onClick={handleOpen}
        className={[
          "flex items-center gap-2 px-2.5 border border-gray-300 rounded-sm px-2 py-1 w-60 text-xs transition-all duration-150 outline-none bg-white",
          open
            ? "border-gray-400 ring-2 ring-gray-200"
            : "border-gray-300 hover:border-gray-400",
        ].join(" ")}
      >
        <span className="text-gray-500 shrink-0"><CalIcon /></span>
        <span className={hasCommitted ? "text-gray-800" : "text-gray-400"}>
          {hasCommitted
            ? `${fmtShort(committed.start)} – ${fmtShort(committed.end)}`
            : placeholder}
        </span>
        {hasCommitted && (
          <span
            onClick={handleClear}
            className="ml-auto flex items-center justify-center w-4 h-4 rounded-full hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
          >
            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </span>
        )}
      </button>

      {/* ── Popup panel ── */}
      {open && (
        <div
          className={[
            "absolute z-50 top-[calc(100%+6px)] left-0",
            "bg-white rounded-2xl shadow-2xl border border-gray-100",
            "overflow-hidden",
            // animate in
            "animate-[fadeSlideIn_0.15s_ease-out]",
          ].join(" ")}
          style={{
            width: showSidebar ? 740 : 552,
            boxShadow: "0 12px 48px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.06)",
          }}
        >
          <style>{`
            @keyframes fadeSlideIn {
              from { opacity: 0; transform: translateY(-6px) scale(0.98); }
              to   { opacity: 1; transform: translateY(0)   scale(1); }
            }
          `}</style>

          <div className="flex">
            {/* Sidebar */}
            {showSidebar && (
              <div className="w-44 shrink-0 border-r border-gray-100 py-3 flex flex-col gap-0.5 mt-10">
                {presets.map(key => {
                  const label = ALL_PRESETS[key]; if (!label) return null;
                  const active = activeKey === key;
                  return (
                    <button
                      key={key}
                      onClick={() => handlePreset(key)}
                      className={[
                        "mx-2 px-3 py-2 rounded-full text-xs text-center transition-colors duration-100 cursor-pointer border",
                        active
                          ? "bg-lime-100 text-lime-900 border-lime-500"
                          : "text-gray-600 border-gray-200 hover:border-gray-300 hover:bg-gray-50",
                      ].join(" ")}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>
            )}

            {/* Calendar */}
            <div className="flex flex-col flex-1 min-w-0 p-5">
              {/* Month nav */}
              <div className="flex items-center mb-4 gap-2">
                <button
                  onClick={goLeft}
                  className="w-7 h-7 flex items-center justify-center rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors"
                >
                  <ChevronLeft />
                </button>
                <div className="flex flex-1">
                  <div className="flex-1 text-center text-xs font-semibold text-gray-900">
                    {MONTHS[leftMonth]} {leftYear}
                  </div>
                  <div className="flex-1 text-center text-xs font-semibold text-gray-900">
                    {MONTHS[rightMonth]} {rightYear}
                  </div>
                </div>
                <button
                  onClick={goRight}
                  className="w-7 h-7 flex items-center justify-center rounded-lg text-gray-400 hover:bg-gray-100 hover:text-gray-700 transition-colors"
                >
                  <ChevronRight />
                </button>
              </div>

              {/* Grids */}
              <div
                className="flex gap-5"
                onMouseLeave={() => setHoverDate(null)}
              >
                <MonthGrid
                  year={leftYear} month={leftMonth}
                  startDate={startDate} endDate={endDate}
                  hoverDate={selecting ? hoverDate : null}
                  onDay={handleDay} onHover={handleHover}
                />
                <div className="w-px bg-gray-100 self-stretch" />
                <MonthGrid
                  year={rightYear} month={rightMonth}
                  startDate={startDate} endDate={endDate}
                  hoverDate={selecting ? hoverDate : null}
                  onDay={handleDay} onHover={handleHover}
                />
              </div>

              {/* Footer */}
              <div className="flex items-center justify-between mt-4 pt-4 border-t border-gray-100">
                <div className="text-xs text-gray-400 font-medium">
                  {startDate ? (
                    <span>
                      <span className="text-gray-900 font-semibold">{fmt(startDate)}</span>
                      <span className="mx-1.5 text-gray-300">→</span>
                      <span className={endDate ? "text-gray-900 font-semibold" : "text-gray-400"}>
                        {endDate ? fmt(endDate) : "pick end date…"}
                      </span>
                    </span>
                  ) : (
                    <span>Pick a start date</span>
                  )}
                </div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setOpen(false)}
                    className="h-8 px-4 rounded-full text-xs text-gray-500 hover:bg-gray-100 transition-colors"
                  >
                    Avbryt
                  </button>
                  <button
                    onClick={handleApply}
                    disabled={!canApply}
                    className={[
                      "h-8 px-8 rounded-full text-xs transition-all duration-150",
                      canApply
                        ? "bg-lime-700 text-white hover:bg-lime-900 shadow-sm"
                        : "bg-gray-100 text-gray-300 cursor-not-allowed",
                    ].join(" ")}
                  >
                    Använd
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
