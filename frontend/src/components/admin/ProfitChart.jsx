import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSessionExpiry } from "../../auth/useSessionExpiry.js";
import { fetchProfitReport, labelledDays, linePath, nearestDayIndex, profitHighlights, profitPeriod, PROFIT_DAYS, SERIES, chartScales } from "../../lib/profit-report.js";
import { formatPrice } from "../../lib/product-display.js";
import { useRequest } from "../../lib/useRequest.js";
import { ErrorState } from "../RequestStates.js";
import Table from "../ui/Table.js";

const WIDTH = 720;
const HEIGHT = 300;
const shortDay = value => new Date(`${value}T00:00:00Z`).toLocaleDateString("tr-TR", { day: "numeric", month: "short", timeZone: "UTC" });
const compact = new Intl.NumberFormat("tr-TR", { notation: "compact", maximumFractionDigits: 1 });

// Revenue, cost and profit per day for the last 30 days (SCRUM-87; data from SCRUM-152, docs/profit-report-api.md).
// Three lines on one axis that includes zero. Colors are the first three validated categorical slots (see
// styles/admin.css). Hover or the arrow keys pick a day; every day is also in the table under the chart.
export default function ProfitChart() {
  const read = useRequest();
  useSessionExpiry(read.error);
  const [report, setReport] = useState(null);
  const [period] = useState(() => profitPeriod(new Date(), PROFIT_DAYS));
  const [active, setActive] = useState(null);
  const svg = useRef(null);

  const load = useCallback(async () => {
    const result = await read.run(({ signal }) => fetchProfitReport({ ...period, signal }));
    if (result.status === "success") setReport(result.data);
  }, [read.run, period]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);

  const days = report?.days ?? [];
  const scales = useMemo(() => days.length ? chartScales(days, { width: WIDTH, height: HEIGHT }) : null, [report]);
  const highlights = useMemo(() => profitHighlights(days), [report]);
  const loading = read.status === "loading";

  function pointerMove(event) {
    const box = svg.current?.getBoundingClientRect();
    if (!box || !scales || box.width === 0) return;
    setActive(nearestDayIndex(days, ((event.clientX - box.left) / box.width) * WIDTH, scales));
  }
  function keyDown(event) {
    if (!days.length) return;
    const step = event.key === "ArrowRight" ? 1 : event.key === "ArrowLeft" ? -1 : 0;
    if (event.key === "Escape") setActive(null);
    if (step === 0) return;
    event.preventDefault();
    setActive(previous => Math.min(days.length - 1, Math.max(0, (previous ?? (step > 0 ? -1 : days.length)) + step)));
  }

  const tableColumns = [
    { key: "date", header: "Day (UTC)", rowHeader: true, render: row => shortDay(row.date) },
    ...SERIES.map(series => ({ key: series.key, header: series.label, align: "end", render: row => formatPrice(row[series.key]) }))
  ];
  const day = active === null ? null : days[active];

  return <section className="admin-profit" aria-labelledby="profit-heading">
    <div className="admin-panel-head"><h2 id="profit-heading">Revenue, cost and profit</h2></div>
    <p className="admin-lead">Per day for the last {PROFIT_DAYS} days (UTC). Cost is 50% of the price unless the product has its own cost.</p>
    <ErrorState error={read.error} onRetry={load} retrying={loading} />
    {loading && !report && <Table columns={tableColumns} loading loadingLabel="Loading the chart…" />}
    {report && <>
      <ul className="admin-profit-legend" aria-label="Series">
        {SERIES.map(series => <li key={series.key}>
          <span className={`admin-profit-swatch admin-profit-swatch--${series.key}`} aria-hidden="true" />
          {series.label}: <strong>{formatPrice(report[series.key])}</strong>
        </li>)}
      </ul>
      {highlights && <p className="admin-profit-note">Best day {shortDay(highlights.best.date)} ({formatPrice(highlights.best.profit)} profit);
        weakest day {shortDay(highlights.worst.date)} ({formatPrice(highlights.worst.profit)}).</p>}
      {!highlights && <p className="admin-empty" role="status">No sales in this period yet.</p>}
      <div className="admin-profit-plot">
        <svg ref={svg} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" tabIndex={0}
          aria-label={`Line chart of daily revenue, cost and profit. Use the left and right arrow keys to read a day; the same numbers are in the table below.`}
          onPointerMove={pointerMove} onPointerLeave={() => setActive(null)} onKeyDown={keyDown} onBlur={() => setActive(null)}>
          {scales.ticks.map(tick => <g key={tick}>
            <line className={tick === 0 ? "admin-profit-zero" : "admin-profit-grid"} x1={scales.left} x2={WIDTH - scales.right}
              y1={scales.y(tick)} y2={scales.y(tick)} />
            <text className="admin-profit-tick" x={scales.left - 8} y={scales.y(tick)} textAnchor="end" dominantBaseline="middle">{compact.format(tick)}</text>
          </g>)}
          {labelledDays(days.length).map(index => <text key={index} className="admin-profit-tick" x={scales.x(index)} y={HEIGHT - 8}
            textAnchor={index === 0 ? "start" : index === days.length - 1 ? "end" : "middle"}>{shortDay(days[index].date)}</text>)}
          {SERIES.map(series => <path key={series.key} className={`admin-profit-line admin-profit-line--${series.key}`}
            d={linePath(days, series.key, scales)} fill="none" />)}
          {day && <g>
            <line className="admin-profit-cursor" x1={scales.x(active)} x2={scales.x(active)} y1={scales.top} y2={HEIGHT - scales.bottom} />
            {SERIES.map(series => <circle key={series.key} className={`admin-profit-dot admin-profit-dot--${series.key}`}
              cx={scales.x(active)} cy={scales.y(day[series.key])} r="5" />)}
          </g>}
        </svg>
        {day && <div className="admin-profit-tip" role="status"
          style={{ left: `${(scales.x(active) / WIDTH) * 100}%` }}>
          <strong>{shortDay(day.date)}</strong>
          {SERIES.map(series => <span key={series.key}>{series.label}: {formatPrice(day[series.key])}</span>)}
        </div>}
      </div>
      <details className="admin-profit-table">
        <summary>Show the numbers as a table</summary>
        <Table columns={tableColumns} rows={days} rowKey={row => row.date} caption="Revenue, cost and profit per day" />
      </details>
    </>}
  </section>;
}
