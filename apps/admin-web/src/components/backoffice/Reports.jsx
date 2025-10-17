import { useEffect, useMemo, useState } from "react";
import {
  listBackOfficeStations,
  getReportSummary,
  getBookingTimeSeries,
  getRevenueTimeSeries,
  getRevenueByStation,
  getOccupancyHeatmap,
} from "../../services/api";
import { format, subDays, startOfDay, endOfDay } from "date-fns";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ResponsiveContainer,
  BarChart,
  Bar,
  Legend,
} from "recharts";

function Section({ title, actions, children }) {
  return (
    <section className="bg-white border rounded-xl shadow mb-6">
      <div className="px-4 py-3 border-b flex items-center justify-between">
        <h2 className="font-semibold">{title}</h2>
        <div className="flex gap-2">{actions}</div>
      </div>
      <div className="p-4">{children}</div>
    </section>
  );
}

function Kpi({ label, value, hint }) {
  return (
    <div className="p-4 bg-gray-50 border rounded-lg">
      <div className="text-xs text-gray-500">{label}</div>
      <div className="text-2xl font-semibold">{value}</div>
      {hint ? <div className="text-xs text-gray-500 mt-1">{hint}</div> : null}
    </div>
  );
}

function toUtcISOString(d) {
  // normalize to exact seconds for stable queries
  const iso = new Date(d).toISOString();
  return iso;
}

function useStations() {
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    (async () => {
      try {
        const res = await listBackOfficeStations(1, 200);
        setStations(res.items || []);
      } catch (e) {
        console.error(e);
      } finally {
        setLoading(false);
      }
    })();
  }, []);
  return { stations, loading };
}

export default function Reports() {
  // === Filters (default: last 30 days, all stations)
  const defaultFrom = startOfDay(subDays(new Date(), 30));
  const defaultTo = endOfDay(new Date());

  const { stations } = useStations();
  const [stationId, setStationId] = useState("");
  const [fromUtc, setFromUtc] = useState(toUtcISOString(defaultFrom));
  const [toUtc, setToUtc] = useState(toUtcISOString(defaultTo));

  // === Summary KPIs
  const [kpi, setKpi] = useState(null);
  const [loadingKpi, setLoadingKpi] = useState(false);

  const refreshSummary = async () => {
    setLoadingKpi(true);
    try {
      const res = await getReportSummary({
        fromUtc,
        toUtc,
        stationId: stationId || undefined,
      });
      setKpi(res);
    } catch (e) {
      console.error(e);
      setKpi(null);
    } finally {
      setLoadingKpi(false);
    }
  };

  useEffect(() => {
    refreshSummary();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // === Booking Time Series
  const [metric, setMetric] = useState("created");
  const [granularity, setGranularity] = useState("day");
  const [seriesBookings, setSeriesBookings] = useState([]);
  const [loadingBookings, setLoadingBookings] = useState(false);

  const refreshBookings = async () => {
    setLoadingBookings(true);
    try {
      const res = await getBookingTimeSeries({
        metric,
        stationId: stationId || undefined,
        fromUtc,
        toUtc,
        granularity,
      });
      setSeriesBookings(
        (res.points || []).map((p) => ({
          x: p.bucketStartUtc,
          value: Number(p.value),
        }))
      );
    } catch (e) {
      console.error(e);
      setSeriesBookings([]);
    } finally {
      setLoadingBookings(false);
    }
  };

  // === Revenue Time Series
  const [seriesRevenue, setSeriesRevenue] = useState([]);
  const [loadingRevenue, setLoadingRevenue] = useState(false);

  const refreshRevenue = async () => {
    setLoadingRevenue(true);
    try {
      const res = await getRevenueTimeSeries({
        stationId: stationId || undefined,
        fromUtc,
        toUtc,
        granularity,
      });
      setSeriesRevenue(
        (res.points || []).map((p) => ({
          x: p.bucketStartUtc,
          value: Number(p.value),
        }))
      );
    } catch (e) {
      console.error(e);
      setSeriesRevenue([]);
    } finally {
      setLoadingRevenue(false);
    }
  };

  // === Revenue by Station
  const [byStation, setByStation] = useState([]);
  const [loadingByStation, setLoadingByStation] = useState(false);

  const refreshByStation = async () => {
    setLoadingByStation(true);
    try {
      const res = await getRevenueByStation({
        fromUtc,
        toUtc,
      });
      setByStation((res.items || []).map((i) => ({ stationId: i.stationId, revenue: Number(i.revenue) })));
    } catch (e) {
      console.error(e);
      setByStation([]);
    } finally {
      setLoadingByStation(false);
    }
  };

  // === Heatmap (optional extra)
  const [heatCells, setHeatCells] = useState([]);
  const [loadingHeat, setLoadingHeat] = useState(false);

  const refreshHeatmap = async () => {
    if (!stationId) {
      setHeatCells([]);
      return;
    }
    setLoadingHeat(true);
    try {
      const res = await getOccupancyHeatmap({
        stationId,
        fromUtc,
        toUtc,
      });
      setHeatCells(res.cells || []);
    } catch (e) {
      console.error(e);
      setHeatCells([]);
    } finally {
      setLoadingHeat(false);
    }
  };

  // one-click refresh everything
  const refreshAll = async () => {
    await Promise.all([
      refreshSummary(),
      refreshBookings(),
      refreshRevenue(),
      refreshByStation(),
      refreshHeatmap(),
    ]);
  };

  // helper to make nice tick labels
  const tickFormatter = (iso) => {
    try {
      return format(new Date(iso), granularity === "month" ? "yyyy-MM" : "MMM d");
    } catch {
      return iso;
    }
  };

  const heatMatrix = useMemo(() => {
    // Build 7x24 matrix (0=Sun .. 6=Sat)
    const m = Array.from({ length: 7 }, () => Array.from({ length: 24 }, () => 0));
    heatCells.forEach((c) => {
      if (c.dow >= 0 && c.dow <= 6 && c.hour >= 0 && c.hour <= 23) {
        m[c.dow][c.hour] = c.avgReservedPct || 0;
      }
    });
    return m;
  }, [heatCells]);

  return (
    <div className="p-4 max-w-7xl mx-auto">
      <div className="mb-4">
        <h1 className="text-2xl font-semibold">Reports &amp; Summary</h1>
        <p className="text-sm text-gray-600">
          Filter by date range and (optionally) station, then refresh to update all cards & charts.
        </p>
      </div>

      {/* Filters */}
      <Section
        title="Filters"
        actions={
          <>
            <button className="px-3 py-1.5 border rounded" onClick={refreshAll}>
              Refresh All
            </button>
          </>
        }
      >
        <div className="grid md:grid-cols-4 gap-3">
          <div>
            <label className="text-sm">From (UTC)</label>
            <input
              type="datetime-local"
              className="w-full border rounded px-2 py-1.5"
              value={format(new Date(fromUtc), "yyyy-MM-dd'T'HH:mm")}
              onChange={(e) => setFromUtc(new Date(e.target.value).toISOString())}
            />
          </div>
          <div>
            <label className="text-sm">To (UTC)</label>
            <input
              type="datetime-local"
              className="w-full border rounded px-2 py-1.5"
              value={format(new Date(toUtc), "yyyy-MM-dd'T'HH:mm")}
              onChange={(e) => setToUtc(new Date(e.target.value).toISOString())}
            />
          </div>
          <div>
            <label className="text-sm">Station (optional)</label>
            <select
              className="w-full border rounded px-2 py-1.5"
              value={stationId}
              onChange={(e) => setStationId(e.target.value)}
            >
              <option value="">All stations</option>
              {stations.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.type})
                </option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-2 items-end">
            <div>
              <label className="text-sm">Granularity</label>
              <select
                className="w-full border rounded px-2 py-1.5"
                value={granularity}
                onChange={(e) => setGranularity(e.target.value)}
              >
                <option value="day">Day</option>
                <option value="week">Week</option>
                <option value="month">Month</option>
              </select>
            </div>
            <div>
              <label className="text-sm">Bookings metric</label>
              <select
                className="w-full border rounded px-2 py-1.5"
                value={metric}
                onChange={(e) => setMetric(e.target.value)}
              >
                <option value="created">Created</option>
                <option value="approved">Approved</option>
                <option value="rejected">Rejected</option>
                <option value="cancelled">Cancelled</option>
                <option value="checkedin">Checked-in</option>
                <option value="completed">Completed</option>
              </select>
            </div>
          </div>
        </div>
      </Section>

      {/* Summary KPIs */}
      <Section
        title="Summary KPIs"
        actions={
          <button className="px-3 py-1.5 border rounded" onClick={refreshSummary} disabled={loadingKpi}>
            {loadingKpi ? "Loading…" : "Refresh"}
          </button>
        }
      >
        {kpi ? (
          <div className="grid md:grid-cols-4 gap-3">
            <Kpi label="Bookings Created" value={kpi.bookingsCreated} />
            <Kpi label="Approved" value={kpi.approved} hint={`Approval rate ${(kpi.approvalRate * 100).toFixed(1)}%`} />
            <Kpi label="Checked-in" value={kpi.checkedIn} hint={`Check-in rate ${(kpi.checkInRate * 100).toFixed(1)}%`} />
            <Kpi label="Completed" value={kpi.completed} hint={`Completion rate ${(kpi.completionRate * 100).toFixed(1)}%`} />
            <Kpi label="Rejected" value={kpi.rejected} />
            <Kpi label="Cancelled" value={kpi.cancelled} />
            <Kpi label="Revenue (total)" value={Number(kpi.revenueTotal).toFixed(2)} />
            <Kpi label="Energy (kWh)" value={Number(kpi.energyTotalKwh).toFixed(2)} />
          </div>
        ) : (
          <div className="text-sm text-gray-600">No data. Click Refresh.</div>
        )}
      </Section>

      {/* Booking Time Series */}
      <Section
        title={`Bookings over time — ${metric} (${granularity})`}
        actions={
          <button className="px-3 py-1.5 border rounded" onClick={refreshBookings} disabled={loadingBookings}>
            {loadingBookings ? "Loading…" : "Refresh"}
          </button>
        }
      >
        <div className="w-full h-72">
          <ResponsiveContainer>
            <LineChart data={seriesBookings}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="x" tickFormatter={tickFormatter} />
              <YAxis allowDecimals={false} />
              <Tooltip labelFormatter={(l) => format(new Date(l), "PPp")} />
              <Legend />
              <Line type="monotone" dataKey="value" name={metric} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </Section>

      {/* Revenue Time Series */}
      <Section
        title={`Revenue over time (${granularity})`}
        actions={
          <button className="px-3 py-1.5 border rounded" onClick={refreshRevenue} disabled={loadingRevenue}>
            {loadingRevenue ? "Loading…" : "Refresh"}
          </button>
        }
      >
        <div className="w-full h-72">
          <ResponsiveContainer>
            <LineChart data={seriesRevenue}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="x" tickFormatter={tickFormatter} />
              <YAxis />
              <Tooltip labelFormatter={(l) => format(new Date(l), "PPp")} />
              <Legend />
              <Line type="monotone" dataKey="value" name="Revenue" dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </Section>

      {/* Revenue by Station */}
      <Section
        title="Revenue by station"
        actions={
          <button className="px-3 py-1.5 border rounded" onClick={refreshByStation} disabled={loadingByStation}>
            {loadingByStation ? "Loading…" : "Refresh"}
          </button>
        }
      >
        <div className="w-full h-72">
          <ResponsiveContainer>
            <BarChart data={byStation}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="stationId"
                tickFormatter={(id) => (id ? id.slice(0, 6) + "…" : "")}
                interval={0}
                angle={-30}
                textAnchor="end"
                height={60}
              />
              <YAxis />
              <Tooltip />
              <Legend />
              <Bar dataKey="revenue" name="Revenue" />
            </BarChart>
          </ResponsiveContainer>
        </div>
        <div className="text-xs text-gray-500 mt-2">
          Tip: Hover for exact values. Station names can be shown by resolving IDs client-side if you prefer.
        </div>
      </Section>

      {/* Optional: Occupancy Heatmap (requires a Station selected) */}
      <Section
        title="Occupancy heatmap (avg reserved % by day/hour — select a station)"
        actions={
          <button className="px-3 py-1.5 border rounded" onClick={refreshHeatmap} disabled={loadingHeat || !stationId}>
            {loadingHeat ? "Loading…" : "Refresh"}
          </button>
        }
      >
        {!stationId ? (
          <div className="text-sm text-gray-600">Select a Station above, then click Refresh.</div>
        ) : (
          <div className="overflow-auto">
            <table className="text-xs border">
              <thead>
                <tr>
                  <th className="border px-2 py-1 bg-gray-50 text-left">Day \ Hour</th>
                  {Array.from({ length: 24 }, (_, h) => (
                    <th key={h} className="border px-2 py-1 bg-gray-50">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {["Sun","Mon","Tue","Wed","Thu","Fri","Sat"].map((d, i) => (
                  <tr key={d}>
                    <td className="border px-2 py-1 bg-gray-50 font-medium">{d}</td>
                    {Array.from({ length: 24 }, (_, h) => {
                      const v = heatMatrix[i][h]; // 0..1
                      const pct = Math.round(v * 100);
                      const bg = `hsl(${120 - Math.min(120, pct)}, 70%, ${90 - Math.min(60, pct/100*60)}%)`;
                      return (
                        <td
                          key={h}
                          className="border text-center"
                          title={`${pct}%`}
                          style={{ background: bg, minWidth: 24 }}
                        >
                          {pct ? `${pct}%` : ""}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Section>
    </div>
  );
}
