import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listStationsPublic, getUser } from "../services/api";

export default function Home() {
  const [stations, setStations] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const [filters, setFilters] = useState({
    type: "",
    status: "Active",
    minConnectors: "",
  });

  const user = getUser(); // may be null; roles on user?.roles

  useEffect(() => {
    let ignore = false;
    async function run() {
      setLoading(true);
      try {
        const { items, total } = await listStationsPublic({
          type: filters.type || undefined,
          status: filters.status || undefined,
          minConnectors: filters.minConnectors ? Number(filters.minConnectors) : undefined,
          page,
          pageSize,
        });
        if (!ignore) {
          setStations(items || []);
          setTotal(total || 0);
        }
      } catch (e) {
        // best-effort; keep home public
        console.error(e);
      } finally {
        if (!ignore) setLoading(false);
      }
    }
    run();
    return () => { ignore = true; };
  }, [filters, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header / Nav */}
      <header className="bg-white border-b">
        <div className="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
          <Link to="/" className="text-lg font-bold">EV Charge</Link>

          <nav className="flex items-center gap-3">
            <Link className="text-sm text-gray-700 hover:text-black" to="/">Home</Link>
            {/* <a
              className="text-sm text-gray-700 hover:text-black"
              href="https://localhost:8085/swagger"
              target="_blank" rel="noreferrer"
            >
              API Docs
            </a> */}
            {!user ? (
              <>
                <Link
                  to="/login"
                  className="px-3 py-1.5 rounded-md bg-blue-600 text-white text-sm hover:bg-blue-700"
                >
                  Login
                </Link>
                <Link
                  to="/apply-backoffice"
                  className="px-3 py-1.5 rounded-md border text-sm hover:bg-gray-100"
                >
                  Apply as BackOffice
                </Link>
              </>
            ) : (
              <Link
                to={user.roles?.includes("BackOffice") ? "/dashboard/backoffice" : "/dashboard"}
                className="px-3 py-1.5 rounded-md bg-green-600 text-white text-sm hover:bg-green-700"
              >
                Dashboard
              </Link>
            )}
          </nav>
        </div>
      </header>

      {/* Hero */}
      <section className="bg-gradient-to-b from-white to-gray-50">
        <div className="max-w-6xl mx-auto px-4 py-10">
          <h1 className="text-2xl sm:text-3xl font-semibold">
            Find nearby charging stations & manage bookings
          </h1>
          <p className="text-gray-600 mt-2">
            Public directory of stations with simple BackOffice tools for operators.
          </p>
        </div>
      </section>

      {/* Filters */}
      <section>
        <div className="max-w-6xl mx-auto px-4">
          <div className="bg-white border rounded-xl p-4 flex flex-wrap gap-3 items-end">
            <div>
              <label className="block text-xs text-gray-600 mb-1">Type</label>
              <select
                className="border rounded-md px-2 py-1"
                value={filters.type}
                onChange={(e) => { setPage(1); setFilters(f => ({ ...f, type: e.target.value })); }}
              >
                <option value="">Any</option>
                <option value="AC">AC</option>
                <option value="DC">DC</option>
              </select>
            </div>

            <div>
              <label className="block text-xs text-gray-600 mb-1">Status</label>
              <select
                className="border rounded-md px-2 py-1"
                value={filters.status}
                onChange={(e) => { setPage(1); setFilters(f => ({ ...f, status: e.target.value })); }}
              >
                <option value="Active">Active</option>
                <option value="">Any</option>
                <option value="Inactive">Inactive</option>
                <option value="Maintenance">Maintenance</option>
              </select>
            </div>

            <div>
              <label className="block text-xs text-gray-600 mb-1">Min connectors</label>
              <input
                type="number"
                min={1}
                className="border rounded-md px-2 py-1 w-28"
                value={filters.minConnectors}
                onChange={(e) => { setPage(1); setFilters(f => ({ ...f, minConnectors: e.target.value })); }}
                placeholder="e.g. 2"
              />
            </div>

            <button
              className="ml-auto px-3 py-2 rounded-md border text-sm hover:bg-gray-100"
              onClick={() => {
                setFilters({ type: "", status: "Active", minConnectors: "" });
                setPage(1);
              }}
            >
              Reset
            </button>
          </div>
        </div>
      </section>

      {/* Station list */}
      <section className="mt-6">
        <div className="max-w-6xl mx-auto px-4">
          <div className="bg-white border rounded-xl p-4">
            <div className="flex items-center justify-between mb-3">
              <h2 className="font-semibold">Stations</h2>
              <span className="text-sm text-gray-500">{total} total</span>
            </div>

            {loading ? (
              <div className="py-10 text-center text-gray-500">Loading…</div>
            ) : stations.length === 0 ? (
              <div className="py-8 text-center text-gray-500">No stations found.</div>
            ) : (
              <ul className="divide-y">
                {stations.map((s) => (
                  <li key={s.id} className="py-3 flex items-start gap-4">
                    <div className="min-w-0">
                      <div className="font-medium">{s.name}</div>
                      <div className="text-xs text-gray-600 mt-0.5">
                        {s.type} • {s.connectors} connectors • {s.status}
                      </div>
                      <div className="text-xs text-gray-500 mt-1">
                        Lat: {s.lat?.toFixed?.(5)} | Lng: {s.lng?.toFixed?.(5)}
                      </div>
                      {s?.pricing && (
                        <div className="text-xs text-gray-500 mt-1">
                          Pricing: {s.pricing.model}
                          {s.pricing.model === "flat" && ` • Base: ${s.pricing.base}`}
                          {s.pricing.model === "hourly" && ` • Per hour: ${s.pricing.perHour}`}
                          {s.pricing.model === "kwh" && ` • Per kWh: ${s.pricing.perKwh}`}
                        </div>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}

            {/* Pagination */}
            <div className="mt-4 flex items-center justify-between">
              <button
                className="px-3 py-1.5 border rounded-md text-sm disabled:opacity-50"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
              >
                Prev
              </button>
              <div className="text-sm text-gray-600">
                Page {page} / {totalPages}
              </div>
              <button
                className="px-3 py-1.5 border rounded-md text-sm disabled:opacity-50"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
              >
                Next
              </button>
            </div>
          </div>

          {/* CTA Card for BackOffice */}
          <div className="mt-6 bg-white border rounded-xl p-4">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
              <div>
                <div className="font-semibold">Operate your own stations?</div>
                <div className="text-sm text-gray-600">
                  Apply to create a BackOffice account and manage operators & stations.
                </div>
              </div>
              <div className="flex gap-2">
                <Link
                  to="/apply-backoffice"
                  className="px-3 py-2 rounded-md bg-blue-600 text-white text-sm hover:bg-blue-700"
                >
                  Apply as BackOffice
                </Link>
                {!user && (
                  <Link
                    to="/login"
                    className="px-3 py-2 rounded-md border text-sm hover:bg-gray-100"
                  >
                    Login
                  </Link>
                )}
              </div>
            </div>
          </div>

          {/* Footer */}
          <footer className="py-10 text-center text-xs text-gray-500">
            &copy; {new Date().getFullYear()} EV Charge — Thin client
          </footer>
        </div>
      </section>
    </div>
  );
}
